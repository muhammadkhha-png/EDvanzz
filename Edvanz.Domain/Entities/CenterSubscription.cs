using Edvanz.Domain.Entities.ShareProp;
using System.ComponentModel.DataAnnotations;

namespace Edvanz.Domain.Entities;

/// <summary>
/// A center's subscription period AND its live entitlement (the quota package). One center has
/// many rows over time; exactly one row per center has <see cref="IsCurrent"/> = true, enforced
/// by the filtered unique index IX_CenterSubscriptions_Current (mirrors BR-SUB-006 for teachers).
///
/// The package the center chooses / the super admin activates: how many <see cref="FullTeacherSlots"/>
/// and <see cref="ManagerialTeacherSlots"/> the center may create, and the aggregate student
/// capacity — <see cref="StudentCapacityTotal"/> overall, split into
/// <see cref="StudentCapacityUnderFull"/> and <see cref="StudentCapacityUnderManagerial"/>. These
/// are enforced live against the center's actual teacher/student counts on create.
///
/// Status (Active / ExpiringSoon / Expired) is DERIVED at read time from (IsCurrent, StartDate,
/// EndDate) via SubscriptionStatusCalculator — never stored (mirrors the teacher D-08 rule). The
/// center's subscription covers ALL its teachers; center-owned teachers have no TeacherSubscription.
///
/// FK behavior is configured ENTIRELY in Fluent API (EdvanzDbContext) — no [ForeignKey]
/// annotations here, per the BUG-4 rule.
/// </summary>
public class CenterSubscription : BaseEntity
{
    /// <summary>The center that owns this subscription period.</summary>
    public long CenterId { get; set; }
    public Center Center { get; set; } = null!;

    // ── Period ──────────────────────────────────────────────────
    /// <summary>Period start (UTC). Equals PaymentConfirmedAt for a first-of-sequence activation;
    /// equals the previous current row's EndDate for an early renewal (no days lost).</summary>
    public DateTime StartDate { get; set; }

    /// <summary>Period end (UTC). StartDate + 30 days by default; admin can override.</summary>
    public DateTime EndDate { get; set; }

    /// <summary>True for the center's CURRENT row (the one middleware evaluates). Exactly one per center.</summary>
    public bool IsCurrent { get; set; }

    // ── Quota package (the entitlement) ─────────────────────────
    /// <summary>Number of Full-plan teacher slots the center may have active.</summary>
    public int FullTeacherSlots { get; set; }

    /// <summary>Number of Managerial-plan teacher slots the center may have active.</summary>
    public int ManagerialTeacherSlots { get; set; }

    /// <summary>Number of Managerial + Parents (ManagerialPlus) teacher slots the center may have active.</summary>
    public int ManagerialPlusTeacherSlots { get; set; }

    /// <summary>Overall student capacity across ALL of the center's teachers.</summary>
    public int StudentCapacityTotal { get; set; }

    /// <summary>Student capacity pooled across the center's Full-plan teachers.</summary>
    public int StudentCapacityUnderFull { get; set; }

    /// <summary>Student capacity pooled across the center's Managerial-plan teachers.</summary>
    public int StudentCapacityUnderManagerial { get; set; }

    /// <summary>Student capacity pooled across the center's Managerial + Parents teachers.</summary>
    public int StudentCapacityUnderManagerialPlus { get; set; }

    // ── Payment / audit ─────────────────────────────────────────
    /// <summary>Amount paid in EGP (decimal(10,2) via Fluent). Zero for a super-admin manual activation.</summary>
    public decimal AmountPaidEGP { get; set; }

    /// <summary>The single UtcNow captured at the start of the activation transaction — the
    /// authoritative confirmation timestamp and the StartDate source when there is no prior current row.</summary>
    public DateTime PaymentConfirmedAt { get; set; }

    /// <summary>Optional free-text note (max 500, Fluent).</summary>
    public string? Note { get; set; }

    /// <summary>The user (super admin) who created this subscription record.</summary>
    public long? CreatedByUserId { get; set; }
    public User? CreatedByUser { get; set; }

    /// <summary>SQL Server rowversion for optimistic concurrency on the IsCurrent flip.</summary>
    [Timestamp]
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
}
