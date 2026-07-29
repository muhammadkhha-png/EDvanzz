using Edvanz.Application.Dtos.Notifications;
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
    /// The <paramref name="payload"/> carries the category discriminator and the
    /// structured deep link; the adapter emits them as the data["type"] and
    /// data["deepLink"] FCM keys respectively.
    /// Polly rate-limit and timeout policies are applied at the call site
    /// (SendSubscriptionReminderJob — §7.4), not inside this method.
    /// </summary>
    Task<PushNotificationSendResult> SendAsync(
        string fcmToken, string title, string body, PushPayload payload);

    /// <summary>
    /// Sends a single push to multiple device tokens belonging to the SAME recipient
    /// (one Badge/Category/Screen is correct for all of them). Uses FCM's
    /// SendEachForMulticastAsync — one FCM call for up to 500 tokens instead of N
    /// sequential SendAsync round-trips.
    /// Returned list is index-aligned with <paramref name="fcmTokens"/>.
    /// Polly rate-limit/timeout policies are applied at the call site, same as SendAsync.
    /// </summary>
    Task<IReadOnlyList<TokenSendResult>> SendMulticastAsync(
        IReadOnlyList<string> fcmTokens, string title, string body, PushPayload payload);
}
