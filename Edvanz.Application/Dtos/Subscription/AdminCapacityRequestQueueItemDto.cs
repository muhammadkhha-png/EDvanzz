namespace Edvanz.Application.Dtos.Subscription;

/// <summary>
/// One row of GET /api/admin/subscriptions/capacity-requests — a Pending
/// capacity-increase request enriched with live teacher context so the admin can decide
/// with the usage picture in front of them.
/// </summary>
public class AdminCapacityRequestQueueItemDto
{
    /// <summary>The CapacityIncreaseRequest row id (used by approve/reject).</summary>
    public long Id { get; set; }

    public long TeacherId { get; set; }

    public string TeacherName { get; set; } = string.Empty;

    public string TeacherCode { get; set; } = string.Empty;

    /// <summary>The teacher's LIVE StudentCapacity (may differ from CapacityAtRequest if an admin changed it since).</summary>
    public int CurrentCapacity { get; set; }

    /// <summary>The teacher's StudentCapacity snapshotted at submission time.</summary>
    public int CapacityAtRequest { get; set; }

    /// <summary>The capacity the teacher is asking for.</summary>
    public int RequestedCapacity { get; set; }

    /// <summary>Live count of the teacher's active roster students (usage vs. limit context).</summary>
    public int ActiveStudentCount { get; set; }

    /// <summary>What the teacher would pay per renewal if approved: RequestedCapacity × per-student rate (0 when the rate is unconfigured).</summary>
    public decimal ProjectedMonthlyPriceEGP { get; set; }

    /// <summary>The teacher's optional justification.</summary>
    public string? Note { get; set; }

    /// <summary>When the request was submitted (UTC). Queue is FIFO on this value.</summary>
    public DateTime RequestedAt { get; set; }
}
