using Edvanz.Domain.Entities.ShareProp;
using Edvanz.Domain.Enums;

namespace Edvanz.Domain.Entities;

/// <summary>
/// A center-initiated request to ACTIVATE / change its subscription package: the center picks how
/// many Full and Managerial teacher slots and the student capacities (overall / under Full / under
/// Managerial) it wants; the super admin reviews and on approve activates the matching
/// <see cref="CenterSubscription"/>. Payment is arranged OUTSIDE the app — this request is only the
/// "please set me up" signal. Mirrors <see cref="SubscriptionRequest"/> (the teacher analogue).
///
/// Lifecycle (<see cref="SubscriptionRequestStatus"/>): Pending → Approved | Rejected | Cancelled.
/// Terminal rows are kept for audit; a filtered unique index allows one live Pending row per center.
///
/// FK behavior is configured ENTIRELY in Fluent API (EdvanzDbContext) — no [ForeignKey]
/// annotations here, per the BUG-4 rule.
/// </summary>
public class CenterSubscriptionRequest : BaseEntity
{
    /// <summary>The center the subscription is requested for.</summary>
    public long CenterId { get; set; }
    public Center Center { get; set; } = null!;

    // ── Requested quota package ─────────────────────────────────
    public int FullTeacherSlots { get; set; }
    public int ManagerialTeacherSlots { get; set; }
    public int StudentCapacityTotal { get; set; }
    public int StudentCapacityUnderFull { get; set; }
    public int StudentCapacityUnderManagerial { get; set; }

    /// <summary>
    /// Fee computed server-side at submission time from the admin-editable per-slot rates
    /// (Full slots × FullTeacherRate + Managerial slots × ManagerialRate). A snapshot for the
    /// admin's context; a client-supplied amount is never trusted.
    /// </summary>
    public decimal ComputedAmountEGP { get; set; }

    /// <summary>Optional free-text note from the center (max 500, Fluent).</summary>
    public string? Note { get; set; }

    /// <summary>Current lifecycle state.</summary>
    public SubscriptionRequestStatus Status { get; set; } = SubscriptionRequestStatus.Pending;

    /// <summary>When the center submitted the request (UTC).</summary>
    public DateTime RequestedAt { get; set; }

    /// <summary>User id of whoever submitted (the center, or a center assistant). Plain audit column — no FK.</summary>
    public long RequestedByUserId { get; set; }

    /// <summary>When Status reached a terminal state. Null while Pending.</summary>
    public DateTime? ResolvedAt { get; set; }

    /// <summary>The super admin who approved/rejected, or the center-side user who cancelled.</summary>
    public long? ResolvedByUserId { get; set; }
    public User? ResolvedByUser { get; set; }

    /// <summary>Reason given when Status = Rejected (max 500, Fluent). Shown to the center.</summary>
    public string? RejectionReason { get; set; }
}
