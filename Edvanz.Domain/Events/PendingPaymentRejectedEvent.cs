namespace Edvanz.Domain.Events;

/// <summary>
/// Emitted when a PendingSubscriptionPayment is rejected — either by super admin
/// (manual rejection with reason) or by Paymob webhook (provider-side failure).
///
/// Dispatched AFTER the rejection transaction commits.
/// Handlers registered in DI:
///   - SendPaymentRejectedPushHandler       → Firebase push (informs teacher)
///   - SendPaymentRejectedWhatsAppHandler   → Messaging module (optional — TBD)
///   - RecordRejectionInNotificationHistoryHandler → UserNotification row
///
/// NOTE: This event does NOT invalidate the subscription cache because a rejected
/// pending payment never altered the teacher's current subscription state.
/// </summary>
public sealed class PendingPaymentRejectedEvent : IDomainEvent
{
    /// <summary>
    /// Constructs the event. Caller passes the UTC timestamp that aligns with the
    /// rejection transaction's commit moment (typically the same UtcNow captured at
    /// transaction start, mirroring the C-4 discipline applied to renewals).
    /// </summary>
    public PendingPaymentRejectedEvent(
        long teacherId,
        long pendingPaymentId,
        string reason,
        DateTime occurredAtUtc)
    {
        TeacherId = teacherId;
        PendingPaymentId = pendingPaymentId;
        Reason = reason;
        OccurredAtUtc = occurredAtUtc;
    }

    /// <summary>
    /// The teacher whose payment was rejected.
    /// </summary>
    public long TeacherId { get; }

    /// <summary>
    /// The PendingSubscriptionPayment.Id that was rejected.
    /// </summary>
    public long PendingPaymentId { get; }

    /// <summary>
    /// Rejection reason — shown to the teacher verbatim in the push notification.
    /// Must be localized before reaching this event (handlers forward as-is).
    /// </summary>
    public string Reason { get; }

    /// <inheritdoc />
    public DateTime OccurredAtUtc { get; }
}
