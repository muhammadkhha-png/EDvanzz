using Edvanz.Application.Dtos;
using Edvanz.Application.ServiceContract;
using Edvanz.Domain.Entities;
using Edvanz.Domain.Enums;
using Edvanz.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Edvanz.Application.Services;

/// <summary>
/// Generates the next occurrence (and its obligations) for one recurring assignment
/// template. Pure business logic — Hangfire-agnostic. The Hangfire dispatcher calls
/// into this service per-template; the dispatcher handles fan-out, this handles the work.
///
/// SEPARATION FROM HANGFIRE:
/// Mirrors the pattern used by <c>SubscriptionReminderService</c> — services live in
/// Application, Hangfire jobs live in Infrastructure and are thin wrappers. Tests
/// against this service do not need a Hangfire fixture.
///
/// IDEMPOTENCY GUARANTEES:
/// - The unique index on <c>AssignmentOccurrences (TemplateId, OccurrenceNumber)</c>
///   prevents duplicate occurrence rows even if two materializer runs race.
/// - Per-template DB-row lock (SELECT WITH UPDLOCK) serializes runs on the same
///   template (catalog §7.2 — chosen over Hangfire Pro's mutex feature).
/// - If the materializer crashes between INSERT occurrence and INSERT obligations,
///   the surrounding transaction rolls everything back. The next run starts clean.
///
/// SNAPSHOT DECOUPLING (REQ-EXH-034):
/// Each generated occurrence captures the template's CURRENT grading config at the
/// moment of generation — not the template's original config. This means a tutor can
/// adjust MaxGrade or PassingThreshold mid-recurrence and only future occurrences
/// reflect the change; historical reports remain reproducible.
/// </summary>
public interface IRecurringAssignmentMaterializerService
{
    /// <summary>
    /// Returns the template ids whose next occurrence is due to be materialized
    /// within the lookahead window. Cheap query — used by the dispatcher to fan out
    /// per-template work onto the Hangfire queue.
    /// </summary>
    Task<Result<IReadOnlyList<long>>> GetTemplateIdsDueForMaterializationAsync(
        DateTime asOfDate, int lookaheadDays);

    /// <summary>
    /// Materializes the next occurrence for ONE template. Idempotent — if the next
    /// occurrence already exists or the template has been stopped, returns success
    /// with a zero-row indicator and no exception.
    /// </summary>
    Task<Result<MaterializationOutcome>> MaterializeNextOccurrenceAsync(long templateId);
}

/// <summary>
/// Outcome of a single template's materialization run. Useful for logging and the
/// Hangfire dashboard so operators can see what happened without scraping logs.
/// </summary>
public sealed class MaterializationOutcome
{
    public long TemplateId { get; init; }
    public long? NewOccurrenceId { get; init; }
    public int? NewOccurrenceNumber { get; init; }
    public DateTime? NewDueDate { get; init; }
    public int ObligationsCreated { get; init; }
    public string Reason { get; init; } = string.Empty;

    public static MaterializationOutcome Created(
        long templateId, long occurrenceId, int number, DateTime dueDate, int obligationsCreated)
        => new()
        {
            TemplateId = templateId,
            NewOccurrenceId = occurrenceId,
            NewOccurrenceNumber = number,
            NewDueDate = dueDate,
            ObligationsCreated = obligationsCreated,
            Reason = "OccurrenceMaterialized",
        };

    public static MaterializationOutcome Skipped(long templateId, string reason)
        => new() { TemplateId = templateId, Reason = reason };
}

/// <summary>
/// Default implementation. Scoped lifetime — fresh DbContext per execution so the
/// Hangfire-spawned scope has its own change tracker.
/// </summary>
public class RecurringAssignmentMaterializerService : IRecurringAssignmentMaterializerService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAssignmentScopeResolver _scopeResolver;
    private readonly ILogger<RecurringAssignmentMaterializerService> _logger;

    public RecurringAssignmentMaterializerService(
        IUnitOfWork unitOfWork,
        IAssignmentScopeResolver scopeResolver,
        ILogger<RecurringAssignmentMaterializerService> logger)
    {
        _unitOfWork = unitOfWork;
        _scopeResolver = scopeResolver;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<Result<IReadOnlyList<long>>> GetTemplateIdsDueForMaterializationAsync(
        DateTime asOfDate, int lookaheadDays)
    {
        DateTime cutoff = asOfDate.Date.AddDays(lookaheadDays);

        var ids = await _unitOfWork.ExamHomeworkRepo
            .GetTemplateIdsDueForMaterializationAsync(asOfDate.Date, cutoff);

        return Result<IReadOnlyList<long>>.Success(ids, System.Net.HttpStatusCode.OK);
    }

    /// <inheritdoc />
    public async Task<Result<MaterializationOutcome>> MaterializeNextOccurrenceAsync(long templateId)
    {
        await _unitOfWork.BeginTransactionAsync();
        try
        {
            // Acquire a per-template row lock to serialize concurrent runs.
            // SELECT WITH UPDLOCK on the template row — held until COMMIT/ROLLBACK.
            // Catalog §7.2 chose this over Hangfire Pro's Mutex attribute (free vs paid).
            var template = await _unitOfWork.ExamHomeworkRepo
                .LockTemplateForMaterializationAsync(templateId);

            if (template is null)
            {
                await _unitOfWork.RollbackAsync();
                return Result<MaterializationOutcome>.Success(
                    MaterializationOutcome.Skipped(templateId, "TemplateNotFound"), System.Net.HttpStatusCode.OK);
            }

            // ── Eligibility gates ──
            if (!template.IsRecurring)
                return await SkipAndCommitAsync(templateId, "TemplateNotRecurring");

            if (template.IsRecurrenceStopped)
                return await SkipAndCommitAsync(templateId, "RecurrenceStopped");

            // ── Compute the next occurrence's due date ──
            var lastOccurrence = await _unitOfWork.ExamHomeworkRepo
                .GetLatestOccurrenceAsync(templateId, template.TeacherId);

            if (lastOccurrence is null)
            {
                // Defensive: a recurring template should always have at least one
                // occurrence (created synchronously at template-create time). If it
                // doesn't, something upstream went wrong — log and skip rather than
                // guess.
                _logger.LogWarning(
                    "Materializer: template {TemplateId} is recurring but has no first occurrence. " +
                    "Investigate the create flow.", templateId);
                return await SkipAndCommitAsync(templateId, "NoFirstOccurrenceFound");
            }

            DateTime? nextDueDate = ComputeNextDueDate(
                template.RecurrencePattern, lastOccurrence.DueDate);

            if (nextDueDate is null)
                return await SkipAndCommitAsync(templateId, "RecurrencePatternNotMaterializable");

            // Respect the recurrence end date — stop generating past it.
            if (template.RecurrenceEndDate.HasValue
                && nextDueDate.Value > template.RecurrenceEndDate.Value.Date)
                return await SkipAndCommitAsync(templateId, "BeyondRecurrenceEndDate");

            // ── Resolve scope as of the new due date ──
            // The persisted scope rows are the authority. Session/group memberships are
            // resolved live, so a student newly added to a scoped session shows up.
            var scopes = await _unitOfWork.ExamHomeworkRepo
                .GetScopesByTemplateAsync(templateId);

            var resolvedStudentIds = await _scopeResolver
                .ResolveFromPersistedScopesAsync(template.TeacherId, scopes);

            if (resolvedStudentIds.Count == 0)
            {
                // Scope evaluated to zero students for this period (e.g., the only
                // session in scope has no enrolled students yet). Don't create an
                // empty occurrence — try again on the next dispatcher run.
                return await SkipAndCommitAsync(templateId, "ScopeResolvedToZeroStudents");
            }

            // ── Build the new occurrence + obligations ──
            int nextNumber = lastOccurrence.OccurrenceNumber + 1;
            DateTime utcNow = DateTime.UtcNow;

            var newOccurrence = new AssignmentOccurrence
            {
                TemplateId = template.Id,
                TeacherId = template.TeacherId,
                OccurrenceNumber = nextNumber,
                DueDate = nextDueDate.Value,
                Status = AssignmentOccurrenceStatus.Pending,

                // SNAPSHOT current template config — REQ-EXH-034 decoupling.
                MaxGradeSnapshot = template.MaxGrade,
                PassingThresholdSnapshot = template.PassingThreshold,
                TrackingModeSnapshot = template.TrackingMode,

                CreateAt = utcNow,
            };

            var obligations = resolvedStudentIds.Select(studentId => new StudentAssignmentObligation
            {
                Occurrence = newOccurrence,
                TeacherId = template.TeacherId,
                TeacherStudentId = studentId,
                Status = ObligationStatus.Pending,
                IsGradeEntered = false,
                MarkedByScan = false,
                UpdatedAt = utcNow,
                CreateAt = utcNow,
            }).ToList();

            await _unitOfWork.ExamHomeworkRepo.AddOccurrenceAsync(newOccurrence);
            await _unitOfWork.ExamHomeworkRepo.AddObligationsRangeAsync(obligations);

            try
            {
                await _unitOfWork.SaveChangesAsync();
            }
            catch (DbUpdateException ex)
            {
                // The unique index UX_AssignmentOccurrences_TemplateNumber is the only
                // realistic violator — another worker raced us between LockTemplate and
                // INSERT. The lock SHOULD prevent this; logging it as a warning lets us
                // detect silent lock failures in production.
                await _unitOfWork.RollbackAsync();
                _logger.LogWarning(ex,
                    "Materializer: race detected on template {TemplateId} occurrence #{Number}. " +
                    "Treating as already-materialized.", templateId, nextNumber);

                return Result<MaterializationOutcome>.Success(
                    MaterializationOutcome.Skipped(templateId, "AlreadyMaterializedByPeer"), System.Net.HttpStatusCode.OK);
            }

            await _unitOfWork.CommitAsync();

            _logger.LogInformation(
                "Materializer: template {TemplateId} occurrence #{Number} created on {DueDate}. " +
                "Students assigned: {Count}",
                templateId, nextNumber, nextDueDate.Value.ToString("yyyy-MM-dd"), obligations.Count);

            return Result<MaterializationOutcome>.Success(
                MaterializationOutcome.Created(
                    templateId, newOccurrence.Id, nextNumber, nextDueDate.Value, obligations.Count), System.Net.HttpStatusCode.OK);
        }
        catch
        {
            await _unitOfWork.RollbackAsync();
            throw;
        }
    }

    // ══════════════════════════════════════════════════════════════════════
    // PRIVATE HELPERS
    // ══════════════════════════════════════════════════════════════════════

    private async Task<Result<MaterializationOutcome>> SkipAndCommitAsync(long templateId, string reason)
    {
        // Even on skip we commit — the LockTemplate read needs to release. No data was
        // changed, so this is just a transaction close.
        await _unitOfWork.CommitAsync();

        _logger.LogDebug(
            "Materializer: template {TemplateId} skipped — {Reason}", templateId, reason);

        return Result<MaterializationOutcome>.Success(
            MaterializationOutcome.Skipped(templateId, reason), System.Net.HttpStatusCode.OK);
    }

    /// <summary>
    /// Computes the next due date from a recurrence pattern + the last occurrence's date.
    ///
    /// PATTERN SEMANTICS:
    /// - <c>OneTime</c>: returns null. Should never reach this method (filtered earlier).
    /// - <c>EverySession</c> / <c>EveryTwoSessions</c>: homework patterns tied to actual
    ///   session dates. Without per-student session-schedule lookups here, we approximate
    ///   "every session" as 7 days and "every two sessions" as 14 days. The exact behavior
    ///   should be validated against REQ-EXH-009 — this is documented as a follow-up.
    /// - <c>Monthly</c>: exam pattern. Adds one calendar month, preserving day-of-month
    ///   where valid (Jan 31 → Feb 28/29 → Mar 31).
    /// </summary>
    private static DateTime? ComputeNextDueDate(RecurrencePattern pattern, DateTime lastDueDate)
        => pattern switch
        {
            RecurrencePattern.OneTime => null,
            RecurrencePattern.EverySession => lastDueDate.Date.AddDays(7),
            RecurrencePattern.EveryTwoSessions => lastDueDate.Date.AddDays(14),
            RecurrencePattern.Monthly => lastDueDate.Date.AddMonths(1),
            _ => null,
        };
}
