using Edvanz.Domain.Entities.ShareProp;
using Edvanz.Domain.Enums;
using System.ComponentModel.DataAnnotations.Schema;

namespace Edvanz.Domain.Entities;

/// <summary>
/// Append-only audit trail for status and grade transitions on a
/// <see cref="StudentAssignmentObligation"/>. One row inserted on every
/// service-driven state change.
///
/// Design decision 5.1: retained indefinitely. The table is structured so a future
/// range partition by <see cref="ChangedAt"/> month is non-breaking — no unique
/// constraint blocks partition switching.
///
/// Captures BOTH status and grade transitions (not grade-only) because moving a
/// student from "Did Not Attend" to "Attended" is also auditable, and graded
/// homework needs the same trail.
///
/// REQ-EXH-043 grade analysis depends on the MaxGrade and PassingThreshold in effect
/// at the time the grade was recorded. Those are snapshotted here so historical
/// reports remain reproducible if the tutor edits the template later (REQ-EXH-034).
/// </summary>
public class StudentObligationAuditLog : BaseEntity
{
    // ══════════════════════════════════════════════
    // OBLIGATION LINKAGE & TENANT SCOPE
    // ══════════════════════════════════════════════

    /// <summary>
    /// The obligation that was modified.
    /// NOTE: This audit row is NOT cascade-deleted with the obligation.
    /// Service layer detaches audit logs (or copies them to a deletion archive) before
    /// hard-deleting the parent template per REQ-EXH-037, so audit history survives.
    /// </summary>
    [ForeignKey(nameof(StudentObligation))]
    public long StudentObligationId { get; set; }
    public StudentAssignmentObligation StudentObligation { get; set; } = null!;

    /// <summary>
    /// Foreign key to the owning Teacher. Denormalized from the obligation row for
    /// tenant-scoped audit queries (e.g., "show all grade changes for tutor X
    /// in the last 30 days") without joining the obligation table.
    /// </summary>
    [ForeignKey(nameof(Teacher))]
    public long TeacherId { get; set; }
    public Teacher Teacher { get; set; } = null!;

    // ══════════════════════════════════════════════
    // STATUS TRANSITION
    // ══════════════════════════════════════════════

    /// <summary>
    /// The status before the change.
    /// </summary>
    public ObligationStatus OldStatus { get; set; }

    /// <summary>
    /// The status after the change.
    /// </summary>
    public ObligationStatus NewStatus { get; set; }

    // ══════════════════════════════════════════════
    // GRADE TRANSITION
    // ══════════════════════════════════════════════

    /// <summary>
    /// The grade value before the change. Null when the prior state had no grade.
    /// </summary>
    [Column(TypeName = "decimal(8,2)")]
    public decimal? OldGradeValue { get; set; }

    /// <summary>
    /// The grade value after the change. Null when the new state has no grade.
    /// </summary>
    [Column(TypeName = "decimal(8,2)")]
    public decimal? NewGradeValue { get; set; }

    // ══════════════════════════════════════════════
    // SNAPSHOTTED GRADING CONFIGURATION
    // ══════════════════════════════════════════════

    /// <summary>
    /// Snapshot of the occurrence's MaxGrade at the moment of this audit entry.
    /// REQ-EXH-043: Grade analysis reports use this value, not the live template,
    /// so historical analyses remain stable when the tutor edits MaxGrade later.
    /// </summary>
    [Column(TypeName = "decimal(8,2)")]
    public decimal? MaxGradeSnapshot { get; set; }

    /// <summary>
    /// Snapshot of the occurrence's PassingThreshold at the moment of this audit entry.
    /// REQ-EXH-044: Below Passing Grade reports remain reproducible.
    /// </summary>
    [Column(TypeName = "decimal(8,2)")]
    public decimal? PassingThresholdSnapshot { get; set; }

    // ══════════════════════════════════════════════
    // CHANGE METADATA
    // ══════════════════════════════════════════════

    /// <summary>
    /// Optional free-text reason recorded by the tutor for this change.
    /// Useful when a grade is amended after initial entry.
    /// </summary>
    public string? ChangeReason { get; set; }

    /// <summary>
    /// The User who performed the change.
    /// </summary>
    [ForeignKey(nameof(ChangedByUser))]
    public long ChangedByUserId { get; set; }
    public User ChangedByUser { get; set; } = null!;

    /// <summary>
    /// UTC timestamp of the change. Designed as the future partition key
    /// if this table is later range-partitioned by month.
    /// </summary>
    public DateTime ChangedAt { get; set; }
}
