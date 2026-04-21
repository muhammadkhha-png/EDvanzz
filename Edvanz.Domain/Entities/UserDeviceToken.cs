using Edvanz.Domain.Entities.ShareProp;
using Edvanz.Domain.Enums;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Edvanz.Domain.Entities;

/// <summary>
/// Firebase Cloud Messaging (FCM) device-token registration for a user.
/// Decoupled from RefreshTokens (D-06) so push notifications can reach a teacher
/// even while they are logged out — critical for pre-expiry reminders
/// (REQ-SUB-005 requires alerts regardless of login state).
///
/// Multiple rows per user are supported: one token per device. A teacher may have
/// phone + tablet registered simultaneously. The reminder scanner fans out a push
/// to every IsActive token but creates a single UserNotification row (EC-12).
///
/// Lifecycle:
///   - Flutter calls POST /api/notifications/register-fcm-token on login and on
///     Firebase token-refresh events. Upsert on (UserId, FcmToken).
///   - On Firebase "registration-token-not-registered" error during a send,
///     IsActive is flipped to false. Future scans skip this row.
///   - Cleanup of IsActive = false rows older than 90 days is out of scope for v1
///     but the index IX_UserDeviceTokens_UserId_IsActive supports the future job.
/// </summary>
public class UserDeviceToken : BaseEntity
{
    /// <summary>
    /// Foreign key to the User who owns this device.
    /// </summary>
    [ForeignKey(nameof(User))]
    public long UserId { get; set; }

    public User User { get; set; } = null!;

    /// <summary>
    /// The Firebase-generated FCM token identifying this specific app installation
    /// on this specific device. Required payload for IPushNotificationSender.SendAsync.
    /// Tokens are long strings (typically 150-250 chars) — reserved 500 to be safe.
    /// </summary>
    [MaxLength(500)]
    public string FcmToken { get; set; } = null!;

    /// <summary>
    /// The platform this token belongs to: Android or iOS.
    /// </summary>
    public DevicePlatform Platform { get; set; }

    /// <summary>
    /// UTC timestamp when the token was first registered with the server.
    /// </summary>
    public DateTime RegisteredAt { get; set; }

    /// <summary>
    /// UTC timestamp of the last activity on this token (login, token refresh).
    /// Used by the eventual 90-day-stale cleanup job.
    /// </summary>
    public DateTime LastSeenAt { get; set; }

    /// <summary>
    /// Whether this token is considered valid for sending.
    /// Flipped to false when Firebase reports the token as unregistered (user
    /// uninstalled the app, device was reset, etc.).
    /// </summary>
    public bool IsActive { get; set; } = true;
}
