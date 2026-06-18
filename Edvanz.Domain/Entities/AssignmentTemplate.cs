using Edvanz.Domain.Entities.ShareProp;
using Edvanz.Domain.Enums;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Edvanz.Domain.Entities;

/// <summary>
/// Represents the canonical configuration of an exam or homework assignment created by a tutor.
/// One template generates one or many <see cref="AssignmentOccurrence"/> rows depending on its
/// recurrence pattern. The template itself never carries student state — that lives on
/// <see cref="StudentAssignmentObligation"/> rows tied to occurrences.
///
/// REQ-EXH-001: Two assignment types — Exam and Homework — share a common creation structure.
/// REQ-EXH-004: Mandatory bilingual name (English + Arabic).
/// REQ-EXH-006: Optional notes for tutor reference.
/// REQ-EXH-008/013: Recurrence configuration with edit restrictions after the first occurrence
/// has student data recorded.
/// REQ-EXH-NFR-004: All template data is scoped exclusively to the owning tutor.
///
/// HARD DELETE per REQ-EXH-037. There is no IsDeleted flag — deletion is final and NoActions
/// to all related occurrences and obligations. A JSON snapshot is captured in
/// <see cref="AssignmentDeletionLog"/> before deletion to preserve historical context.
/// </summary>
public class AssignmentTemplate : BaseEntity
{
    // ══════════════════════════════════════════════
    // OWNERSHIP & TENANT SCOPE
    // ══════════════════════════════════════════════

    /// <summary>
    /// Foreign key to the owning Teacher. All template data is scoped to this teacher.
    /// REQ-EXH-NFR-004: No template is visible to other tutor accounts.
    /// </summary>
    [ForeignKey(nameof(Teacher))]
    public long TeacherId { get; set; }
    public Teacher Teacher { get; set; } = null!;

    // ══════════════════════════════════════════════
    // TYPE & NAMING
    // ══════════════════════════════════════════════

    /// <summary>
    /// Whether this template is an Exam or a Homework assignment.
    /// REQ-EXH-001: Determines grading behavior and recurrence options.
    /// </summary>
    public AssignmentType AssignmentType { get; set; }

    /// <summary>
    /// English display name of the assignment.
    /// REQ-EXH-004: Mandatory; editable after creation per REQ-EXH-034.
    /// </summary>
    public string Name { get; set; } = null!;

    /// <summary>
    /// Arabic display name of the assignment.
    /// REQ-EXH-004: Mandatory; supports Egyptian Arabic dialect.
    /// REQ-EXH-NFR-003: Bilingual support across all assignment screens.
    /// </summary>
    public string NameAr { get; set; } = null!;

    /// <summary>
    /// Optional free-text notes or instructions.
    /// REQ-EXH-006: Additional context for the tutor's own reference.
    /// </summary>
    public string? Notes { get; set; }

    // ══════════════════════════════════════════════
    // RECURRENCE CONFIGURATION (REQ-EXH-008 through 013)
    // ══════════════════════════════════════════════

    /// <summary>
    /// Whether this template generates recurring occurrences or a single one-time occurrence.
    /// REQ-EXH-008: Decided at creation; editable subject to REQ-EXH-013 constraints.
    /// </summary>
    public bool IsRecurring { get; set; }

    /// <summary>
    /// The recurrence pattern. Valid values depend on AssignmentType:
    /// homework supports OneTime, EverySession, EveryTwoSessions (REQ-EXH-009);
    /// exam supports OneTime or Monthly (REQ-EXH-010).
    /// REQ-EXH-013: Not editable after the first occurrence has recorded student data.
    /// </summary>
    public RecurrencePattern RecurrencePattern { get; set; } = RecurrencePattern.OneTime;

    /// <summary>
    /// Optional end date for recurring assignments.
    /// Null means the recurrence continues until explicitly stopped via
    /// <see cref="IsRecurrenceStopped"/>.
    /// </summary>
    [Column(TypeName = "date")]
    public DateTime? RecurrenceEndDate { get; set; }

    /// <summary>
    /// Set to true when the tutor stops a recurring assignment from generating future occurrences.
    /// REQ-EXH-012: Stopping recurrence does not affect or delete previously recorded occurrences.
    /// The scheduler skips templates where this flag is true.
    /// </summary>
    public bool IsRecurrenceStopped { get; set; } = false;

    // ══════════════════════════════════════════════
    // HOMEWORK-SPECIFIC CONFIGURATION (REQ-EXH-014 through 017)
    // ══════════════════════════════════════════════

    /// <summary>
    /// The tracking mode for homework assignments. Null for exam templates.
    /// REQ-EXH-014/015: Selectable per homework; default is CompletionOnly.
    /// Service-layer validation enforces non-null when AssignmentType = Homework.
    /// </summary>
    public HomeworkTrackingMode? TrackingMode { get; set; }

    // ══════════════════════════════════════════════
    // EXAM-SPECIFIC CONFIGURATION (REQ-EXH-020 through 021)
    // ══════════════════════════════════════════════

    /// <summary>
    /// The maximum grade for an exam, used as the reference point for grade analysis.
    /// REQ-EXH-020: Defined during creation; editable per REQ-EXH-034.
    /// Null for homework templates and for completion-only contexts.
    /// Snapshotted onto <see cref="AssignmentOccurrence.MaxGradeSnapshot"/> at occurrence
    /// generation so historical reports remain stable when this value is later edited.
    /// </summary>
    [Column(TypeName = "decimal(8,2)")]
    public decimal? MaxGrade { get; set; }

    /// <summary>
    /// The passing grade threshold for an exam.
    /// REQ-EXH-021: Used to flag students who scored below it.
    /// REQ-EXH-044: Drives the Below Passing Grade report.
    /// Null for homework templates. Snapshotted onto the occurrence row, same rationale
    /// as <see cref="MaxGrade"/>.
    /// </summary>
    [Column(TypeName = "decimal(8,2)")]
    public decimal? PassingThreshold { get; set; }

    // ══════════════════════════════════════════════
    // AUDIT
    // ══════════════════════════════════════════════

    /// <summary>
    /// The User who created this template.
    /// </summary>
    [ForeignKey(nameof(CreatedByUser))]
    public long CreatedByUserId { get; set; }
    public User CreatedByUser { get; set; } = null!;

    /// <summary>
    /// Last update timestamp. Maintained by the service layer on every edit.
    /// REQ-EXH-034: Templates are editable after creation, subject to recurrence restrictions.
    /// </summary>
    public DateTime? UpdatedAt { get; set; }

    // ══════════════════════════════════════════════
    // CONCURRENCY
    // ══════════════════════════════════════════════

    /// <summary>
    /// Optimistic concurrency token. Prevents lost updates when concurrent requests
    /// edit the same template (e.g., tutor renaming while assistant adjusts notes).
    /// Same pattern as <see cref="PaymentTransaction.RowVersion"/>.
    /// </summary>
    [Timestamp]
    public byte[] RowVersion { get; set; } = null!;

    // ══════════════════════════════════════════════
    // NAVIGATION PROPERTIES
    // ══════════════════════════════════════════════

    /// <summary>
    /// All scope rows targeting this template. A template may have many scopes which
    /// are unioned and deduplicated when generating obligations (REQ-EXH-003).
    /// </summary>
    public ICollection<AssignmentScope> Scopes { get; set; } = new List<AssignmentScope>();

    /// <summary>
    /// All occurrences generated from this template. One-time templates have exactly one;
    /// recurring templates accumulate occurrences over time.
    /// </summary>
    public ICollection<AssignmentOccurrence> Occurrences { get; set; } = new List<AssignmentOccurrence>();
}
