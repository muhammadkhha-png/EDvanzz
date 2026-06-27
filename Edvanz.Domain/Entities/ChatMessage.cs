using Edvanz.Domain.Entities.ShareProp;

namespace Edvanz.Domain.Entities.Chat;

/// <summary>
/// A single message within a 1:1 <see cref="Conversation"/>.
///
/// Two-way chat: the sender is whichever participant authored it; the recipient is the
/// other participant (derived from the conversation, not stored). Read state is tracked
/// per message via <see cref="IsRead"/> / <see cref="ReadAt"/> — sufficient for a 1:1
/// thread, so no separate read-receipt table.
///
/// PERSISTED CONTRACT: see EdvanzDbContext.OnModelCreating. ConversationId and
/// SenderUserId are NoAction FKs with NO [ForeignKey] attribute (Fluent API only).
/// </summary>
public class ChatMessage : BaseEntity
{
    /// <summary>FK to the owning conversation. NoAction-deleted (app-layer cascade).</summary>
    public long ConversationId { get; set; }

    /// <summary>The conversation this message belongs to.</summary>
    public Conversation Conversation { get; set; } = null!;

    /// <summary>
    /// FK to the User who sent this message (one of the conversation's two participants).
    /// NoAction on account purge.
    /// </summary>
    public long SenderUserId { get; set; }

    /// <summary>The sender account.</summary>
    public User SenderUser { get; set; } = null!;

    /// <summary>
    /// Free-text message body (AAM-FR-07.4 — unstructured content). Required.
    /// Trimmed and length-validated in the service layer before persistence.
    /// </summary>
    public string Body { get; set; } = null!;

    /// <summary>UTC timestamp the message was sent (server-stamped).</summary>
    public DateTime SentAt { get; set; }

    /// <summary>
    /// True once the recipient (the other participant) has opened the thread past this
    /// message. Flipped in bulk by the mark-read path. Backs the unread badge.
    /// </summary>
    public bool IsRead { get; set; } = false;

    /// <summary>UTC timestamp the recipient read this message. Null until read.</summary>
    public DateTime? ReadAt { get; set; }

    /// <summary>
    /// Soft-delete flag for a future "unsend/delete message" feature (out of scope v1).
    /// Present for convention compliance.
    /// </summary>
    public bool IsDeleted { get; set; } = false;

    /// <summary>UTC timestamp of soft-deletion. Null while active.</summary>
    public DateTime? DeletedAt { get; set; }
}