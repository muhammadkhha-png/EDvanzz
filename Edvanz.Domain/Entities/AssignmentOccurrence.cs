using Edvanz.Domain.Entities.ShareProp;
using Edvanz.Domain.Enums;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Edvanz.Domain.Entities;

/// <summary>
/// Represents one concrete instance of an <see cref="AssignmentTemplate"/>. A one-time template
/// has exactly one occurrence; a recurring template accumulates occurrences over time as the
/// scheduler materializes them.
///
/// REQ-EXH-005: Every assignment has a mandatory date — exposed here as <see cref="DueDate"/>.
/// REQ-EXH-007: On creation of an occurrence, <see cref="StudentAssignmentObligation"/> rows
/// are generated for every student in the resolved scope.
/// REQ-EXH-011: Each generated occurrence is independently trackable with its own student records.
///
/// SNAPSHOTTED CONFIGURATION:
/// REQ-EXH-034 allows the tutor to edit the template's MaxGrade, PassingThreshold, and TrackingMode
/// after creation. To keep historical reports stable (REQ-EXH-043 grade analysis depends on the
/// MaxGrade in effect at the time the exam was taken), this entity carries snapshots of those values
/// taken at occurrence-generation time. Reports and grade-entry logic always read from these
/// snapshots, never from the live template.
///
/// HARD DELETE per REQ-EXH-037 (NoAction with template).
/// </summary>
public class AssignmentOccurrence : BaseEntity
{
    // ══════════════════════════════════════════════
    // TEMPLATE LINKAGE & TENANT SCOPE
    // ══════════════════════════════════════════════

    /// <summary>
    /// The template this occurrence was generated from. NoAction-deleted with the template.
    /// </summary>
    [ForeignKey(nameof(Template))]
    public long TemplateId { get; set; }
    public AssignmentTemplate Template { get; set; } = null!;

    /// <summary>
    /// Foreign key to the owning Teacher. Denormalized from
    /// <see cref="AssignmentTemplate.TeacherId"/> so tenant-scoped queries
    /// (REQ-EXH-NFR-004) avoid a join through the template table.
    /// </summary>
    [ForeignKey(nameof(Teacher))]
    public long TeacherId { get; set; }
    public Teacher Teacher { get; set; } = null!;

    // ══════════════════════════════════════════════
    // SESSION ANCHOR (exam delivery)
    // ══════════════════════════════════════════════

    /// <summary>
    /// EXAM-ONLY: the session this exam occurrence is assigned to. Every exam occurrence is
    /// anchored to exactly one session (both DuringSession and SeparateTime). Null for homework.
    /// Fluent-only FK (no [ForeignKey] annotation) with OnDelete: SetNull.
    /// </summary>
    public long? SessionId { get; set; }
    public Session? Session { get; set; }

    /// <summary>
    /// EXAM-ONLY, DuringSession only: the concrete scheduled session occurrence the exam is taken
    /// during. Exam attendance is driven by and kept in sync with this occurrence's attendance.
    /// Null for SeparateTime exams and for homework.
    /// </summary>
    public long? SessionOccurrenceId { get; set; }
    public SessionOccurrence? SessionOccurrence { get; set; }

    // ══════════════════════════════════════════════
    // OCCURRENCE IDENTITY
    // ══════════════════════════════════════════════

    /// <summary>
    /// Sequential index of this occurrence within its template, starting at 1.
    /// Used in tracking views and reports to identify which iteration of a recurring
    /// assignment is being referenced (e.g., "Quiz 1 — Week 3").
    /// </summary>
    public int OccurrenceNumber { get; set; }

    /// <summary>
    /// The date on which this occurrence is due (homework) or scheduled to take place (exam).
    /// REQ-EXH-005: Mandatory assignment date.
    /// Stored as <c>date</c> — no time component.
    /// </summary>
    [Column(TypeName = "date")]
    public DateTime DueDate { get; set; }

    /// <summary>
    /// Lifecycle status of this occurrence. Drives report filters and edit gates.
    /// </summary>
    public AssignmentOccurrenceStatus Status { get; set; } = AssignmentOccurrenceStatus.Pending;

    // ══════════════════════════════════════════════
    // SNAPSHOTTED GRADING CONFIGURATION
    // ══════════════════════════════════════════════

    /// <summary>
    /// Snapshot of <see cref="AssignmentTemplate.MaxGrade"/> at the moment this occurrence was generated.
    /// Reports use this value, not the live template, so historical analyses remain stable when
    /// the tutor edits the template's MaxGrade per REQ-EXH-034.
    /// Null for homework occurrences and for completion-only contexts.
    /// </summary>
    [Column(TypeName = "decimal(8,2)")]
    public decimal? MaxGradeSnapshot { get; set; }

    /// <summary>
    /// Snapshot of <see cref="AssignmentTemplate.PassingThreshold"/> at occurrence generation time.
    /// Same rationale as <see cref="MaxGradeSnapshot"/>.
    /// REQ-EXH-021/044: Pass/fail flagging and Below Passing Grade report.
    /// </summary>
    [Column(TypeName = "decimal(8,2)")]
    public decimal? PassingThresholdSnapshot { get; set; }

    /// <summary>
    /// Snapshot of <see cref="AssignmentTemplate.TrackingMode"/> at occurrence generation time.
    /// Null for exam occurrences. Defends against the edge case where the tutor
    /// flips a homework template's tracking mode mid-term.
    /// </summary>
    public HomeworkTrackingMode? TrackingModeSnapshot { get; set; }

    // ══════════════════════════════════════════════
    // CONCURRENCY
    // ══════════════════════════════════════════════

    /// <summary>
    /// Optimistic concurrency token. Protects against lost updates when the scheduler
    /// or service layer modifies <see cref="Status"/> concurrently with grade-entry flows.
    /// </summary>
    [Timestamp]
    public byte[] RowVersion { get; set; } = null!;

    // ══════════════════════════════════════════════
    // NAVIGATION PROPERTIES
    // ══════════════════════════════════════════════

    /// <summary>
    /// All student obligations for this occurrence — one per student in the resolved scope.
    /// REQ-EXH-007: Generated immediately on occurrence creation.
    /// </summary>
    public ICollection<StudentAssignmentObligation> Obligations { get; set; }
        = new List<StudentAssignmentObligation>();
    /// <summary>
    /// Problem: The tracking view (REQ-EXH-029) needs to show “total number of students the assignment applies to”. Without a cached count, you must COUNT(*) over StudentAssignmentObligations. With 50k students, that’s an extra scan.
    /// </summary>
    public int? TotalStudentCount { get; set; }
}
