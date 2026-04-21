using System.Text.Json.Serialization;

namespace Edvanz.Domain.Enums;

/// <summary>
/// Lifecycle states for a PendingSubscriptionPayment record.
/// A pending payment NEVER grants access — only a Confirmed pending produces a TeacherSubscription row
/// (BR-SUB-008).
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum PendingPaymentStatus : byte
{
    /// <summary>
    /// Teacher initiated the renewal flow. Awaiting either Paymob webhook callback
    /// or manual transaction-reference submission.
    /// </summary>
    Initiated = 1,

    /// <summary>
    /// Teacher submitted the manual transaction reference. Awaiting super admin review.
    /// </summary>
    AwaitingSuperAdminApproval = 2,

    /// <summary>
    /// Payment confirmed — a TeacherSubscription row has been created for this pending payment.
    /// Terminal state.
    /// </summary>
    Confirmed = 3,

    /// <summary>
    /// Payment rejected by super admin (or by Paymob webhook with failure).
    /// Terminal state.
    /// </summary>
    Rejected = 4,

    /// <summary>
    /// Pending payment auto-expired after 24 hours in Initiated state without tutor submission.
    /// Terminal state. Hangfire PendingPaymentExpiryJob handles this transition.
    /// </summary>
    Expired = 5
}
