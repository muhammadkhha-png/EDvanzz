namespace Edvanz.Application.Dtos.Chat;

/// <summary>
/// Input for POST /api/chat/conversations/{conversationId}/messages.
/// The conversationId comes from the route; the sender from the JWT.
/// </summary>
public class SendMessageRequest
{
    /// <summary>
    /// Free-text message body. Trimmed server-side before persistence.
    /// 1–4 000 characters. Supports Arabic and English (AAM-NFR-06).
    /// </summary>
    public string Body { get; set; } = null!;
}