using Edvanz.Domain.Entities.ShareProp;
using Edvanz.Domain.Enums;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Edvanz.Domain.Entities;

/// <summary>
/// Represents a push notification persisted for a user's in-app notification inbox
/// (Facebook-style bell-icon feed). v1 scope: populated only for teachers (Q2.1=B).
///
/// IMPORTANT SCOPE BOUNDARY (D-05):
///   - PUSH notifications → persisted here.
///   - WhatsApp messages  → NOT persisted here. They use the existing Messaging module's
///                          MessageLog table (no duplicate logging).
///
/// Supports read/unread tracking (Q11=C) — IsRead + ReadAt enable per-notification state
/// plus a fast unread-count query via IX_UserNotifications_UserId_IsRead_SentAt (M-6).
/// </summary>
public class UserNotification : BaseEntity
{
    /// <summary>
    /// Foreign key to the User who owns this notification (the recipient).
    /// For subscription reminders, this is the teacher's User.Id.
    /// </summary>
    [ForeignKey(nameof(User))]
    public long UserId { get; set; }

    public User User { get; set; } = null!;

    /// <summary>
    /// Short notification title. Localized at send time per the user's LanguagePreference.
    /// </summary>
    [MaxLength(200)]
    public string Title { get; set; } = null!;

    /// <summary>
    /// Notification body. Localized at send time. Frozen — language change later in the
    /// conversation does not re-render historical notifications (EC-15).
    /// </summary>
    [MaxLength(1000)]
    public string Body { get; set; } = null!;

    /// <summary>
    /// Optional JSON payload for frontend deep-linking. E.g. { "screen": "renewal" }.
    /// The Flutter app parses this to route the user when they tap the notification.
    /// </summary>
    [MaxLength(500)]
    public string? DeepLinkPayload { get; set; }

    /// <summary>
    /// UTC timestamp when the notification was sent.
    /// </summary>
    public DateTime SentAt { get; set; }

    /// <summary>
    /// Whether the user has viewed/acknowledged this notification.
    /// Flipped via POST /api/notifications/{id}/mark-read or mark-all-read.
    /// </summary>
    public bool IsRead { get; set; } = false;

    /// <summary>
    /// UTC timestamp when the user marked this notification as read.
    /// Null until read.
    /// </summary>
    public DateTime? ReadAt { get; set; }

    /// <summary>
    /// Notification category. v1 has only SubscriptionReminder but the enum is extensible
    /// for future notification types without breaking the schema.
    /// No index on this column for v1 (M-3 — add only when a filter query exists).
    /// </summary>
    public NotificationCategory Category { get; set; } = NotificationCategory.notifiction;
}
