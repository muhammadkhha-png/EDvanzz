using Edvanz.Domain.Entities;
using Edvanz.Domain.Enums;

namespace Edvanz.Domain.Interfaces;

/// <summary>
/// Repository for the SubscriptionAlerts idempotency log (§5.3 / FR-SUB-014).
/// Each alert (TeacherId, SubscriptionEndDate, AlertDay) is recorded at most once,
/// enforced by the unique index IX_SubscriptionAlerts_Key.
///
/// The per-teacher reminder worker uses HasAlertBeenSentAsync as a cheap pre-check;
/// the unique index is the hard guarantee against concurrent INSERTs.
/// </summary>
public interface ISubscriptionAlertRepo : IGenericRepo<SubscriptionAlert, long>
{
    /// <summary>
    /// Cheap pre-check before doing the channel sends (push + WhatsApp).
    /// Returns true if a SubscriptionAlert row already exists for the
    /// (TeacherId, SubscriptionEndDate.Date, AlertDay) tuple.
    ///
    /// SubscriptionEndDate is compared by DATE only (the column is mapped as `date`).
    /// The unique index on the same key is the source of truth — this method just
    /// avoids unnecessary channel work when an obvious duplicate is detected.
    /// </summary>
    Task<bool> HasAlertBeenSentAsync(long teacherId, DateTime subscriptionEndDate, int alertDay);

    /// <summary>
    /// Inserts a SubscriptionAlert idempotency record after the worker finishes
    /// (or attempts) channel sends. ChannelsSent reflects ONLY successes — None
    /// is a valid value when both channels failed and is recorded so the dispatcher
    /// does not re-fan-out on the next run (EC-14).
    ///
    /// SaveChanges is NOT called here — the worker owns the unit-of-work boundary
    /// so it can catch unique-key violations and exit gracefully.
    /// </summary>
    Task InsertAlertAsync(SubscriptionAlert alert);
}