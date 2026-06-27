namespace Edvanz.Application.Dtos.Chat;

/// <summary>
/// Wire shape for a single message in a conversation thread.
/// </summary>
public class ChatMessageDto
{
    public long MessageId { get; set; }
    public long ConversationId { get; set; }
    public long SenderUserId { get; set; }

    /// <summary>Display name of the sender at response time (not frozen).</summary>
    public string SenderName { get; set; } = null!;

    public string Body { get; set; } = null!;

    /// <summary>UTC timestamp stamped server-side at insert time.</summary>
    public DateTime SentAt { get; set; }

    /// <summary>True once the recipient has called mark-read past this message.</summary>
    public bool IsRead { get; set; }

    public DateTime? ReadAt { get; set; }

    /// <summary>
    /// True when the calling user is the sender. Computed in the service layer from
    /// the JWT-resolved caller id — never stored on the entity.
    /// </summary>
    public bool IsFromMe { get; set; }
}