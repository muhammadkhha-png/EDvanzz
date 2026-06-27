using Edvanz.Application.IservicesContract;
using Hangfire;

namespace Edvanz.Infrastructure.BackGroundJobs;

/// <summary>
/// Infrastructure implementation of <see cref="IChatPushDispatcher"/>.
/// The ONLY class in the chat subsystem that references <see cref="IBackgroundJobClient"/>.
/// Keeps Hangfire out of the Application layer (CLAUDE.md §6.2).
///
/// Follows the same pattern as <see cref="SubscriptionReminderDispatcherJob"/>
/// for IBackgroundJobClient usage.
/// </summary>
public class ChatPushDispatcher : IChatPushDispatcher
{
    private readonly IBackgroundJobClient _backgroundJobs;

    public ChatPushDispatcher(IBackgroundJobClient backgroundJobs)
    {
        _backgroundJobs = backgroundJobs;
    }

    /// <inheritdoc />
    public void Dispatch(
        long conversationId,
        string senderName,
        string messagePreview,
        long recipientUserId)
    {
        // Fire-and-forget: the message is already durable when Dispatch is called.
        // [Queue("notifications")] on the interface method routes to the correct queue.
        _backgroundJobs.Enqueue<IChatPushJob>(
            job => job.SendAsync(conversationId, senderName, messagePreview, recipientUserId));
    }
}