using Hangfire;

namespace Edvanz.Application.IservicesContract;

/// <summary>
/// Hangfire job triggered by AdminSubscriptionService.RejectPendingAsync.
/// Sends Push + WhatsApp + inserts a UserNotification row carrying the rejection
/// reason (REQ-SUB-007 pattern).
///
/// Separate from IRenewalNotificationJob because:
///   - Different localization keys (PaymentRejectedTitle / PaymentRejectedBody).
///   - Different deep-link payload (routes to renewal screen with manual flow).
///   - Different audit trail in UserNotification.
/// </summary>
public interface IPendingPaymentRejectedNotificationJob
{
    /// <summary>
    /// Dispatches the rejection notification across all channels.
    /// </summary>
    /// <param name="teacherId">The teacher whose pending payment was rejected.</param>
    /// <param name="pendingPaymentId">The PendingSubscriptionPayment row id.</param>
    /// <param name="rejectionReason">The reason captured by the admin.</param>
    [Queue("notifications")]
    Task SendAsync(long teacherId, long pendingPaymentId, string rejectionReason);
}