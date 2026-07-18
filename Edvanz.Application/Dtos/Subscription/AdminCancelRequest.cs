namespace Edvanz.Application.Dtos.Subscription;

/// <summary>
/// Input DTO for POST /api/admin/subscriptions/cancel (REQ-ADM-013).
/// Immediately terminates a tutor's CURRENT subscription by expiring it in place
/// (EndDate = UtcNow, IsCurrent unchanged). The tutor drops to the free tier on
/// the next request. Reversible via activate/extend/end-date; history preserved.
/// </summary>
public class AdminCancelRequest
{
    /// <summary>The Teacher.Id whose current subscription is being cancelled.</summary>
    public long TeacherId { get; set; }
}