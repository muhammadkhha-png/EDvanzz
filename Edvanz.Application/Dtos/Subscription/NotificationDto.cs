using Edvanz.Domain.Enums;

namespace Edvanz.Application.Dtos.Subscription;

/// <summary>
/// One row of the bell-icon feed (FR-SUB-051).
/// </summary>
public class NotificationDto
{
    public long Id { get; set; }

    public string Title { get; set; } = null!;

    public string Body { get; set; } = null!;

    /// <summary>
    /// Optional JSON payload for frontend deep-linking. Null when no specific route applies.
    /// </summary>
    public string? DeepLinkPayload { get; set; }

    public DateTime SentAt { get; set; }

    public bool IsRead { get; set; }

    public DateTime? ReadAt { get; set; }

    public NotificationCategory Category { get; set; }
}