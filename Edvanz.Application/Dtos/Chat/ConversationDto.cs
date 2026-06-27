namespace Edvanz.Application.Dtos.Chat;

/// <summary>
/// Wire shape for a conversation entry — used in the conversation list and as the
/// result of a get-or-create call.
/// </summary>
public class ConversationDto
{
    public long ConversationId { get; set; }

    /// <summary>User.Id of the other participant.</summary>
    public long OtherParticipantUserId { get; set; }

    /// <summary>Display name of the other participant (User.FullName).</summary>
    public string OtherParticipantName { get; set; } = null!;

    /// <summary>Role string of the other participant (Teacher / Student / Parent / Assistant).</summary>
    public string OtherParticipantRole { get; set; } = null!;

    /// <summary>
    /// Truncated preview of the most recent message body.
    /// Null when the conversation has no messages yet.
    /// </summary>
    public string? LastMessagePreview { get; set; }

    /// <summary>UTC timestamp of the most recent message. Null when no messages exist.</summary>
    public DateTime? LastMessageAt { get; set; }

    /// <summary>True when the most recent message was sent by the calling user.</summary>
    public bool IsLastMessageMine { get; set; }

    /// <summary>
    /// Unread message count for the calling user in this conversation.
    /// Drives the per-conversation badge on the Flutter chat list.
    /// </summary>
    public int UnreadCount { get; set; }
}