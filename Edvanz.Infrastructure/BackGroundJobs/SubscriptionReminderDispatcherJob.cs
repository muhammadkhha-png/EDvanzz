using Edvanz.Application.IservicesContract;
using Edvanz.Application.ServiceContract;
using Edvanz.Domain.Constants;
using Hangfire;
using Microsoft.Extensions.Logging;

namespace Edvanz.Infrastructure.BackGroundJobs;

/// <summary>
/// Daily reminder dispatcher (§7.2). Hangfire recurring job triggered by the
/// cron registered in Program.cs (default 09:00 Africa/Cairo via
/// ReminderSchedulerOptions).
///
/// RESPONSIBILITY:
///   - Call ISubscriptionReminderService.GetTeachersDueForReminderAsync to scan
///     for teachers in the D-5..D-0 alert window (cheap query against the
///     filtered unique index).
///   - For each result, ENQUEUE one ISubscriptionReminderJob on the
///     "notifications" queue. The actual send work happens in the worker.
///
/// SEPARATION:
///   This class is the ONLY place outside the Hangfire dashboard that calls
///   IBackgroundJobClient for reminder fan-out. Per the Phase 06 directive, the
///   Application service layer does NOT touch Hangfire — it returns the
///   eligibility list and lets this job perform the queue push.
///
/// FAILURE MODES:
///   - If GetTeachersDueForReminderAsync returns Failure, log and exit. No retry
///     would help; the next day's run will catch up.
///   - If individual Enqueue calls throw, Hangfire's recurring-job retry policy
///     re-runs the WHOLE dispatcher. The per-teacher worker has its own
///     idempotency (SubscriptionAlerts unique index), so duplicate enqueues
///     don't produce duplicate notifications.
/// </summary>
public class SubscriptionReminderDispatcherJob
{
    private readonly ISubscriptionReminderService _reminderService;
    private readonly IBackgroundJobClient _backgroundJobs;
    private readonly ILogger<SubscriptionReminderDispatcherJob> _logger;

    public SubscriptionReminderDispatcherJob(
        ISubscriptionReminderService reminderService,
        IBackgroundJobClient backgroundJobs,
        ILogger<SubscriptionReminderDispatcherJob> logger)
    {
        _reminderService = reminderService;
        _backgroundJobs = backgroundJobs;
        _logger = logger;
    }

    /// <summary>
    /// Hangfire entry point. Registered as a recurring job in Program.cs.
    /// </summary>
    public async Task RunAsync()
    {
        var result = await _reminderService.GetTeachersDueForReminderAsync();

        if (!result.IsSuccess || result.Data is null)
        {
            _logger.LogError(
                "Reminder dispatcher: eligibility scan failed — {Message}", result.Message);
            return;
        }

        int enqueued = 0;
        foreach (var teacher in result.Data)
        {
            // One worker per (teacher, alert day). The worker idempotency check
            // makes duplicate enqueues from a re-run of the dispatcher safe.
            _backgroundJobs.Enqueue<ISubscriptionReminderJob>(
                job => job.SendAsync(teacher.TeacherId, teacher.SubscriptionEndDate, teacher.DaysUntilExpiry));
            enqueued++;
        }

        _logger.LogInformation(
            "Reminder dispatcher: enqueued {Count} per-teacher worker jobs", enqueued);
    }
}