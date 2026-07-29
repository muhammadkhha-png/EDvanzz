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
    msg,notifiction
}
