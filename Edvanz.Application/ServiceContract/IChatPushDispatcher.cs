namespace Edvanz.Application.IservicesContract;

/// <summary>
/// Intent-based dispatcher that enqueues a <see cref="IChatPushJob"/> after a
/// direct-chat message is committed. Decouples the Application layer from Hangfire's
/// <c>IBackgroundJobClient</c> (CLAUDE.md §6.2 — Hangfire belongs in Infrastructure).
///
/// The Infrastructure implementation calls
/// <c>IBackgroundJobClient.Enqueue&lt;IChatPushJob&gt;(j => j.SendAsync(...))</c>.
/// The Application layer only knows this interface.
/// </summary>
public interface IChatPushDispatcher
{
    /// <summary>
    /// Enqueues the push-notification job for a newly sent direct-chat message.
    /// Fire-and-forget — the message is already durably committed before this is called,
    /// so a dispatch failure does not affect message persistence.
    /// </summary>
    void Dispatch(
        long conversationId,
        string senderName,
        string messagePreview,
        long recipientUserId);
}