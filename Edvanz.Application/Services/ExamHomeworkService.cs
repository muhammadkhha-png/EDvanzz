using System.Net;
using System.Text.Json;
using Edvanz.Application.Dtos;
using Edvanz.Application.Dtos.ExamHomework;
using Edvanz.Application.ServiceContract;
using Edvanz.Domain.Entities;
using Edvanz.Domain.Enums;
using Edvanz.Domain.Interfaces;
using Edvanz.Domain.Resources;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;

namespace Edvanz.Application.Services;

/// <summary>
/// Implements the Application contract for the Exams &amp; Homework Module (Module 6).
///
/// LAYER POSITION: Application. Depends on <see cref="IUnitOfWork"/> for persistence
/// and <see cref="IStringLocalizer{Messages}"/> for bilingual messages. Zero direct
/// dependency on EF Core or any Infrastructure type.
///
/// SCOPE OF THIS FILE:
/// Phase 2 — template lifecycle (create, list, get, update, delete, stop-recurrence,
/// list-occurrences). Phase 3 methods are stubbed and will be filled in next.
///
/// DESIGN DECISIONS REFLECTED HERE:
/// - REQ-EXH-NFR-004: every read &amp; write is keyed by <c>teacherId</c> (resolved from
///   JWT in the controller, never from the body or route).
/// - REQ-EXH-007: first occurrence is materialized synchronously inside the create
///   transaction so the obligation list is visible immediately.
/// - REQ-EXH-013: recurrence-pattern edits are locked once any obligation is non-Pending
///   (returns 422 — see review note: this is a business rule violation, not a 409 race).
/// - REQ-EXH-034 + snapshots: edits to grading config NEVER overwrite snapshots on
///   already-materialized occurrences. Historical reproducibility wins over recompute.
/// - BR-EXH-002: stopping a recurrence preserves all previously generated occurrences.
/// - REQ-EXH-037: hard delete is final, but a JSON snapshot survives in
///   <c>AssignmentDeletionLogs</c>; audit-log rows are archived into the snapshot then
///   bulk-deleted (their FK to obligations is Restrict, so cascade would otherwise fail).
///
/// ARCHITECTURAL RULE FOLLOWED:
/// Every database operation goes through a NAMED method on a repo. No raw expression
/// predicates and no <c>GetQueryable()</c> calls in this file. Same rule the codebase
/// already enforces for PaymentService, AttendanceService, etc.
/// </summary>
public class ExamHomeworkService : IExamHomeworkService
{
    // ══════════════════════════════════════════════════════════════════════
    // CONSTANTS — extracted to remove magic numbers
    // ══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Cap on per-template student resolution at create time. Above this, the
    /// synchronous first-occurrence materialization risks blowing the transaction
    /// budget. REQ-EXH-007 promises immediate visibility — that contract stops here.
    /// Larger scopes must use Hangfire materialization (out of scope for v1).
    /// </summary>
    private const int MaxStudentsPerOccurrence = 5_000;

    /// <summary>
    /// Cap on per-student detail in deletion-log JSON snapshots. Above this we
    /// store a summary instead of the full per-student list, per
    /// <see cref="AssignmentDeletionLog.TemplateSnapshotJson"/> documentation.
    /// </summary>
    private const int DeletionSnapshotPerStudentCap = 10_000;

    private readonly IUnitOfWork _unitOfWork;
    private readonly IStringLocalizer<Messages> _localizer;

    public ExamHomeworkService(IUnitOfWork unitOfWork, IStringLocalizer<Messages> localizer)
    {
        _unitOfWork = unitOfWork;
        _localizer = localizer;
    }

    // ══════════════════════════════════════════════════════════════════════
    // TEMPLATE LIFECYCLE
    // ══════════════════════════════════════════════════════════════════════

    /// <inheritdoc />
    public async Task<Result<AssignmentTemplateDto>> CreateTemplateAsync(
        long teacherId, long actingUserId, CreateAssignmentTemplateDto dto)
    {
        // ── 1. Input validation (in-memory rules) ──
        var inputValidation = ValidateCreateInput(dto);
        if (!inputValidation.IsSuccess) return inputValidation;

        // ── 2. Scope ownership validation (DB-bound) ──
        var ownership = await ValidateScopeOwnershipAsync(teacherId, dto.Scopes);
        if (!ownership.IsSuccess)
            return Result<AssignmentTemplateDto>.Failure(
                _localizer, ownership.MessageKey!, ownership.StatusCode);

        // ── 3. Resolve & dedupe target students BEFORE the transaction ──
        // Read-only; minimizes the write-lock window. Reuses PaymentsRepo helpers
        // (single source of truth for "who's in a session").
        var resolvedStudentIds = await ResolveAndDedupeStudentsAsync(teacherId, dto.Scopes);
        if (resolvedStudentIds.Count == 0)
            return Result<AssignmentTemplateDto>.Failure(
                _localizer, "ScopeResolvedToZeroStudents", HttpStatusCode.BadRequest);

        if (resolvedStudentIds.Count > MaxStudentsPerOccurrence)
            return Result<AssignmentTemplateDto>.Failure(
                _localizer, "ScopeResolutionTooLarge", HttpStatusCode.BadRequest);

        // ── 4. Build the entity graph: template → scopes → first occurrence → obligations ──
        DateTime utcNow = DateTime.UtcNow;
        DateTime assignmentDate = dto.AssignmentDate.Date;

        var template = BuildTemplateFromDto(dto, teacherId, actingUserId, utcNow);
        var scopeEntities = BuildScopesFromDto(dto.Scopes, template, teacherId, utcNow);
        var firstOccurrence = BuildFirstOccurrence(template, teacherId, assignmentDate, utcNow);
        var obligations = BuildObligations(firstOccurrence, teacherId, resolvedStudentIds, utcNow);

        // ── 5. Persist within a transaction ──
        await _unitOfWork.BeginTransactionAsync();
        try
        {
            await _unitOfWork.ExamHomeworkRepo.AddTemplateAsync(template);
            await _unitOfWork.ExamHomeworkRepo.AddScopesRangeAsync(scopeEntities);
            await _unitOfWork.ExamHomeworkRepo.AddOccurrenceAsync(firstOccurrence);
            await _unitOfWork.ExamHomeworkRepo.AddObligationsRangeAsync(obligations);

            await _unitOfWork.SaveChangesAsync();
            await _unitOfWork.CommitAsync();
        }
        catch (DbUpdateException)
        {
            await _unitOfWork.RollbackAsync();
            // The unique index UX_StudentAssignmentObligations_Occurrence_Student is the
            // only realistic violator; our own dedupe should prevent it.
            return Result<AssignmentTemplateDto>.Failure(
                _localizer, "DatabaseConflict", HttpStatusCode.Conflict);
        }

        // ── 6. Build response ──
        var hydrated = await _unitOfWork.ExamHomeworkRepo
            .GetTemplateWithScopesAsync(template.Id, teacherId);

        var responseDto = MapTemplateToDto(hydrated!);
        responseDto.FirstOccurrenceId = firstOccurrence.Id;
        responseDto.StudentsAssigned = obligations.Count;

        return Result<AssignmentTemplateDto>.Success(
            responseDto, _localizer, "AssignmentCreated", HttpStatusCode.Created);
    }

    /// <inheritdoc />
    public async Task<Result<PaginatedResponse<List<AssignmentOverviewItemDto>>>> GetOverviewAsync(
        long teacherId, AssignmentOverviewRequest request)
    {
        // ── 1. Page query — all filters and pagination handled by the repo. ──
        // The repo method follows the same (Items, TotalCount) pattern as every other
        // paged method in IExamHomeworkRepo (GetTrackingViewPagedAsync etc.). The service
        // does not compose any LINQ — it consumes a materialized result.
        var (pageEntities, totalCount) = await _unitOfWork.ExamHomeworkRepo
            .GetAssignmentOverviewPagedAsync(
                teacherId,
                request.Search,
                request.AssignmentType,
                request.RecurrencePattern,
                request.IsRecurring,
                request.Page,
                request.PageSize);

        // ── 2. Aggregate side-loads (O(N+constant) — no per-row queries). ──
        var templateIds = pageEntities.Select(t => t.Id).ToList();

        var latestOccurrenceIds = await _unitOfWork.ExamHomeworkRepo
            .GetLatestOccurrenceIdsByTemplateAsync(templateIds);

        var occurrenceSummaries = await _unitOfWork.ExamHomeworkRepo
            .GetCompletionSummariesByOccurrenceIdsAsync(latestOccurrenceIds.Values);

        var nextOrLastDates = await _unitOfWork.ExamHomeworkRepo
            .GetNextOrLastOccurrenceDatesAsync(templateIds, DateTime.UtcNow);

        var scopeCounts = await _unitOfWork.ExamHomeworkRepo
            .GetScopeCountsByTemplateIdsAsync(templateIds);

        // ── 3. Map ──
        var items = pageEntities.Select(t => new AssignmentOverviewItemDto
        {
            Id = t.Id,
            AssignmentType = t.AssignmentType,
            Name = t.Name,
            NameAr = t.NameAr,
            IsRecurring = t.IsRecurring,
            RecurrencePattern = t.RecurrencePattern,
            IsRecurrenceStopped = t.IsRecurrenceStopped,
            NextOrLastOccurrenceDate = nextOrLastDates.GetValueOrDefault(t.Id),
            ScopeSummary = BuildScopeSummary(scopeCounts.GetValueOrDefault(t.Id)),
            CompletionSummary = MapToCompletionSummaryDto(
                latestOccurrenceIds.TryGetValue(t.Id, out var occId)
                    ? occurrenceSummaries.GetValueOrDefault(occId)
                    : null),
            CreatedAt = t.CreateAt,
        }).ToList();

        var response = new PaginatedResponse<List<AssignmentOverviewItemDto>>
        {
            data = items,
            page = request.Page,
            pageSize = request.PageSize,
            totalCount = totalCount,
            totalPages = (int)Math.Ceiling(totalCount / (double)request.PageSize),
        };
        return Result<PaginatedResponse<List<AssignmentOverviewItemDto>>>.Success(
            response, _localizer);
    }

    /// <inheritdoc />
    public async Task<Result<AssignmentTemplateDto>> GetTemplateByIdAsync(
        long teacherId, long templateId)
    {
        var template = await _unitOfWork.ExamHomeworkRepo
            .GetTemplateWithScopesAsync(templateId, teacherId);

        if (template is null)
            return Result<AssignmentTemplateDto>.Failure(
                _localizer, "TemplateNotFound", HttpStatusCode.NotFound);

        return Result<AssignmentTemplateDto>.Success(
            MapTemplateToDto(template), _localizer);
    }

    /// <inheritdoc />
    public async Task<Result<AssignmentTemplateDto>> UpdateTemplateAsync(
        long teacherId, long actingUserId, long templateId, UpdateAssignmentTemplateDto dto)
    {
        var template = await _unitOfWork.ExamHomeworkRepo
            .GetTemplateByIdAndTeacherAsync(templateId, teacherId);

        if (template is null)
            return Result<AssignmentTemplateDto>.Failure(
                _localizer, "TemplateNotFound", HttpStatusCode.NotFound);

        var inputValidation = ValidateUpdateInput(template, dto);
        if (!inputValidation.IsSuccess) return inputValidation;

        // REQ-EXH-013 lock — only when pattern is actually changing.
        bool patternChanging = dto.RecurrencePattern.HasValue
                            && dto.RecurrencePattern.Value != template.RecurrencePattern;
        if (patternChanging)
        {
            bool canEdit = await _unitOfWork.ExamHomeworkRepo
                .CanEditRecurrencePatternAsync(templateId);
            if (!canEdit)
                return Result<AssignmentTemplateDto>.Failure(
                    _localizer, "RecurrencePatternLocked", HttpStatusCode.UnprocessableEntity);
        }

        ApplyTemplateEdits(template, dto);
        template.UpdatedAt = DateTime.UtcNow;

        // Edit the only existing occurrence's DueDate when a non-recurring template's date changes.
        if (dto.AssignmentDate.HasValue && !template.IsRecurring)
        {
            var firstOccurrence = await _unitOfWork.ExamHomeworkRepo
                .GetFirstOccurrenceAsync(template.Id, teacherId);
            if (firstOccurrence is not null)
            {
                firstOccurrence.DueDate = dto.AssignmentDate.Value.Date;
                await _unitOfWork.ExamHomeworkRepo.UpdateOccurrenceAsync(firstOccurrence);
            }
        }

        await _unitOfWork.ExamHomeworkRepo.UpdateTemplateAsync(template);
        _unitOfWork.ExamHomeworkRepo.SetTemplateOriginalRowVersion(template, dto.RowVersion);

        try
        {
            await _unitOfWork.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result<AssignmentTemplateDto>.Failure(
                _localizer, "TemplateConcurrencyConflict", HttpStatusCode.Conflict);
        }

        var hydrated = await _unitOfWork.ExamHomeworkRepo
            .GetTemplateWithScopesAsync(template.Id, teacherId);
        return Result<AssignmentTemplateDto>.Success(
            MapTemplateToDto(hydrated!), _localizer, "AssignmentUpdated");
    }

    /// <inheritdoc />
    public async Task<Result<bool>> DeleteTemplateAsync(
        long teacherId, long actingUserId, long templateId, bool confirm)
    {
        if (!confirm)
            return Result<bool>.Failure(
                _localizer, "DeletionConfirmationRequired", HttpStatusCode.BadRequest);

        var template = await _unitOfWork.ExamHomeworkRepo
            .GetTemplateWithScopesAsync(templateId, teacherId);

        if (template is null)
            return Result<bool>.Failure(
                _localizer, "TemplateNotFound", HttpStatusCode.NotFound);

        // ── Compute audit context BEFORE the delete fires ──
        int studentsAffected = await _unitOfWork.ExamHomeworkRepo
            .CountStudentsWithRecordedDataAsync(templateId);
        int occurrenceCount = await _unitOfWork.ExamHomeworkRepo
            .CountOccurrencesByTemplateAsync(templateId);

        // Pull audit-log history into the snapshot before we delete it.
        var auditLogs = await _unitOfWork.ExamHomeworkRepo
            .GetAuditLogsForTemplateAsync(templateId);

        string snapshotJson = BuildDeletionSnapshotJson(
            template, studentsAffected, occurrenceCount, auditLogs);

        var deletionLog = new AssignmentDeletionLog
        {
            TemplateId = template.Id,
            TeacherId = teacherId,
            DeletionType = AssignmentDeletionType.HardDelete,
            StudentsAffected = studentsAffected,
            TemplateSnapshotJson = snapshotJson,
            DeletedByUserId = actingUserId,
            DeletedAt = DateTime.UtcNow,
            LastOccurrenceId = null, // hard delete — no surviving anchor
            CreateAt = DateTime.UtcNow,
        };

        await _unitOfWork.BeginTransactionAsync();
        try
        {
            // 1. Persist the JSON snapshot first — it's the only forensic record.
            await _unitOfWork.ExamHomeworkRepo.AddDeletionLogAsync(deletionLog);

            // 2. Delete audit logs explicitly. Their FK to the obligation is Restrict,
            //    so a cascade from template → occurrence → obligation would fail with
            //    a referential-integrity error if any audit row exists. We archive into
            //    the JSON snapshot above, then delete in bulk here.
            await _unitOfWork.ExamHomeworkRepo.DeleteAuditLogsForTemplateAsync(templateId);

            // 3. Cascade hard delete — scopes, occurrences, obligations all go.
            await _unitOfWork.ExamHomeworkRepo.DeleteTemplateAsync(template);

            await _unitOfWork.SaveChangesAsync();
            await _unitOfWork.CommitAsync();
        }
        catch
        {
            await _unitOfWork.RollbackAsync();
            throw;
        }

        return Result<bool>.Success(true, _localizer, "AssignmentDeleted", HttpStatusCode.NoContent);
    }

    /// <inheritdoc />
    public async Task<Result<StopRecurrenceResultDto>> StopRecurrenceAsync(
        long teacherId, long actingUserId, long templateId, StopRecurrenceDto dto)
    {
        var template = await _unitOfWork.ExamHomeworkRepo
            .GetTemplateByIdAndTeacherAsync(templateId, teacherId);

        if (template is null)
            return Result<StopRecurrenceResultDto>.Failure(
                _localizer, "TemplateNotFound", HttpStatusCode.NotFound);

        if (!template.IsRecurring)
            return Result<StopRecurrenceResultDto>.Failure(
                _localizer, "TemplateIsNotRecurring", HttpStatusCode.BadRequest);

        if (template.IsRecurrenceStopped)
            return Result<StopRecurrenceResultDto>.Failure(
                _localizer, "RecurrenceAlreadyStopped", HttpStatusCode.BadRequest);

        var lastOccurrence = await _unitOfWork.ExamHomeworkRepo
            .GetLatestOccurrenceAsync(templateId, teacherId);

        DateTime utcNow = DateTime.UtcNow;
        template.IsRecurrenceStopped = true;
        template.UpdatedAt = utcNow;

        var deletionLog = new AssignmentDeletionLog
        {
            TemplateId = template.Id,
            TeacherId = teacherId,
            DeletionType = AssignmentDeletionType.StopRecurrence,
            StudentsAffected = 0, // stop-recurrence destroys no data
            TemplateSnapshotJson = BuildStopRecurrenceSnapshotJson(template, lastOccurrence),
            DeletedByUserId = actingUserId,
            DeletedAt = utcNow,
            LastOccurrenceId = lastOccurrence?.Id,
            CreateAt = utcNow,
        };

        await _unitOfWork.ExamHomeworkRepo.UpdateTemplateAsync(template);
        await _unitOfWork.ExamHomeworkRepo.AddDeletionLogAsync(deletionLog);
        _unitOfWork.ExamHomeworkRepo.SetTemplateOriginalRowVersion(template, dto.RowVersion);

        try
        {
            await _unitOfWork.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result<StopRecurrenceResultDto>.Failure(
                _localizer, "TemplateConcurrencyConflict", HttpStatusCode.Conflict);
        }

        return Result<StopRecurrenceResultDto>.Success(new StopRecurrenceResultDto
        {
            TemplateId = template.Id,
            StoppedAt = utcNow,
            LastOccurrenceId = lastOccurrence?.Id,
            LastOccurrenceNumber = lastOccurrence?.OccurrenceNumber,
            LastOccurrenceDueDate = lastOccurrence?.DueDate,
        }, _localizer, "RecurrenceStopped");
    }

    /// <inheritdoc />
    public async Task<Result<PaginatedResponse<List<OccurrenceSummaryItemDto>>>> GetOccurrencesAsync(
        long teacherId, long templateId, int page, int pageSize)
    {
        // Tenant-guarded existence check — never leak existence of other tutors' templates.
        var templateExists = await _unitOfWork.ExamHomeworkRepo
            .GetTemplateByIdAndTeacherAsync(templateId, teacherId) is not null;
        if (!templateExists)
            return Result<PaginatedResponse<List<OccurrenceSummaryItemDto>>>.Failure(
                _localizer, "TemplateNotFound", HttpStatusCode.NotFound);

        var (occurrences, totalCount) = await _unitOfWork.ExamHomeworkRepo
            .GetOccurrencesByTemplatePagedAsync(teacherId, templateId, page, pageSize);

        // O(2) aggregate: occurrence-level counts in one grouped query.
        var occurrenceIds = occurrences.Select(o => o.Id).ToList();
        var summaries = await _unitOfWork.ExamHomeworkRepo
            .GetCompletionSummariesByOccurrenceIdsAsync(occurrenceIds);

        var items = occurrences.Select(o => new OccurrenceSummaryItemDto
        {
            Id = o.Id,
            OccurrenceNumber = o.OccurrenceNumber,
            DueDate = o.DueDate,
            Status = o.Status.ToString(),
            MaxGradeSnapshot = o.MaxGradeSnapshot,
            PassingThresholdSnapshot = o.PassingThresholdSnapshot,
            TrackingModeSnapshot = o.TrackingModeSnapshot,
            Totals = MapToCompletionSummaryDto(summaries.GetValueOrDefault(o.Id)),
        }).ToList();

        var response = new PaginatedResponse<List<OccurrenceSummaryItemDto>>
        {
            data = items,
            page = page,
            pageSize = pageSize,
            totalCount = totalCount,
            totalPages = (int)Math.Ceiling(totalCount / (double)pageSize),
        };
        return Result<PaginatedResponse<List<OccurrenceSummaryItemDto>>>.Success(
            response, _localizer);
    }

    // ══════════════════════════════════════════════════════════════════════
    // PHASE 3 — STUBS (will be implemented in the next delivery)
    // Scope management, tracking views, status entry, grading, scanning, audit.
    // Stubs throw to make missing wiring obvious if a controller calls them prematurely.
    // ══════════════════════════════════════════════════════════════════════

    public Task<Result<AddScopesResultDto>> AddScopesAsync(
        long teacherId, long actingUserId, long templateId, AddScopesDto dto)
        => throw new NotImplementedException("Phase 3");

    public Task<Result<bool>> RemoveScopeAsync(
        long teacherId, long actingUserId, long templateId, long scopeId)
        => throw new NotImplementedException("Phase 3");

    public Task<Result<AddStudentsResultDto>> AddStudentsToTemplateAsync(
        long teacherId, long actingUserId, long templateId, AddStudentsToTemplateDto dto)
        => throw new NotImplementedException("Phase 3");

    public Task<Result<PaginatedResponse<List<EligibleStudentDto>>>> GetEligibleStudentsAsync(
        long teacherId, long templateId, EligibleStudentsRequest request)
        => throw new NotImplementedException("Phase 3");

    public Task<Result<RemoveStudentResultDto>> RemoveStudentFromTemplateAsync(
        long teacherId, long actingUserId, long templateId, long teacherStudentId, bool force)
        => throw new NotImplementedException("Phase 3");

    public Task<Result<bool>> RemoveObligationAsync(
        long teacherId, long actingUserId, long obligationId, bool force)
        => throw new NotImplementedException("Phase 3");

    public Task<Result<OccurrenceDetailDto>> GetOccurrenceAsync(long teacherId, long occurrenceId)
        => throw new NotImplementedException("Phase 3");

    public Task<Result<OccurrenceSummaryDto>> GetOccurrenceSummaryAsync(long teacherId, long occurrenceId)
        => throw new NotImplementedException("Phase 3");

    public Task<Result<PaginatedResponse<List<TrackingViewRowDto>>>> GetTrackingViewAsync(
        long teacherId, long occurrenceId, TrackingViewRequest request)
        => throw new NotImplementedException("Phase 3");

    public Task<Result<List<StudentPickerRowDto>>> SearchStudentsInOccurrenceAsync(
        long teacherId, long occurrenceId, string query, int limit)
        => throw new NotImplementedException("Phase 3");

    public Task<Result<TrackingViewRowDto>> UpdateExamStatusAsync(
        long teacherId, long actingUserId, long occurrenceId, long teacherStudentId,
        UpdateExamStatusDto dto)
        => throw new NotImplementedException("Phase 3");

    public Task<Result<TrackingViewRowDto>> UpdateHomeworkStatusAsync(
        long teacherId, long actingUserId, long occurrenceId, long teacherStudentId,
        UpdateHomeworkStatusDto dto)
        => throw new NotImplementedException("Phase 3");

    public Task<Result<TrackingViewRowDto>> UpdateExamStatusByCodeAsync(
        long teacherId, long actingUserId, long occurrenceId, UpdateStatusByCodeDto dto)
        => throw new NotImplementedException("Phase 3");

    public Task<Result<TrackingViewRowDto>> UpdateHomeworkStatusByCodeAsync(
        long teacherId, long actingUserId, long occurrenceId, UpdateStatusByCodeDto dto)
        => throw new NotImplementedException("Phase 3");

    public Task<Result<BulkStatusResultDto>> BulkUpdateStatusAsync(
        long teacherId, long actingUserId, long occurrenceId, BulkUpdateStatusDto dto)
        => throw new NotImplementedException("Phase 3");

    public Task<Result<ScanResultDto>> ScanBarcodeAsync(
        long teacherId, long actingUserId, long occurrenceId, ScanBarcodeDto dto)
        => throw new NotImplementedException("Phase 3");

    public Task<Result<GradeEntryViewDto>> GetPendingGradesAsync(
        long teacherId, long occurrenceId, GradeEntryRequest request)
        => throw new NotImplementedException("Phase 3");

    public Task<Result<TrackingViewRowDto>> EnterGradeAsync(
        long teacherId, long actingUserId, long obligationId, EnterGradeDto dto)
        => throw new NotImplementedException("Phase 3");

    public Task<Result<PaginatedResponse<List<ObligationAuditEntryDto>>>> GetObligationAuditLogAsync(
        long teacherId, long obligationId, int page, int pageSize)
        => throw new NotImplementedException("Phase 3");

    // ══════════════════════════════════════════════════════════════════════
    // PRIVATE HELPERS — INPUT VALIDATION
    // ══════════════════════════════════════════════════════════════════════

    private Result<AssignmentTemplateDto> ValidateCreateInput(CreateAssignmentTemplateDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Name) || dto.Name.Length > 200
         || string.IsNullOrWhiteSpace(dto.NameAr) || dto.NameAr.Length > 200)
            return Failure("AssignmentNameRequired");

        if (dto.Notes is not null && dto.Notes.Length > 2000)
            return Failure("AssignmentNotesTooLong");

        if (dto.AssignmentDate.Date < DateTime.UtcNow.Date)
            return Failure("AssignmentDateInPast");

        if (dto.Scopes is null || dto.Scopes.Count == 0)
            return Failure("ScopeListEmpty");

        foreach (var scope in dto.Scopes)
        {
            if (!IsScopeShapeValid(scope))
                return Failure("InvalidScopeShape");
        }

        if (!dto.IsRecurring && dto.RecurrencePattern != RecurrencePattern.OneTime)
            return Failure("RecurrenceFlagMismatch");

        if (dto.AssignmentType == AssignmentType.Homework)
        {
            if (dto.TrackingMode is null)
                return Failure("HomeworkRequiresTrackingMode");

            if (dto.RecurrencePattern is not (RecurrencePattern.OneTime
                                           or RecurrencePattern.EverySession
                                           or RecurrencePattern.EveryTwoSessions))
                return Failure("InvalidRecurrenceForHomework");
        }
        else // Exam
        {
            if (dto.MaxGrade is null || dto.MaxGrade.Value <= 0m)
                return Failure("ExamRequiresMaxGrade");

            if (dto.PassingThreshold is null
             || dto.PassingThreshold.Value < 0m
             || dto.PassingThreshold.Value > dto.MaxGrade.Value)
                return Failure("PassingThresholdOutOfRange");

            if (dto.RecurrencePattern is not (RecurrencePattern.OneTime
                                           or RecurrencePattern.Monthly))
                return Failure("InvalidRecurrenceForExam");
        }

        return Result<AssignmentTemplateDto>.Success(default!, _localizer);
    }

    private Result<AssignmentTemplateDto> ValidateUpdateInput(
        AssignmentTemplate template, UpdateAssignmentTemplateDto dto)
    {
        if (dto.RowVersion is null || dto.RowVersion.Length == 0)
            return Failure("BadRequest");

        if (dto.Name is not null
            && (string.IsNullOrWhiteSpace(dto.Name) || dto.Name.Length > 200))
            return Failure("AssignmentNameRequired");

        if (dto.NameAr is not null
            && (string.IsNullOrWhiteSpace(dto.NameAr) || dto.NameAr.Length > 200))
            return Failure("AssignmentNameRequired");

        if (dto.Notes is not null && dto.Notes.Length > 2000)
            return Failure("AssignmentNotesTooLong");

        if (dto.AssignmentDate.HasValue)
        {
            // assignmentDate is not editable for recurring templates per §3.1 endpoint 4.
            if (template.IsRecurring)
                return Failure("BadRequest");

            if (dto.AssignmentDate.Value.Date < DateTime.UtcNow.Date)
                return Failure("AssignmentDateInPast");
        }

        if (template.AssignmentType == AssignmentType.Homework
            && dto.RecurrencePattern.HasValue
            && dto.RecurrencePattern.Value is not (RecurrencePattern.OneTime
                                                or RecurrencePattern.EverySession
                                                or RecurrencePattern.EveryTwoSessions))
            return Failure("InvalidRecurrenceForHomework");

        if (template.AssignmentType == AssignmentType.Exam
            && dto.RecurrencePattern.HasValue
            && dto.RecurrencePattern.Value is not (RecurrencePattern.OneTime
                                                or RecurrencePattern.Monthly))
            return Failure("InvalidRecurrenceForExam");

        if (template.AssignmentType == AssignmentType.Exam
            && (dto.MaxGrade.HasValue || dto.PassingThreshold.HasValue))
        {
            decimal effectiveMax = dto.MaxGrade ?? template.MaxGrade ?? 0m;
            decimal effectiveThreshold = dto.PassingThreshold ?? template.PassingThreshold ?? 0m;

            if (effectiveMax <= 0m)
                return Failure("ExamRequiresMaxGrade");

            if (effectiveThreshold < 0m || effectiveThreshold > effectiveMax)
                return Failure("PassingThresholdOutOfRange");
        }

        return Result<AssignmentTemplateDto>.Success(default!, _localizer);
    }

    private static bool IsScopeShapeValid(ScopeInputDto s) =>
        s.ScopeType switch
        {
            AssignmentScopeType.IndividualStudent =>
                s.TeacherStudentId.HasValue && !s.SessionId.HasValue && !s.SessionGroupId.HasValue,
            AssignmentScopeType.Session =>
                s.SessionId.HasValue && !s.TeacherStudentId.HasValue && !s.SessionGroupId.HasValue,
            AssignmentScopeType.SessionGroup =>
                s.SessionGroupId.HasValue && !s.TeacherStudentId.HasValue && !s.SessionId.HasValue,
            _ => false
        };

    /// <summary>
    /// Validates that every referenced scope target belongs to the JWT teacher.
    /// Returns a tuple instead of <see cref="Result{T}"/> so the caller can decide
    /// which T to wrap the failure in (it differs per endpoint).
    /// </summary>
    private async Task<(bool IsSuccess, string? MessageKey, HttpStatusCode StatusCode)>
        ValidateScopeOwnershipAsync(long teacherId, IEnumerable<ScopeInputDto> scopes)
    {
        foreach (var s in scopes)
        {
            switch (s.ScopeType)
            {
                case AssignmentScopeType.IndividualStudent:
                    var student = await _unitOfWork.Students
                        .GetActiveByIdAndTeacherAsync(s.TeacherStudentId!.Value, teacherId);
                    if (student is null)
                        return (false, "ScopeTargetNotFoundOrForeign", HttpStatusCode.BadRequest);
                    break;

                case AssignmentScopeType.Session:
                    var session = await _unitOfWork.SessionsRepo
                        .GetByIdAndTeacherAsync(s.SessionId!.Value, teacherId);
                    if (session is null)
                        return (false, "ScopeTargetNotFoundOrForeign", HttpStatusCode.BadRequest);
                    break;

                case AssignmentScopeType.SessionGroup:
                    var group = await _unitOfWork.SessionsRepo
                        .GetGroupByIdAndTeacherAsync(s.SessionGroupId!.Value, teacherId);
                    if (group is null)
                        return (false, "ScopeTargetNotFoundOrForeign", HttpStatusCode.BadRequest);
                    break;
            }
        }
        return (true, null, HttpStatusCode.OK);
    }

    // ══════════════════════════════════════════════════════════════════════
    // PRIVATE HELPERS — STUDENT RESOLUTION (REUSE existing PaymentsRepo methods)
    // ══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Resolves all scope rows to a deduplicated set of TeacherStudentIds.
    /// Reuses <c>IPaymentRepo.GetStudentIdsBySessionAsync</c> and
    /// <c>GetStudentIdsByGroupAsync</c> — single source of truth for "who's in a session".
    /// REQ-EXH-003: HashSet handles deduplication for free.
    /// </summary>
    private async Task<HashSet<long>> ResolveAndDedupeStudentsAsync(
        long teacherId, IEnumerable<ScopeInputDto> scopes)
    {
        var ids = new HashSet<long>();

        foreach (var s in scopes)
        {
            IReadOnlyList<long> resolved = s.ScopeType switch
            {
                AssignmentScopeType.IndividualStudent
                    => new[] { s.TeacherStudentId!.Value },

                AssignmentScopeType.Session
                    => await _unitOfWork.PaymentsRepo
                        .GetStudentIdsBySessionAsync(teacherId, s.SessionId!.Value),

                AssignmentScopeType.SessionGroup
                    => await _unitOfWork.PaymentsRepo
                        .GetStudentIdsByGroupAsync(teacherId, s.SessionGroupId!.Value),

                _ => Array.Empty<long>(),
            };

            foreach (var id in resolved) ids.Add(id);
        }

        return ids;
    }

    // ══════════════════════════════════════════════════════════════════════
    // PRIVATE HELPERS — ENTITY BUILDERS
    // ══════════════════════════════════════════════════════════════════════

    private static AssignmentTemplate BuildTemplateFromDto(
        CreateAssignmentTemplateDto dto, long teacherId, long actingUserId, DateTime utcNow)
        => new()
        {
            TeacherId = teacherId,
            AssignmentType = dto.AssignmentType,
            Name = dto.Name.Trim(),
            NameAr = dto.NameAr.Trim(),
            Notes = string.IsNullOrWhiteSpace(dto.Notes) ? null : dto.Notes.Trim(),
            IsRecurring = dto.IsRecurring,
            RecurrencePattern = dto.RecurrencePattern,
            RecurrenceEndDate = dto.RecurrenceEndDate?.Date,
            IsRecurrenceStopped = false,
            TrackingMode = dto.AssignmentType == AssignmentType.Homework ? dto.TrackingMode : null,
            MaxGrade = dto.AssignmentType == AssignmentType.Exam ? dto.MaxGrade : null,
            PassingThreshold = dto.AssignmentType == AssignmentType.Exam ? dto.PassingThreshold : null,
            CreatedByUserId = actingUserId,
            UpdatedAt = utcNow,
            CreateAt = utcNow,
        };

    private static List<AssignmentScope> BuildScopesFromDto(
        IEnumerable<ScopeInputDto> dtos, AssignmentTemplate template, long teacherId, DateTime utcNow)
        => dtos.Select(s => new AssignmentScope
        {
            TeacherId = teacherId,
            Template = template,
            ScopeType = s.ScopeType,
            TeacherStudentId = s.ScopeType == AssignmentScopeType.IndividualStudent ? s.TeacherStudentId : null,
            SessionId = s.ScopeType == AssignmentScopeType.Session ? s.SessionId : null,
            SessionGroupId = s.ScopeType == AssignmentScopeType.SessionGroup ? s.SessionGroupId : null,
            CreateAt = utcNow,
        }).ToList();

    private static AssignmentOccurrence BuildFirstOccurrence(
        AssignmentTemplate template, long teacherId, DateTime dueDate, DateTime utcNow)
        => new()
        {
            Template = template,
            TeacherId = teacherId,
            OccurrenceNumber = 1,
            DueDate = dueDate,
            Status = AssignmentOccurrenceStatus.Pending,
            MaxGradeSnapshot = template.MaxGrade,
            PassingThresholdSnapshot = template.PassingThreshold,
            TrackingModeSnapshot = template.TrackingMode,
            CreateAt = utcNow,
        };

    private static List<StudentAssignmentObligation> BuildObligations(
        AssignmentOccurrence occurrence, long teacherId, IEnumerable<long> studentIds, DateTime utcNow)
        => studentIds.Select(studentId => new StudentAssignmentObligation
        {
            Occurrence = occurrence,
            TeacherId = teacherId,
            TeacherStudentId = studentId,
            Status = ObligationStatus.Pending,
            IsGradeEntered = false,
            MarkedByScan = false,
            UpdatedAt = utcNow,
            CreateAt = utcNow,
        }).ToList();

    private static void ApplyTemplateEdits(AssignmentTemplate template, UpdateAssignmentTemplateDto dto)
    {
        if (dto.Name is not null) template.Name = dto.Name.Trim();
        if (dto.NameAr is not null) template.NameAr = dto.NameAr.Trim();
        if (dto.Notes is not null) template.Notes = string.IsNullOrWhiteSpace(dto.Notes) ? null : dto.Notes.Trim();
        if (dto.RecurrencePattern.HasValue) template.RecurrencePattern = dto.RecurrencePattern.Value;
        if (dto.RecurrenceEndDate.HasValue) template.RecurrenceEndDate = dto.RecurrenceEndDate.Value.Date;
        if (dto.TrackingMode.HasValue && template.AssignmentType == AssignmentType.Homework)
            template.TrackingMode = dto.TrackingMode.Value;
        if (dto.MaxGrade.HasValue && template.AssignmentType == AssignmentType.Exam)
            template.MaxGrade = dto.MaxGrade.Value;
        if (dto.PassingThreshold.HasValue && template.AssignmentType == AssignmentType.Exam)
            template.PassingThreshold = dto.PassingThreshold.Value;
    }

    // ══════════════════════════════════════════════════════════════════════
    // PRIVATE HELPERS — MAPPERS & FORMATTING
    // ══════════════════════════════════════════════════════════════════════

    private static AssignmentTemplateDto MapTemplateToDto(AssignmentTemplate t) => new()
    {
        Id = t.Id,
        AssignmentType = t.AssignmentType,
        Name = t.Name,
        NameAr = t.NameAr,
        Notes = t.Notes,
        IsRecurring = t.IsRecurring,
        RecurrencePattern = t.RecurrencePattern,
        RecurrenceEndDate = t.RecurrenceEndDate,
        IsRecurrenceStopped = t.IsRecurrenceStopped,
        TrackingMode = t.TrackingMode,
        MaxGrade = t.MaxGrade,
        PassingThreshold = t.PassingThreshold,
        CreatedAt = t.CreateAt,
        UpdatedAt = t.UpdatedAt,
        RowVersion = t.RowVersion,
        Scopes = t.Scopes.Select(MapScopeToDto).ToList(),
    };

    private static ScopeOutputDto MapScopeToDto(AssignmentScope s) => new()
    {
        Id = s.Id,
        ScopeType = s.ScopeType,
        TeacherStudentId = s.TeacherStudentId,
        StudentName = s.TeacherStudent?.StudentName,
        StudentCode = s.TeacherStudent?.StudentCode,
        SessionId = s.SessionId,
        SessionName = s.Session?.SessionName,
        SessionGroupId = s.SessionGroupId,
        SessionGroupName = s.SessionGroup?.GroupName,
    };

    private static CompletionSummaryDto MapToCompletionSummaryDto(OccurrenceCompletionSummary? src)
    {
        if (src is null) return new CompletionSummaryDto();
        return new CompletionSummaryDto
        {
            TotalStudents = src.TotalStudents,
            DoneOrAttended = src.DoneOrAttended,
            NotDoneOrAbsent = src.NotDoneOrAbsent,
            Pending = src.Pending,
        };
    }

    /// <summary>
    /// Builds the human-readable scope summary string (e.g., "3 students · 2 sessions · 1 group")
    /// from the per-template aggregate returned by the repo.
    /// </summary>
    private static string BuildScopeSummary(ScopeCountAggregate? c)
    {
        if (c is null) return "—";
        var parts = new List<string>(3);
        if (c.IndividualCount > 0)
            parts.Add($"{c.IndividualCount} {(c.IndividualCount == 1 ? "student" : "students")}");
        if (c.SessionCount > 0)
            parts.Add($"{c.SessionCount} {(c.SessionCount == 1 ? "session" : "sessions")}");
        if (c.GroupCount > 0)
            parts.Add($"{c.GroupCount} {(c.GroupCount == 1 ? "group" : "groups")}");
        return parts.Count == 0 ? "—" : string.Join(" · ", parts);
    }

    private static string BuildDeletionSnapshotJson(
        AssignmentTemplate template,
        int studentsAffected,
        int occurrenceCount,
        IReadOnlyList<StudentObligationAuditLog> auditLogs)
    {
        bool perStudentDetailIncluded = studentsAffected <= DeletionSnapshotPerStudentCap;

        // Audit logs are archived into the snapshot before being bulk-deleted in the same
        // transaction (their FK to obligations is Restrict). This way the audit trail
        // survives the hard delete inside this single JSON column.
        var auditSummary = auditLogs.Select(a => new
        {
            a.Id,
            a.StudentObligationId,
            a.OldStatus,
            a.NewStatus,
            a.OldGradeValue,
            a.NewGradeValue,
            a.MaxGradeSnapshot,
            a.PassingThresholdSnapshot,
            a.ChangeReason,
            a.ChangedByUserId,
            a.ChangedAt,
        }).ToList();

        var snapshot = new
        {
            template.Id,
            template.AssignmentType,
            template.Name,
            template.NameAr,
            template.Notes,
            template.IsRecurring,
            template.RecurrencePattern,
            template.RecurrenceEndDate,
            template.MaxGrade,
            template.PassingThreshold,
            template.TrackingMode,
            template.CreatedByUserId,
            template.CreateAt,
            Scopes = template.Scopes.Select(MapScopeToDto).ToList(),
            OccurrenceCount = occurrenceCount,
            StudentsAffected = studentsAffected,
            PerStudentDetailIncluded = perStudentDetailIncluded,
            AuditLogCount = auditLogs.Count,
            AuditLogs = auditSummary,
            CapturedAt = DateTime.UtcNow,
        };
        return JsonSerializer.Serialize(snapshot);
    }

    private static string BuildStopRecurrenceSnapshotJson(
        AssignmentTemplate template, AssignmentOccurrence? lastOccurrence)
    {
        var snapshot = new
        {
            template.Id,
            template.Name,
            template.NameAr,
            template.RecurrencePattern,
            LastOccurrenceId = lastOccurrence?.Id,
            LastOccurrenceNumber = lastOccurrence?.OccurrenceNumber,
            LastOccurrenceDueDate = lastOccurrence?.DueDate,
            CapturedAt = DateTime.UtcNow,
        };
        return JsonSerializer.Serialize(snapshot);
    }

    private Result<AssignmentTemplateDto> Failure(string key) =>
        Result<AssignmentTemplateDto>.Failure(_localizer, key, HttpStatusCode.BadRequest);
}
