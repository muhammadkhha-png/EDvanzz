using Hangfire;

namespace Edvanz.Application.IservicesContract;

/// <summary>
/// Hangfire job triggered by SubscriptionService.ConfirmPaymentAsync after a
/// successful renewal commit (§5.9 / I-2). Sends:
///   1. Firebase Push to every IsActive UserDeviceToken
///   2. WhatsApp via the Messaging module
///   3. Inserts a UserNotification row (push record only — D-05)
///
/// Hangfire retries automatically (3 attempts, exponential backoff).
/// Failures after retries are logged — the subscription is still active,
/// the teacher just didn't get the confirmation notification.
///
/// Mirrors the existing IMessageSenderJob pattern in the codebase.
/// </summary>
public interface IRenewalNotificationJob
{
    /// <summary>
    /// Dispatches the renewal-confirmation notification across all channels.
    /// </summary>
    /// <param name="teacherId">The teacher whose subscription was renewed.</param>
    /// <param name="subscriptionId">The new TeacherSubscription row id.</param>
    [Queue("notifications")]
    Task SendAsync(long teacherId, long subscriptionId);
}