using Edvanz.Domain.Entities.ShareProp;
using Edvanz.Domain.Enums;
using System.ComponentModel.DataAnnotations.Schema;

namespace Edvanz.Domain.Entities;

/// <summary>
/// Core attendance fact table. One row per student per session occurrence.
/// REQ-ATT-006/007: All three attendance methods produce the same record type.
/// REQ-ATT-017: Cross-session events captured via IsCrossSession and related fields.
/// REQ-ATT-024/025: Editable with audit trail via IsEdited and AttendanceEditLogs.
/// BR-ATT-002: Duplicate prevention via unique constraint (TeacherStudentId, SessionOccurrenceId).
/// BR-ATT-005: Denormalized SessionId/SessionName/OccurrenceDate AND StudentName/StudentCode
///             survive both session hard-delete and student permanent purge.
/// BR-ATT-006: Original RecordedAt never altered; edits logged separately.
/// REQ-ATT-NFR-004: RecordedAt stored with datetime2(0) precision (to the second).
///
/// DENORMALIZATION RATIONALE (SessionId, SessionName, OccurrenceDate, StudentName, StudentCode):
/// Sessions use hard delete (BR-SES-004). Students use permanent purge after 10-day recycle bin.
/// BR-ATT-005 mandates attendance records survive both session deletion and student purge.
/// These denormalized fields make every record self-describing after its parent entities
/// are destroyed.
///
/// Multi-tenant isolation: TeacherId stored directly for tenant-scoped indexes.
/// </summary>
public class AttendanceRecord : BaseEntity
{
    // ══════════════════════════════════════════════
    // TENANT ISOLATION
    // ══════════════════════════════════════════════

    /// <summary>
    /// Foreign key to the owning Teacher. Stored directly for tenant-scoped index performance.
    /// REQ-ATT-NFR-003: All attendance data scoped to individual tutor account.
    /// </summary>
    [ForeignKey(nameof(Teacher))]
    public long TeacherId { get; set; }
    public Teacher Teacher { get; set; } = null!;

    // ══════════════════════════════════════════════
    // OCCURRENCE REFERENCE
    // ══════════════════════════════════════════════

    /// <summary>
    /// Foreign key to the materialized occurrence.
    /// SET NULL when session (and its occurrences) are deleted.
    /// The denormalized OccurrenceDate field preserves the date after deletion.
    /// </summary>
    public long? SessionOccurrenceId { get; set; }

    /// <summary>
    /// Navigation to the session occurrence. Nullable after session deletion.
    /// </summary>
    [ForeignKey(nameof(SessionOccurrenceId))]
    public SessionOccurrence? SessionOccurrence { get; set; }

    // ══════════════════════════════════════════════
    // STUDENT REFERENCE (Step 1.1: Nullable FK for purge safety)
    // ══════════════════════════════════════════════

    /// <summary>
    /// Foreign key to the student record.
    /// Step 1.1: Changed from long to long? — SET NULL on student permanent purge.
    /// BR-ATT-005: Records preserved after student soft/hard delete.
    /// The denormalized StudentName and StudentCode fields preserve display data.
    /// </summary>
    [ForeignKey(nameof(TeacherStudent))]
    public long? TeacherStudentId { get; set; }

    /// <summary>
    /// Navigation to the student record. Nullable after student permanent purge.
    /// </summary>
    public TeacherStudent? TeacherStudent { get; set; }

    /// <summary>
    /// Links to the specific assignment period for this attendance record.
    /// REQ-ATT-020/046: Enables grouping records by assignment period in the timeline.
    /// Step 1.1: Changed from long to long? — SET NULL on assignment cleanup during purge.
    /// </summary>
    [ForeignKey(nameof(StudentSessionAssignment))]
    public long? StudentSessionAssignmentId { get; set; }

    /// <summary>
    /// Navigation to the assignment period. Nullable after student purge or session deletion.
    /// </summary>
    public StudentSessionAssignment? StudentSessionAssignment { get; set; }

    // ══════════════════════════════════════════════
    // DENORMALIZED STUDENT CONTEXT (Step 7.2 — BR-ATT-005)
    // ══════════════════════════════════════════════

    /// <summary>
    /// Denormalized: snapshot of student name at recording time.
    /// BR-ATT-005 / Step 7.2: Enables display after student permanent purge.
    /// </summary>
    public string? StudentName { get; set; }

    /// <summary>
    /// Denormalized: snapshot of student code at recording time.
    /// BR-ATT-005 / Step 7.2: Enables display after student permanent purge.
    /// </summary>
    public string? StudentCode { get; set; }

    // ══════════════════════════════════════════════
    // DENORMALIZED SESSION CONTEXT (BR-ATT-005)
    // ══════════════════════════════════════════════

    /// <summary>
    /// Denormalized: the session this occurrence belonged to.
    /// Survives session deletion. Nullable only if session was deleted.
    /// </summary>
    public long? SessionId { get; set; }

    /// <summary>
    /// Denormalized: snapshot of session name at recording time.
    /// BR-ATT-005: Enables display after session hard-deletion.
    /// </summary>
    public string SessionName { get; set; } = null!;

    /// <summary>
    /// Denormalized: the occurrence date.
    /// Enables date-based queries after session/occurrence deletion.
    /// REQ-ATT-NFR-004: date precision.
    /// </summary>
    [Column(TypeName = "date")]
    public DateTime OccurrenceDate { get; set; }

    /// <summary>
    /// FIX H3: Denormalized session group Id at recording time.
    /// REQ-ATT-040 Type 5 (SessionGroupAttendance) requires filtering by session group.
    /// After session hard-delete, SessionOccurrence is NoAction-deleted and
    /// the navigation path r.SessionOccurrence.Session.SessionGroupId becomes null.
    /// This denormalized field enables Report Type 5 to include records from deleted sessions,
    /// satisfying BR-ATT-005.
    /// </summary>
    public long? SessionGroupId { get; set; }

    // ══════════════════════════════════════════════
    // ATTENDANCE DATA
    // ══════════════════════════════════════════════

    /// <summary>
    /// The student's attendance status for this occurrence.
    /// REQ-ATT-006: 0=Absent, 1=Present, 2=CrossSessionPresent, 3=Held.
    /// REQ-ATT-024: Changeable via Edit Attendance (logged in AttendanceEditLogs).
    /// </summary>
    public AttendanceStatus Status { get; set; }

    /// <summary>
    /// Which method was used to record this attendance.
    /// REQ-ATT-006: ManualCode, MultiSelect, or BarcodeScan.
    /// REQ-ATT-007: All methods produce the same record type.
    /// </summary>
    public AttendanceMethod AttendanceMethod { get; set; }

    // ══════════════════════════════════════════════
    // CROSS-SESSION FIELDS (REQ-ATT-014 through 018)
    // ══════════════════════════════════════════════

    /// <summary>
    /// Whether this is a cross-session attendance event.
    /// REQ-ATT-017: Student attended a linked session instead of their assigned session.
    /// </summary>
    public bool IsCrossSession { get; set; } = false;

    /// <summary>
    /// If cross-session: the session the student physically attended.
    /// Nullable: only set when IsCrossSession is true.
    /// </summary>
    public long? CrossSessionId { get; set; }

    /// <summary>
    /// Denormalized: name of the session the student physically attended.
    /// REQ-ATT-018: "Attended in Session A on Saturday" display.
    /// </summary>
    public string? CrossSessionName { get; set; }

    /// <summary>
    /// The actual date the student attended in the linked session.
    /// REQ-ATT-018: Cross-session mapping — the physical attendance date.
    /// </summary>
    [Column(TypeName = "date")]
    public DateTime? CrossSessionOccurrenceDate { get; set; }

    // ══════════════════════════════════════════════
    // RECORDING METADATA
    // ══════════════════════════════════════════════

    /// <summary>
    /// Timestamp of original recording.
    /// REQ-ATT-NFR-004: Precision to the second (datetime2(0)).
    /// BR-ATT-006: Never altered — edits are logged in AttendanceEditLogs.
    /// </summary>
    public DateTime RecordedAt { get; set; }

    /// <summary>
    /// Who recorded this attendance. FK to Users table.
    /// Nullable for system-generated records (e.g., bulk "Mark All Present").
    /// </summary>
    public long? RecordedByUserId { get; set; }

    // ══════════════════════════════════════════════
    // EDIT TRACKING (REQ-ATT-025 / BR-ATT-006)
    // ══════════════════════════════════════════════

    /// <summary>
    /// Has this record been modified after initial creation?
    /// REQ-ATT-025: Differentiates original from modified records.
    /// </summary>
    public bool IsEdited { get; set; } = false;

    /// <summary>
    /// Timestamp of the most recent edit.
    /// REQ-ATT-025: Edit timestamp preserved for audit.
    /// </summary>
    public DateTime? LastEditedAt { get; set; }

    /// <summary>
    /// Who made the last edit. FK to Users table.
    /// </summary>
    public long? LastEditedByUserId { get; set; }

    // Navigation property
    public ICollection<AttendanceEditLog> EditLogs { get; set; } = new List<AttendanceEditLog>();
}