namespace Edvanz.Application.Dtos.Subscription;

/// <summary>
/// Input DTO for POST /api/admin/subscriptions/activate-managerial AND
/// /api/admin/subscriptions/activate-managerial-plus (same shape for both).
///
/// Mirrors <see cref="AdminActivateRequest"/> (same no-payment SuperAdminOverride row,
/// same period defaults) but stamps the new subscription as
/// <see cref="Domain.Enums.SubscriptionPlanType.Managerial"/> (or
/// <see cref="Domain.Enums.SubscriptionPlanType.ManagerialPlus"/> on the -plus route): while
/// this subscription is the teacher's current active one, no student or parent account may be
/// linked to them; ManagerialPlus additionally keeps the public parent follow-up page open.
/// </summary>
public class AdminActivateManagerialRequest
{
    /// <summary>
    /// The teacher to activate a managerial subscription for.
    /// </summary>
    public long TeacherId { get; set; }

    /// <summary>
    /// Optional explicit start date (UTC). Null defaults to UtcNow at activation time.
    /// A future-dated StartDate produces a row that is not-yet-active until UtcNow >= StartDate.
    /// </summary>
    public DateTime? StartDate { get; set; }

    /// <summary>
    /// Optional explicit end date (UTC). Null defaults to StartDate + 30 days.
    /// </summary>
    public DateTime? EndDate { get; set; }

    /// <summary>
    /// When true, atomically severs EVERY existing live student link and active parent link
    /// for the teacher as part of this activation (they become RemovedByTeacher). When false
    /// (default), existing students/parents are kept — only NEW links are blocked going
    /// forward. The forward block applies either way; this flag only controls existing links.
    /// </summary>
    public bool RemoveExistingLinks { get; set; }
}
