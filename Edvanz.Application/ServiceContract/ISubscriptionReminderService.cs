using Edvanz.Application.Dtos;
using Edvanz.Domain.Interfaces;

namespace Edvanz.Application.ServiceContract;

/// <summary>
/// Subscription reminder dispatcher + per-teacher worker (§7).
/// Two methods, one for each level of the C-7 dispatcher pattern:
///
///   GetTeachersDueForReminderAsync — invoked by SubscriptionReminderDispatcherJob
///   (Hangfire recurring, daily 09:00 Africa/Cairo). Returns the eligibility list
///   so the JOB performs the fan-out (one ISubscriptionReminderJob per teacher).
///   This service does NOT touch Hangfire — keeps the Application layer free of
///   job-framework coupling.
///
///   SendReminderAsync — invoked by ISubscriptionReminderJob (per-teacher worker
///   on the "notifications" queue). Performs idempotency check, sends Push,
///   sends WhatsApp (pessimistic-false until v2), inserts UserNotification +
///   SubscriptionAlert rows.
/// </summary>
public interface ISubscriptionReminderService
{
    /// <summary>
    /// Dispatcher eligibility scan (§7.2). Returns the list of teachers whose
    /// CURRENT subscription EndDate falls within the D-5..D-0 alert window.
    /// The CALLER (the Hangfire dispatcher job) is responsible for enqueueing
    /// one ISubscriptionReminderJob per returned row.
    /// </summary>
    Task<Result<IReadOnlyList<UpcomingExpiryProjection>>> GetTeachersDueForReminderAsync();

    /// <summary>
    /// Per-teacher worker (§7.3). Idempotent — if SubscriptionAlert already exists
    /// for (teacherId, endDate.Date, alertDay), returns Success without resending.
    /// </summary>
    /// <param name="teacherId">Target teacher.</param>
    /// <param name="endDate">The subscription EndDate at dispatch time — part of the idempotency key.</param>
    /// <param name="alertDay">0..5 days-until-expiry bucket.</param>
    Task<Result<bool>> SendReminderAsync(long teacherId, DateTime endDate, int alertDay);
}