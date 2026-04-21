using Edvanz.Domain.Enums;

namespace Edvanz.Domain.Events;

/// <summary>
/// Emitted when a subscription renewal is successfully confirmed — whether via
/// Paymob webhook (auto-confirm) or super admin approval of a pending manual payment.
///
/// Dispatched AFTER the confirmation transaction commits (REQ-SUB-019).
/// Handlers registered in DI:
///   - InvalidateSubscriptionCacheHandler  → Redis cache removal
///   - SendRenewalConfirmationPushHandler  → Firebase push (REQ-SUB-020)
///   - SendRenewalConfirmationWhatsAppHandler → Messaging module (REQ-SUB-020)
///   - RecordRenewalInNotificationHistoryHandler → UserNotification row
///
/// Adding a new side effect (e.g., email, analytics event) means writing a new
/// handler class. SubscriptionService.ConfirmPaymentAsync is NOT modified.
/// </summary>
public sealed class SubscriptionRenewedEvent : IDomainEvent
{
    /// <summary>
    /// Constructs the event. IMPORTANT (C-4): callers MUST pass the same
    /// paymentConfirmedAt UTC value that was used as the transaction's authoritative
    /// "now". The event's OccurredAtUtc must NOT drift from the row's PaymentConfirmedAt.
    /// </summary>
    public SubscriptionRenewedEvent(
        long teacherId,
        long subscriptionId,
        DateTime startDate,
        DateTime endDate,
        decimal amountPaidEGP,
        PaymentChannel paymentChannel,
        DateTime occurredAtUtc)
    {
        TeacherId = teacherId;
        SubscriptionId = subscriptionId;
        StartDate = startDate;
        EndDate = endDate;
        AmountPaidEGP = amountPaidEGP;
        PaymentChannel = paymentChannel;
        OccurredAtUtc = occurredAtUtc;
    }

    /// <summary>
    /// The teacher whose subscription was renewed.
    /// </summary>
    public long TeacherId { get; }

    /// <summary>
    /// The newly-created TeacherSubscription.Id (IsCurrent = true row).
    /// </summary>
    public long SubscriptionId { get; }

    /// <summary>
    /// New period start date (UTC).
    /// </summary>
    public DateTime StartDate { get; }

    /// <summary>
    /// New period end date (UTC).
    /// </summary>
    public DateTime EndDate { get; }

    /// <summary>
    /// Amount paid in EGP.
    /// </summary>
    public decimal AmountPaidEGP { get; }

    /// <summary>
    /// How the payment was processed — useful for handlers that format
    /// the confirmation message differently for each channel.
    /// </summary>
    public PaymentChannel PaymentChannel { get; }

    /// <inheritdoc />
    public DateTime OccurredAtUtc { get; }
}
