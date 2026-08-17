using Edvanz.Domain.Entities.ShareProp;
using Edvanz.Domain.Enums;
using System.ComponentModel.DataAnnotations.Schema;

namespace Edvanz.Domain.Entities;

/// <summary>
/// Audit record of a teacher FORGIVING (waiving) part of a student's outstanding balance.
///
/// Forgiving is NOT a cash event: it produces NO <see cref="PaymentTransaction"/>, NO wallet change and
/// NO collector attribution. It only reduces what the student OWES, by incrementing the per-period
/// <see cref="PaymentPeriod.ForgivenAmount"/> on the oldest unpaid months first (cascade, same
/// month-scoping as the collection engine — CLAUDE.md §7.4).
///
/// REVERSIBILITY: each forgiveness records exactly which periods it touched and by how much
/// (<see cref="Allocations"/>), so reversing it restores the precise balances. Status moves
/// Active → Reversed; the row (and its reversal audit) is kept permanently.
///
/// TENANT / TUTOR-ONLY: only the owning Teacher (or SuperAdmin) may forgive — assistants and
/// center-assistants are blocked at the API gate (BR-PAY-002 class of money-modifying action).
///
/// Denormalized StudentName/StudentCode/SessionName survive student purge / session hard-delete so the
/// audit trail stays self-describing (same pattern as <see cref="PaymentTransaction"/>).
/// </summary>
public class PaymentForgiveness : BaseEntity
{
    // ══════════════════════════════════════════════
    // TENANT ISOLATION
    // ══════════════════════════════════════════════

    /// <summary>Owning teacher. Denormalized for tenant-scoped indexes (REQ-PAY-NFR-001).</summary>
    [ForeignKey(nameof(Teacher))]
    public long TeacherId { get; set; }
    public Teacher Teacher { get; set; } = null!;

    // ══════════════════════════════════════════════
    // STUDENT / SESSION REFERENCE (nullable for purge/hard-delete survival)
    // ══════════════════════════════════════════════

    /// <summary>The student whose balance was forgiven. SET NULL on permanent purge; snapshots below persist.</summary>
    [ForeignKey(nameof(TeacherStudent))]
    public long? TeacherStudentId { get; set; }
    public TeacherStudent? TeacherStudent { get; set; }

    /// <summary>The student's session at forgive time (context only). Plain denormalized id — NO FK.</summary>
    public long? SessionId { get; set; }

    // ══════════════════════════════════════════════
    // FINANCIAL DATA
    // ══════════════════════════════════════════════

    /// <summary>Total amount forgiven across the touched periods (sum of <see cref="Allocations"/>).</summary>
    [Column(TypeName = "decimal(10,2)")]
    public decimal Amount { get; set; }

    /// <summary>Optional free-text note recorded by the teacher (why the balance was waived).</summary>
    public string? Note { get; set; }

    // ══════════════════════════════════════════════
    // LIFECYCLE / AUDIT
    // ══════════════════════════════════════════════

    /// <summary>Active while in effect; Reversed once the waived balance is restored.</summary>
    public ForgivenessStatus Status { get; set; } = ForgivenessStatus.Active;

    /// <summary>The tutor (or SuperAdmin) user who forgave. Plain column, no FK.</summary>
    public long ForgivenByUserId { get; set; }

    /// <summary>Server UTC timestamp of the forgiveness.</summary>
    public DateTime ForgivenAt { get; set; }

    /// <summary>Who reversed the forgiveness (null while Active). Plain column, no FK.</summary>
    public long? ReversedByUserId { get; set; }

    /// <summary>When the forgiveness was reversed (null while Active).</summary>
    public DateTime? ReversedAt { get; set; }

    /// <summary>Optional note recorded on reversal.</summary>
    public string? ReversalNote { get; set; }

    // ══════════════════════════════════════════════
    // DENORMALIZED CONTEXT (survive hard-delete/purge)
    // ══════════════════════════════════════════════

    public string? StudentName { get; set; }
    public string? StudentCode { get; set; }
    public string? SessionName { get; set; }

    // ══════════════════════════════════════════════
    // PER-PERIOD LEDGER (precise reversal)
    // ══════════════════════════════════════════════

    /// <summary>One row per <see cref="PaymentPeriod"/> this forgiveness reduced, with the amount waived
    /// on each — so a reversal restores the exact balances even after later collections.</summary>
    public ICollection<PaymentForgivenessAllocation> Allocations { get; set; } = new List<PaymentForgivenessAllocation>();
}
