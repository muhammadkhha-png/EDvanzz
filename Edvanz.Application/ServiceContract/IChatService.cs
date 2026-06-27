using Edvanz.Application.Dtos;
using Edvanz.Application.Dtos.Chat;

namespace Edvanz.Application.IservicesContract;

/// <summary>
/// Application-layer contract for the 1:1 direct-chat subsystem.
///
/// ELIGIBILITY RULE enforced on every conversation-creating or conversation-accessing call:
///   Adult↔adult (Teacher / Assistant / Parent / SuperAdmin): OPEN — no prior relationship needed.
///   Any conversation involving a Student: LINK-GATED.
///     Student ↔ Teacher   → active StudentTeacherLink required.
///     Student ↔ Parent    → active Method-A ParentChild link required.
///     Student ↔ Assistant → active StudentTeacherLink to the assistant's owning Teacher required.
///     Student ↔ Student   → DISABLED in v1.
///     Student ↔ SuperAdmin→ DISABLED.
///
/// CALLER IDENTITY: every method receives <c>callerUserId</c> (User.Id from JWT).
/// Controllers NEVER pass the caller's id from the request body or route.
/// </summary>
public interface IChatService
{
    /// <summary>
    /// Returns the existing 1:1 conversation between <paramref name="callerUserId"/> and
    /// <paramref name="targetUserId"/>, or creates one if none exists. Applies the
    /// eligibility gate before any creation. Idempotent: repeated calls for the same pair
    /// always return the same conversation row.
    /// Returns 201 Created on first creation, 200 OK on retrieval.
    /// </summary>
    Task<Result<ConversationDto>> GetOrCreateConversationAsync(
        long callerUserId, long targetUserId);

    /// <summary>
    /// Sends a message inside an existing conversation. Validates that
    /// <paramref name="callerUserId"/> is one of the two participants. Persists the
    /// message, refreshes the conversation's denormalized last-message fields, and
    /// dispatches the FCM push notification job for the recipient.
    /// </summary>
    Task<Result<ChatMessageDto>> SendMessageAsync(
        long callerUserId, long conversationId, string body);

    /// <summary>
    /// Returns the caller's conversation list ordered by most-recent message first
    /// (LastMessageAt DESC). Includes per-conversation unread counts for badge display.
    /// </summary>
    Task<Result<PaginatedResponse<List<ConversationDto>>>> GetConversationListAsync(
        long callerUserId, int page, int pageSize);

    /// <summary>
    /// Returns the message thread for a conversation (page 1 = most recent messages,
    /// DESC order). Validates participant membership before returning any data.
    /// Does NOT mark messages as read — that is an explicit caller action via
    /// <see cref="MarkReadAsync"/>.
    /// </summary>
    Task<Result<PaginatedResponse<List<ChatMessageDto>>>> GetThreadAsync(
        long callerUserId, long conversationId, int page, int pageSize);

    /// <summary>
    /// Marks all unread messages in a conversation as read for the calling user.
    /// Returns the count of rows transitioned. Idempotent: running on an already-read
    /// conversation returns 0 rows without an error.
    /// </summary>
    Task<Result<int>> MarkReadAsync(long callerUserId, long conversationId);

    /// <summary>
    /// Returns the total unread message count across all conversations for the caller.
    /// Backs the chat-tab badge on the Flutter app's bottom navigation bar.
    /// </summary>
    Task<Result<int>> GetUnreadCountAsync(long callerUserId);
}