using Edvanz.Domain.Entities.Chat;
using Edvanz.Domain.Enums;

namespace Edvanz.Domain.Interfaces;

/// <summary>
/// Repository for the 1:1 direct-chat subsystem (<see cref="Conversation"/> +
/// <see cref="ChatMessage"/>). All LINQ predicate logic lives here — the Application
/// layer never builds raw expression trees. SaveChanges is never called inside this
/// repo; the caller (service) owns the commit boundary (§5.2 / CLAUDE.md §5.2).
/// </summary>
public interface IChatRepo : IGenericRepo<Conversation, long>
{
    // ── CONVERSATION ─────────────────────────────────────────────────────────

    /// <summary>
    /// Returns the active (non-deleted) conversation between two participants, or null.
    /// Callers MUST pass the SMALLER User.Id as <paramref name="smallerUserId"/> and the
    /// LARGER as <paramref name="largerUserId"/> (canonical pair ordering enforced by the
    /// filtered-unique index UX_Conversations_Participants).
    /// </summary>
    Task<Conversation?> GetByParticipantsAsync(long smallerUserId, long largerUserId);

    /// <summary>
    /// Stages a new <see cref="Conversation"/> row in the EF change tracker.
    /// SaveChanges NOT called — caller owns the commit.
    /// </summary>
    Task InsertConversationAsync(Conversation conversation);

    /// <summary>
    /// Paged conversation list for a user, ordered by <c>LastMessageAt DESC NULLS LAST</c>
    /// (conversations with no messages yet sink to the bottom).
    /// Includes per-conversation unread count: messages the caller did NOT send and has
    /// not yet read. Backed by IX_Conversations_ParticipantA_LastMessageAt and
    /// IX_Conversations_ParticipantB_LastMessageAt.
    /// </summary>
    Task<(IReadOnlyList<ConversationListRow> Items, int TotalCount)> GetConversationListAsync(
        long callerUserId, int page, int pageSize);

    /// <summary>
    /// Returns the conversation by Id only when <paramref name="callerUserId"/> is one of
    /// the two participants. Returns null for any other caller (participant guard /
    /// implicit tenant isolation). Returns a TRACKED entity so the service can mutate
    /// the denormalized last-message fields without a second load.
    /// </summary>
    Task<Conversation?> GetByIdAndParticipantAsync(long conversationId, long callerUserId);

    // ── MESSAGES ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Stages a new <see cref="ChatMessage"/> row in the EF change tracker.
    /// SaveChanges NOT called — caller owns the commit.
    /// </summary>
    Task InsertMessageAsync(ChatMessage message);

    /// <summary>
    /// Paginated message thread for a conversation, ordered by <c>SentAt DESC</c>
    /// (page 1 = most recent messages). The Flutter client reverses the list for
    /// natural top-to-bottom display. Backed by IX_ChatMessages_ConversationId_SentAt.
    /// </summary>
    Task<(IReadOnlyList<ChatMessage> Items, int TotalCount)> GetThreadAsync(
        long conversationId, int page, int pageSize);

    // ── READ STATE ────────────────────────────────────────────────────────────

    /// <summary>
    /// Bulk-marks all unread messages in a conversation as read for
    /// <paramref name="readerUserId"/> — specifically messages sent by the OTHER
    /// participant that are still unread. Uses ExecuteUpdateAsync (single SQL UPDATE,
    /// no entity load). Returns the number of rows transitioned.
    /// Idempotent: re-running on already-read rows is a safe no-op.
    /// </summary>
    Task<int> MarkConversationReadAsync(
        long conversationId, long readerUserId, DateTime readAtUtc);

    /// <summary>
    /// Total unread message count across ALL conversations for <paramref name="userId"/>.
    /// Messages where the user is a participant but NOT the sender and IsRead = false.
    /// Backs the chat tab-bar badge on the Flutter app.
    /// </summary>
    Task<int> GetTotalUnreadCountAsync(long userId);
}

// ════════════════════════════════════════════════════════════════════════════
// PROJECTIONS — live in Domain alongside the interface
// (precedent: PagedAttendanceStudentRow / UpcomingExpiryProjection)
// ════════════════════════════════════════════════════════════════════════════

/// <summary>
/// One row of the conversation-list query — lean projection that avoids loading
/// full entities and eliminates N+1 unread-count sub-queries by computing the
/// count inline in the single DB round-trip.
/// </summary>
public class ConversationListRow
{
    public long ConversationId { get; set; }
    public long OtherParticipantUserId { get; set; }
    public string OtherParticipantName { get; set; } = null!;
    public UserType OtherParticipantUserType { get; set; }
    public string? LastMessagePreview { get; set; }
    public DateTime? LastMessageAt { get; set; }

    /// <summary>
    /// User.Id of the sender of the most recent message. The service layer derives
    /// <c>IsLastMessageMine</c> from this without a second query.
    /// </summary>
    public long? LastMessageSenderUserId { get; set; }

    /// <summary>
    /// Count of unread messages for the calling user in this conversation.
    /// Computed as: ChatMessages WHERE ConversationId = this AND SenderUserId ≠ caller AND IsRead = false.
    /// </summary>
    public int UnreadCount { get; set; }
}