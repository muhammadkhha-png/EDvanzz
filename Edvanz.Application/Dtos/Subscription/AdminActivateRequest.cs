namespace Edvanz.Application.Dtos.Subscription;

/// <summary>
/// Input DTO for POST /api/admin/subscriptions/activate (FR-SUB-060 / REQ-ADM-012).
/// Bypasses payment — inserts a new IsCurrent = true row with PaymentChannel = SuperAdminOverride.
/// </summary>
public class AdminActivateRequest
{
    /// <summary>
    /// The teacher to activate.
    /// </summary>
    public long TeacherId { get; set; }

    /// <summary>
    /// Optional explicit start date (UTC). Null defaults to UtcNow at activation time.
    /// EC-21: a future-dated StartDate produces a row that the policy handler treats
    /// as not-yet-active until UtcNow >= StartDate.
    /// </summary>
    public DateTime? StartDate { get; set; }

    /// <summary>
    /// Optional explicit end date (UTC). Null defaults to StartDate + 30 days (D-01).
    /// </summary>
    public DateTime? EndDate { get; set; }
}