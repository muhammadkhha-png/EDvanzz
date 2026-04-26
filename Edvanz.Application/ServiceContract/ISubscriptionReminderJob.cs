using Hangfire;

namespace Edvanz.Application.IservicesContract;

/// <summary>
/// Per-teacher reminder worker (§7.1 / §7.3).
/// Enqueued by SubscriptionReminderDispatcherJob — one job per teacher per
/// alert day in the D-5..D-0 window. Many run in parallel on the
/// "notifications" worker pool (5 workers per HangfireOptions).
///
/// Idempotency is enforced by the unique index on SubscriptionAlerts
/// (TeacherId, SubscriptionEndDate, AlertDay). Concurrent enqueues for the
/// same alert do not produce duplicate notifications — the second worker
/// sees alreadySent and exits.
///
/// Hangfire retries (3 attempts, exponential backoff 30s / 2m / 5m).
/// </summary>
public interface ISubscriptionReminderJob
{
    /// <summary>
    /// Sends a single subscription reminder (push + WhatsApp + UserNotification).
    /// </summary>
    /// <param name="teacherId">Target teacher.</param>
    /// <param name="endDate">The subscription EndDate at dispatch time — part of the idempotency key.</param>
    /// <param name="alertDay">0..5 days-until-expiry bucket.</param>
    [Queue("notifications")]
    Task SendAsync(long teacherId, DateTime endDate, int alertDay);
}