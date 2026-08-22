using Edvanz.Domain.Enums;

namespace Edvanz.Application.Dtos.Center;

/// <summary>The 5 quota numbers that make up a center subscription package. Reused across request,
/// activation, and read shapes.</summary>
public class CenterQuotaPackage
{
    public int FullTeacherSlots { get; set; }
    public int ManagerialTeacherSlots { get; set; }
    public int StudentCapacityTotal { get; set; }
    public int StudentCapacityUnderFull { get; set; }
    public int StudentCapacityUnderManagerial { get; set; }
}

// ── Center-side ───────────────────────────────────────────────────────────────

/// <summary>Center submits a subscription/package request (admin approves).</summary>
public class SubmitCenterSubscriptionRequestDto : CenterQuotaPackage
{
    public string? Note { get; set; }
}

/// <summary>Center-facing view of its subscription entitlement + derived status + live usage.</summary>
public class CenterSubscriptionDto
{
    public bool HasSubscription { get; set; }
    /// <summary>Derived status string: Active / ExpiringSoon / Expired (null when none).</summary>
    public string? Status { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public int? DaysRemaining { get; set; }

    // Entitlement (the quota package)
    public int FullTeacherSlots { get; set; }
    public int ManagerialTeacherSlots { get; set; }
    public int StudentCapacityTotal { get; set; }
    public int StudentCapacityUnderFull { get; set; }
    public int StudentCapacityUnderManagerial { get; set; }

    // Live usage
    public int UsedFullTeachers { get; set; }
    public int UsedManagerialTeachers { get; set; }
    public int UsedStudentsTotal { get; set; }
    public int UsedStudentsUnderFull { get; set; }
    public int UsedStudentsUnderManagerial { get; set; }

    // A pending request, if any
    public bool HasPendingRequest { get; set; }
    public SubmitCenterSubscriptionRequestDto? PendingRequest { get; set; }
    public decimal? PendingRequestAmountEGP { get; set; }

    /// <summary>The most recent request in ANY status (Pending/Approved/Rejected/Cancelled) —
    /// keeps the outcome visible to the center after the request leaves the Pending state
    /// (a rejected request used to vanish silently).</summary>
    public CenterLatestRequestDto? LatestRequest { get; set; }
}

/// <summary>Center-facing view of its most recent subscription request, whatever its outcome.</summary>
public class CenterLatestRequestDto : CenterQuotaPackage
{
    /// <summary>Pending / Approved / Rejected / Cancelled (SubscriptionRequestStatus name).</summary>
    public string Status { get; set; } = null!;
    public decimal AmountEGP { get; set; }
    public string? Note { get; set; }
    public DateTime RequestedAt { get; set; }
    public DateTime? ResolvedAt { get; set; }
    /// <summary>Set only when Status is Rejected.</summary>
    public string? RejectionReason { get; set; }
}

// ── Admin-side ────────────────────────────────────────────────────────────────

/// <summary>SuperAdmin activates/overrides a center subscription with explicit quotas.</summary>
public class ActivateCenterSubscriptionDto : CenterQuotaPackage
{
    public long CenterId { get; set; }
    /// <summary>Period length; defaults to 30 days.</summary>
    public int DurationDays { get; set; } = 30;
    public string? Note { get; set; }
}

/// <summary>SuperAdmin approves a pending request, optionally adjusting the quotas first.</summary>
public class ApproveCenterSubscriptionRequestDto : CenterQuotaPackage
{
    public int DurationDays { get; set; } = 30;
    public string? Note { get; set; }
}

public class RejectCenterSubscriptionRequestDto
{
    public string? RejectionReason { get; set; }
}

/// <summary>Admin queue row for a pending center subscription request.</summary>
public class CenterSubscriptionRequestQueueItemDto
{
    public long RequestId { get; set; }
    public long CenterId { get; set; }
    public string CenterName { get; set; } = null!;
    public string CenterCode { get; set; } = null!;
    public int FullTeacherSlots { get; set; }
    public int ManagerialTeacherSlots { get; set; }
    public int StudentCapacityTotal { get; set; }
    public int StudentCapacityUnderFull { get; set; }
    public int StudentCapacityUnderManagerial { get; set; }
    public decimal ComputedAmountEGP { get; set; }
    public string? Note { get; set; }
    public DateTime RequestedAt { get; set; }
    public SubscriptionRequestStatus Status { get; set; }
}

/// <summary>Per-slot center pricing (single settings row).</summary>
public class CenterPricingDto
{
    public decimal FullTeacherSlotPriceEGP { get; set; }
    public decimal ManagerialTeacherSlotPriceEGP { get; set; }
}
