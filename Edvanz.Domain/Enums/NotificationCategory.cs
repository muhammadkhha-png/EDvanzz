using System.Text.Json.Serialization;

namespace Edvanz.Domain.Enums;

/// <summary>
/// Categorizes UserNotification records by source.
/// v1 scope: only SubscriptionReminder. Extensible by adding values for future
/// notification types (e.g., AttendanceAlert, PaymentOverdue).
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum NotificationCategory : byte
{
    /// <summary>
    /// Subscription expiry reminder or renewal confirmation/rejection.
    /// REQ-SUB-005, REQ-SUB-020.
    /// </summary>
    SubscriptionReminder = 1,
    RenewalConfirmed=2,
    PaymentRejected=3,

    /// <summary>
    /// Student-teacher link request lifecycle: new request received (teacher),
    /// request accepted/rejected or link removed by the teacher (student).
    /// </summary>
    LinkRequest = 4
}
