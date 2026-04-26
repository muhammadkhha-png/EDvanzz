using Edvanz.Domain.Enums;

namespace Edvanz.Application.Dtos.Subscription;

/// <summary>
/// Output DTO for GET /api/subscription/renew/status/{pendingPaymentId}
/// and the response of POST /renew/manual-submit. Used for polling while
/// awaiting webhook callback or super-admin review.
/// </summary>
public class RenewStatusDto
{
    public long PendingPaymentId { get; set; }

    public PendingPaymentStatus Status { get; set; }

    /// <summary>
    /// UTC timestamp the tutor initiated the flow.
    /// </summary>
    public DateTime InitiatedAt { get; set; }

    /// <summary>
    /// UTC timestamp the row reached a terminal state (Confirmed / Rejected / Expired).
    /// Null while still in Initiated or AwaitingSuperAdminApproval.
    /// </summary>
    public DateTime? ResolvedAt { get; set; }

    /// <summary>
    /// Reason supplied by super admin when Status = Rejected. Null otherwise.
    /// </summary>
    public string? RejectionReason { get; set; }
}