using Edvanz.Application.IservicesContract;
using Edvanz.Application.ServiceContract;
using Edvanz.Domain.Constants;
using Hangfire;
using Microsoft.Extensions.Logging;

namespace Edvanz.Application.Services;

/// <summary>
/// Per-teacher reminder worker (§7.3). Implements ISubscriptionReminderJob —
/// thin Hangfire entry point that delegates to ISubscriptionReminderService.
///
/// IDEMPOTENCY:
///   The service-side check (HasAlertBeenSentAsync) plus the unique index on
///   SubscriptionAlerts (TeacherId, SubscriptionEndDate.Date, AlertDay) make
///   re-deliveries safe. Hangfire's automatic retries (3 attempts on
///   AutomaticRetry default) won't produce duplicate notifications.
///
/// HANGFIRE QUEUE:
///   [Queue("notifications")] is on the INTERFACE method (Phase 04). Hangfire
///   reads the attribute at enqueue time, so this implementation doesn't need
///   to repeat it.
/// </summary>
public class SubscriptionReminderJob : ISubscriptionReminderJob
{
    private readonly ISubscriptionReminderService _reminderService;
    private readonly ILogger<SubscriptionReminderJob> _logger;

    public SubscriptionReminderJob(
        ISubscriptionReminderService reminderService,
        ILogger<SubscriptionReminderJob> logger)
    {
        _reminderService = reminderService;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task SendAsync(long teacherId, DateTime endDate, int alertDay)
    {
        var result = await _reminderService.SendReminderAsync(teacherId, endDate, alertDay);

        if (!result.IsSuccess)
        {
            // Throw so Hangfire records the failure and retries (3 attempts default).
            // The service is idempotent on already-sent alerts, so retries are safe.
            _logger.LogError(
                "Reminder worker failed for teacher {TeacherId} day {AlertDay}: {Message}",
                teacherId, alertDay, result.Message);

            throw new InvalidOperationException(
                $"Reminder worker failed for teacher {teacherId}: {result.Message}");
        }
    }
}