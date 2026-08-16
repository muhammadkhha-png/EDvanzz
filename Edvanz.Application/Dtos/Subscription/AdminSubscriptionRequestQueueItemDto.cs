using Edvanz.Domain.Enums;

namespace Edvanz.Application.Dtos.Subscription;

/// <summary>
/// Admin queue row for GET /api/admin/subscriptions/requests — a Pending subscription request
/// enriched with teacher context for the decision screen.
/// </summary>
public class AdminSubscriptionRequestQueueItemDto
{
    /// <summary>The SubscriptionRequest row id.</summary>
    public long Id { get; set; }

    /// <summary>The requesting teacher.</summary>
    public long TeacherId { get; set; }

    /// <summary>Teacher's display name (for the queue screen).</summary>
    public string TeacherName { get; set; } = string.Empty;

    /// <summary>Teacher's shareable code (for the queue screen).</summary>
    public string? TeacherCode { get; set; }

    /// <summary>Requested plan: Full or Managerial.</summary>
    public SubscriptionPlanType PlanType { get; set; }

    /// <summary>Requested student count (0 for Managerial).</summary>
    public int RequestedStudents { get; set; }

    /// <summary>Server-computed fee at submission time, in EGP.</summary>
    public decimal ComputedAmountEGP { get; set; }

    /// <summary>The teacher's optional note.</summary>
    public string? Note { get; set; }

    /// <summary>When the request was submitted (UTC).</summary>
    public DateTime RequestedAt { get; set; }
}
