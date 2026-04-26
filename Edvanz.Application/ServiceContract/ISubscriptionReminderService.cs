using Edvanz.Application.Dtos;

namespace Edvanz.Application.ServiceContract;

/// <summary>
/// Subscription reminder dispatcher + per-teacher worker (§7).
/// Two methods, one for each level of the C-7 dispatcher pattern:
///
///   RunDispatcherAsync — invoked by SubscriptionReminderDispatcherJob (Hangfire
///   recurring, daily 09:00 Africa/Cairo). Scans teachers with EndDate in the
///   D-5..D-0 window and fans out one ISubscriptionReminderJob per teacher.
///
///   SendReminderAsync — invoked by ISubscriptionReminderJob (per-teacher worker
///   on the "notifications" queue). Performs idempotency check, sends Push
///   (rate-limited via Polly), sends WhatsApp (rate-limited via Polly),
///   inserts UserNotification + SubscriptionAlert rows.
/// </summary>
public interface ISubscriptionReminderService
{
    /// <summary>
    /// Dispatcher entry point (§7.2). Cheap query + queue push only — returns in seconds.
    /// </summary>
    Task<Result<int>> RunDispatcherAsync();

    /// <summary>
    /// Per-teacher worker (§7.3). Idempotent — if SubscriptionAlert already exists
    /// for (teacherId, endDate.Date, alertDay), returns Success without resending.
    /// </summary>
    /// <param name="teacherId">Target teacher.</param>
    /// <param name="endDate">The subscription EndDate at dispatch time.</param>
    /// <param name="alertDay">0..5 days-until-expiry bucket.</param>
    Task<Result<bool>> SendReminderAsync(long teacherId, DateTime endDate, int alertDay);
}