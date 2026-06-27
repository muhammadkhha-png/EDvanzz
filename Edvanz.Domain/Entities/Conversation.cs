using Edvanz.Domain.Entities.ShareProp;

namespace Edvanz.Domain.Entities.Chat;

/// <summary>
/// A 1:1 direct-message conversation between exactly two user accounts.
///
/// SCOPE NOTE: This subsystem implements two-way direct chat. It deliberately
/// SUPERSEDES the one-directional internal messaging specified in AAM-FR-07 /
/// AAM-BR-08 (product decision — bidirectional any-pair chat, link-gated where a
/// Student is involved). Participant eligibility (adult↔adult open; any conversation
/// touching a Student requires an existing StudentTeacherLink / ParentChild link) is
/// enforced in the Application layer at creation time, NOT stored on this row — so the
/// rule can evolve without a schema change.
///
/// PAIR IDENTITY: participants are stored in canonical order — ParticipantAUserId is
/// always the smaller User.Id, ParticipantBUserId the larger. A filtered-unique index
/// on (ParticipantAUserId, ParticipantBUserId) guarantees exactly one live conversation
/// per pair. Callers MUST order the two ids before lookup/insert.
///
/// PERSISTED CONTRACT: column types, lengths, FK behaviors (all NoAction — app-layer
/// cascade, required by SQL Server's multiple-cascade-path rule), and indexes are
/// defined in EdvanzDbContext.OnModelCreating. FK columns intentionally carry NO
/// [ForeignKey] attribute — Fluent API is the single source of truth (EF Core 10 drops
/// the explicit OnDelete when both are present).
/// </summary>
public class Conversation : BaseEntity
{
    /// <summary>
    /// The participant with the SMALLER User.Id (canonical ordering).
    /// FK to Users; NoAction on account purge (app-layer cascade).
    /// </summary>
    public long ParticipantAUserId { get; set; }

    /// <summary>The User on the "A" side of the pair.</summary>
    public User ParticipantAUser { get; set; } = null!;

    /// <summary>
    /// The participant with the LARGER User.Id (canonical ordering).
    /// FK to Users; NoAction on account purge (app-layer cascade).
    /// </summary>
    public long ParticipantBUserId { get; set; }

    /// <summary>The User on the "B" side of the pair.</summary>
    public User ParticipantBUser { get; set; } = null!;

    /// <summary>
    /// UTC timestamp of the most recent message. Null until the first message is sent.
    /// Drives the conversation-list ordering (most-recent first).
    /// </summary>
    public DateTime? LastMessageAt { get; set; }

    /// <summary>
    /// Denormalized snippet of the most recent message body for the conversation list,
    /// avoiding a per-row join to ChatMessages. Refreshed on every send.
    /// </summary>
    public string? LastMessagePreview { get; set; }

    /// <summary>
    /// User.Id of the sender of the most recent message. Lets the conversation list
    /// render "You: ..." vs the other party without loading the message row.
    /// </summary>
    public long? LastMessageSenderUserId { get; set; }

    /// <summary>
    /// Soft-delete flag (project-wide default). No delete/hide endpoint exists in v1;
    /// present for convention compliance and the filtered-unique pair index.
    /// </summary>
    public bool IsDeleted { get; set; } = false;

    /// <summary>UTC timestamp of soft-deletion. Null while active.</summary>
    public DateTime? DeletedAt { get; set; }

    /// <summary>
    /// Messages in this conversation, ordered by SentAt at query time.
    /// NoAction-deleted (app-layer).
    /// </summary>
    public ICollection<ChatMessage> Messages { get; set; } = new List<ChatMessage>();
}