using Edvanz.Domain.Entities.ShareProp;
using Edvanz.Domain.Enums;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Edvanz.Domain.Entities;

/// <summary>
/// Represents a single student's recorded state against a single
/// <see cref="AssignmentOccurrence"/>. This is the hot table of the module — every
/// tracking view, grade entry view, and absence report queries it.
///
/// REQ-EXH-007: One row generated per student per occurrence at creation time.
/// REQ-EXH-016/017/019: Status spans three workflows (exam, completion-only HW, graded HW)
/// via the <see cref="ObligationStatus"/> enum.
/// REQ-EXH-026: Barcode scan path uses an atomic conditional UPDATE on this row to mark
/// Attended (exam) or Done (homework) without surfacing a concurrency exception when two
/// scans race; idempotent per design decision 5.3.
/// REQ-EXH-NFR-001: Tracking screens render in &lt; 2 seconds — covering indexes on this
/// table are critical and configured in fluent API.
///
/// HARD DELETE per REQ-EXH-037 (cascade with occurrence and template). Status and grade
/// changes are auditable via <see cref="StudentObligationAuditLog"/>; the audit log itself
/// is not cascaded — service layer detaches audit before delete to retain history.
/// </summary>
public class StudentAssignmentObligation : BaseEntity
{
    // ══════════════════════════════════════════════
    // OCCURRENCE LINKAGE & TENANT SCOPE
    // ══════════════════════════════════════════════

    /// <summary>
    /// The occurrence this obligation belongs to.
    /// </summary>
    [ForeignKey(nameof(Occurrence))]
    public long OccurrenceId { get; set; }
    public AssignmentOccurrence Occurrence { get; set; } = null!;

    /// <summary>
    /// The student this obligation tracks.
    /// REQ-EXH-036: A student may be removed from an assignment's scope; if status was already
    /// recorded, removal also deletes this row (confirmed by tutor).
    /// </summary>
    [ForeignKey(nameof(TeacherStudent))]
    public long TeacherStudentId { get; set; }
    public TeacherStudent TeacherStudent { get; set; } = null!;

    /// <summary>
    /// Foreign key to the owning Teacher. Denormalized from
    /// <see cref="AssignmentOccurrence.TeacherId"/> so the tenant-leading covering indexes
    /// can be evaluated without joining the parent occurrence row.
    /// REQ-EXH-NFR-004: Tenant isolation enforced by index design and query filters.
    /// </summary>
    [ForeignKey(nameof(Teacher))]
    public long TeacherId { get; set; }
    public Teacher Teacher { get; set; } = null!;

    // ══════════════════════════════════════════════
    // RECORDED STATE
    // ══════════════════════════════════════════════

    /// <summary>
    /// The recorded status of this student against this occurrence.
    /// REQ-EXH-016/017/019: Spans completion-only HW, graded HW, and exam workflows.
    /// REQ-EXH-026-A: Grade Entry View filters on Status IN (Attended, DoneWithoutGrade).
    /// </summary>
    public ObligationStatus Status { get; set; } = ObligationStatus.Pending;

    /// <summary>
    /// The numeric grade recorded for this student.
    /// Null for completion-only homework, for Pending/NotDone/DidNotAttend states, and for
    /// any state where a grade has not yet been entered. The
    /// <see cref="IsGradeEntered"/> flag is the authoritative signal for "grade present".
    /// </summary>
    [Column(TypeName = "decimal(8,2)")]
    public decimal? GradeValue { get; set; }

    /// <summary>
    /// True when a numeric grade has been recorded for this obligation.
    /// REQ-EXH-026-A: Combined with Status, this drives the Grade Entry View filter.
    /// Materialized as a column (rather than computed) so it can participate in filtered indexes.
    /// </summary>
    public bool IsGradeEntered { get; set; } = false;

    // ══════════════════════════════════════════════
    // BARCODE SCAN METADATA (REQ-EXH-026)
    // ══════════════════════════════════════════════

    /// <summary>
    /// True when this obligation was marked via barcode scan rather than manual entry.
    /// REQ-EXH-026: The system marks Attended (exam) or Done (homework) automatically on scan.
    /// </summary>
    public bool MarkedByScan { get; set; } = false;

    /// <summary>
    /// Timestamp of the barcode scan that marked this obligation. Null if not scanned.
    /// REQ-EXH-NFR-002: Scan-to-mark must complete in &lt; 1 second; the atomic UPDATE
    /// path sets this value alongside Status in a single statement.
    /// </summary>
    public DateTime? ScannedAt { get; set; }

    // ══════════════════════════════════════════════
    // AUDIT
    // ══════════════════════════════════════════════

    /// <summary>
    /// The User who most recently updated this obligation (status change, grade entry, or scan).
    /// Each transition is also captured in <see cref="StudentObligationAuditLog"/> with full deltas.
    /// </summary>
    [ForeignKey(nameof(LastUpdatedByUser))]
    public long? LastUpdatedByUserId { get; set; }
    public User? LastUpdatedByUser { get; set; }

    /// <summary>
    /// Last update timestamp. Maintained by the service layer on every state change.
    /// </summary>
    public DateTime UpdatedAt { get; set; }

    // ══════════════════════════════════════════════
    // CONCURRENCY
    // ══════════════════════════════════════════════

    /// <summary>
    /// Optimistic concurrency token. Used on the manual grade-entry path to surface conflicts
    /// when two users edit the same obligation. The barcode-scan path uses an atomic conditional
    /// UPDATE instead of RowVersion to avoid exceptions on the &lt; 1 second hot path
    /// (decision 5.3 in the design review).
    /// </summary>
    [Timestamp]
    public byte[] RowVersion { get; set; } = null!;

    // ══════════════════════════════════════════════
    // NAVIGATION PROPERTIES
    // ══════════════════════════════════════════════

    /// <summary>
    /// Audit-log entries for every status or grade change on this obligation.
    /// Append-only; retained indefinitely per design decision 5.1.
    /// </summary>
    public ICollection<StudentObligationAuditLog> AuditLogs { get; set; }
        = new List<StudentObligationAuditLog>();
}
