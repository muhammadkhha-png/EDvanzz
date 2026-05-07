using Edvanz.Application.ServiceContract;
using Edvanz.Application.Services;
using Hangfire;
using Microsoft.Extensions.Logging;

namespace Edvanz.Infrastructure.BackGroundJobs;

// ════════════════════════════════════════════════════════════════════════════
// DISPATCHER — runs daily, fans out per-template work onto the queue
// ════════════════════════════════════════════════════════════════════════════

/// <summary>
/// Daily recurring-assignment materializer dispatcher (catalog §7.2).
///
/// SCHEDULE: Hangfire recurring job, fired by the cron registered in
/// Program.cs. Default 06:00 Africa/Cairo — early enough that the next-day's
/// occurrences are visible by the time tutors open the app.
///
/// FAN-OUT MODEL:
/// 1. Query templates whose next occurrence falls within the lookahead window.
/// 2. Enqueue ONE worker job per template on the "assignment-materialization"
///    queue. The worker holds the per-template DB-row lock — fan-out keeps the
///    dispatcher fast even at scale.
///
/// This mirrors <c>SubscriptionReminderDispatcherJob</c>'s pattern.
/// </summary>
public class RecurringAssignmentDispatcherJob
{
    private readonly IRecurringAssignmentMaterializerService _materializer;
    private readonly IBackgroundJobClient _backgroundJobs;
    private readonly ILogger<RecurringAssignmentDispatcherJob> _logger;

    public RecurringAssignmentDispatcherJob(
        IRecurringAssignmentMaterializerService materializer,
        IBackgroundJobClient backgroundJobs,
        ILogger<RecurringAssignmentDispatcherJob> logger)
    {
        _materializer = materializer;
        _backgroundJobs = backgroundJobs;
        _logger = logger;
    }

    /// <summary>
    /// Hangfire entry point. Registered as a recurring job in Program.cs.
    /// </summary>
    public async Task RunAsync()
    {
        // Lookahead 1 day: tomorrow's occurrences are materialized today. Adjusts
        // for tutors who open the app at midnight expecting the next day's row to
        // be visible. The window is small to limit the number of in-flight jobs;
        // longer windows would queue up too many materializer workers at once.
        const int LookaheadDays = 1;

        var result = await _materializer.GetTemplateIdsDueForMaterializationAsync(
            DateTime.UtcNow, LookaheadDays);

        if (!result.IsSuccess || result.Data is null)
        {
            _logger.LogError(
                "Materializer dispatcher: eligibility scan failed — {Message}", result.Message);
            return;
        }

        if (result.Data.Count == 0)
        {
            _logger.LogDebug("Materializer dispatcher: no templates due today.");
            return;
        }

        int enqueued = 0;
        foreach (long templateId in result.Data)
        {
            // One worker per template. The worker is idempotent (per-template row
            // lock + unique index on (TemplateId, OccurrenceNumber)) so a re-run
            // of the dispatcher is safe.
            _backgroundJobs.Enqueue<IRecurringAssignmentMaterializerJob>(
                job => job.MaterializeAsync(templateId));
            enqueued++;
        }

        _logger.LogInformation(
            "Materializer dispatcher: enqueued {Count} per-template materialization workers.",
            enqueued);
    }
}

// ════════════════════════════════════════════════════════════════════════════
// WORKER INTERFACE — declared in Application so DI binds it cleanly
// ════════════════════════════════════════════════════════════════════════════

/// <summary>
/// Per-template materialization worker. Hangfire's job activator instantiates
/// this through DI so each invocation gets a fresh DbContext scope.
///
/// Mirrors the <c>ISubscriptionReminderJob</c> / <c>IMessageSenderJob</c> pattern.
/// </summary>
public interface IRecurringAssignmentMaterializerJob
{
    /// <summary>
    /// Materializes the next occurrence for one template.
    /// Hangfire retries on exception (3 attempts default, exponential backoff).
    /// The underlying service is idempotent so retries are safe.
    /// </summary>
    [Queue("assignment-materialization")]
    Task MaterializeAsync(long templateId);
}

// ════════════════════════════════════════════════════════════════════════════
// WORKER IMPLEMENTATION — thin Hangfire wrapper around the service
// ════════════════════════════════════════════════════════════════════════════

/// <summary>
/// Default implementation of <see cref="IRecurringAssignmentMaterializerJob"/>.
/// All real logic lives in <c>RecurringAssignmentMaterializerService</c>; this
/// wrapper only exists so Hangfire can serialize a job invocation against an
/// interface (Hangfire's recommendation for testability).
/// </summary>
public class RecurringAssignmentMaterializerJob : IRecurringAssignmentMaterializerJob
{
    private readonly IRecurringAssignmentMaterializerService _service;
    private readonly ILogger<RecurringAssignmentMaterializerJob> _logger;

    public RecurringAssignmentMaterializerJob(
        IRecurringAssignmentMaterializerService service,
        ILogger<RecurringAssignmentMaterializerJob> logger)
    {
        _service = service;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task MaterializeAsync(long templateId)
    {
        var result = await _service.MaterializeNextOccurrenceAsync(templateId);

        if (!result.IsSuccess)
        {
            // Throw so Hangfire records the failure and retries (3 attempts default).
            // Service-side idempotency makes retries safe.
            _logger.LogError(
                "Materializer worker failed for template {TemplateId}: {Message}",
                templateId, result.Message);

            throw new InvalidOperationException(
                $"Materializer worker failed for template {templateId}: {result.Message}");
        }

        // Success — outcome already logged inside the service.
    }
}
