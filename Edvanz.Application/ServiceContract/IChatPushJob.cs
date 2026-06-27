using Hangfire;

namespace Edvanz.Application.IservicesContract;

/// <summary>
/// Per-message FCM push worker for the 1:1 direct-chat subsystem.
/// Enqueued by <see cref="IChatPushDispatcher"/> after a message is committed.
/// Fans out to every active <c>UserDeviceToken</c> for the recipient.
///
/// QUEUE: "notifications" — shares the existing worker pool (CLAUDE.md §6.1).
/// IDEMPOTENCY: a delivery failure on one device token does not abort the whole job.
///   Per-token errors are caught; stale tokens are deactivated via
///   IUserDeviceTokenRepo.DeactivateTokenAsync (EC-11 pattern).
/// HANGFIRE RETRY: 3 attempts (default). FCM accepts duplicate sends gracefully.
///
/// The [Queue] attribute is on the INTERFACE method — Hangfire reads it at enqueue
/// time (CLAUDE.md §6.1). The implementation class does NOT repeat it.
/// </summary>
public interface IChatPushJob
{
    /// <summary>
    /// Delivers a new-message push notification to every active device of
    /// <paramref name="recipientUserId"/>.
    /// </summary>
    /// <param name="conversationId">
    /// Encoded in the FCM data payload as the deep-link target so Flutter routes
    /// directly to the conversation thread on tap.
    /// </param>
    /// <param name="senderName">
    /// Pre-resolved before enqueueing so the job avoids a DB round-trip for display name.
    /// </param>
    /// <param name="messagePreview">Truncated body (≤100 chars) for the OS notification body.</param>
    /// <param name="recipientUserId">User.Id of the message recipient.</param>
    [Queue("notifications")]
    Task SendAsync(
        long conversationId,
        string senderName,
        string messagePreview,
        long recipientUserId);
}