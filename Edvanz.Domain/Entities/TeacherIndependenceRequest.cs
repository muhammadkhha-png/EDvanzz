using Edvanz.Domain.Entities.ShareProp;
using Edvanz.Domain.Enums;

namespace Edvanz.Domain.Entities;

/// <summary>
/// A center-owned teacher's formal request to LEAVE the center and become an independent
/// (standalone) teacher with their own account/subscription. The center manages the teacher's
/// login and subscription today; a teacher who wants to run independently submits this request,
/// which lands in a SuperAdmin queue. On approve the admin DETACHES the teacher from the center
/// (clears <c>Teacher.CenterId</c> and the center-plan/revenue overrides) so the teacher becomes a
/// normal standalone teacher who then subscribes on their own. Payment/settlement of any handover
/// is arranged OUTSIDE the app — this request is only the "please detach me" signal.
///
/// Mirrors <see cref="CenterSubscriptionRequest"/>. Lifecycle
/// (<see cref="SubscriptionRequestStatus"/>): Pending → Approved | Rejected | Cancelled. Terminal
/// rows are kept for audit; a filtered unique index allows one live Pending row per teacher.
///
/// FK behavior is configured ENTIRELY in Fluent API (EdvanzDbContext) — no [ForeignKey]
/// annotations here, per the BUG-4 rule.
/// </summary>
public class TeacherIndependenceRequest : BaseEntity
{
    /// <summary>The center-owned teacher requesting independence.</summary>
    public long TeacherId { get; set; }
    public Teacher Teacher { get; set; } = null!;

    /// <summary>The center the teacher belongs to at submission time (audit — the FK is cleared on
    /// approve, but this column records who they were under).</summary>
    public long CenterId { get; set; }
    public Center Center { get; set; } = null!;

    /// <summary>Optional free-text reason from the teacher (max 500, Fluent).</summary>
    public string? Note { get; set; }

    /// <summary>Current lifecycle state.</summary>
    public SubscriptionRequestStatus Status { get; set; } = SubscriptionRequestStatus.Pending;

    /// <summary>When the teacher submitted the request (UTC).</summary>
    public DateTime RequestedAt { get; set; }

    /// <summary>The teacher's user id (who submitted). Plain audit column — no FK.</summary>
    public long RequestedByUserId { get; set; }

    /// <summary>When Status reached a terminal state. Null while Pending.</summary>
    public DateTime? ResolvedAt { get; set; }

    /// <summary>The super admin who approved/rejected, or the teacher-side user who cancelled.</summary>
    public long? ResolvedByUserId { get; set; }
    public User? ResolvedByUser { get; set; }

    /// <summary>Reason given when Status = Rejected (max 500, Fluent). Shown to the teacher.</summary>
    public string? RejectionReason { get; set; }
}
