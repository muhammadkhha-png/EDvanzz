using Edvanz.Application.Dtos.Subscription;

namespace Edvanz.Application.IservicesContract;

/// <summary>
/// Abstraction over Firebase Cloud Messaging.
/// Wraps the FirebaseAdmin SDK so the Application layer never references it directly.
///
/// REQ-SUB-006: in-app push channel for subscription reminders and renewal notifications.
/// EC-11: when Firebase reports "registration-token-not-registered", the sender
/// returns a result with ShouldDeactivateToken = true and the caller flips
/// UserDeviceToken.IsActive = false via IUserDeviceTokenRepo.DeactivateTokenAsync.
/// </summary>
public interface IPushNotificationSender
{
    /// <summary>
    /// Sends a single push notification to one device token.
    /// Polly rate-limit and timeout policies are applied at the call site
    /// (SendSubscriptionReminderJob — §7.4), not inside this method.
    /// </summary>
    Task<PushNotificationSendResult> SendAsync(
        string fcmToken, string title, string body, string? deepLinkPayload);
}