namespace Edvanz.Application.Dtos.Subscription;

/// <summary>
/// Body DTO for POST /api/admin/subscriptions/pending/{id}/reject.
/// Wraps the rejection reason so it's a structured POST body, not a raw string.
/// </summary>
public class RejectPendingRequest
{
    /// <summary>
    /// The reason the admin is rejecting this payment. Required, max 500 chars.
    /// Surfaced to the teacher via the rejection notification.
    /// </summary>
    public string RejectionReason { get; set; } = null!;
}