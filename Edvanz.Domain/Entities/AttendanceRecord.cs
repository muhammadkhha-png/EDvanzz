using Edvanz.Domain.Entities.ShareProp;
using Edvanz.Domain.Enums;
using System.ComponentModel.DataAnnotations.Schema;

namespace Edvanz.Domain.Entities;

/// <summary>
/// The core attendance fact table. One row per student per session occurrence.
/// Records whether a student was Present, Absent, or Held for a specific session date.
/// 
/// REQ-ATT-006/007: Unified record regardless of attendance method.
/// REQ-ATT-017: Supports cross-session attendance via AttendedSessionOccurrenceId.
/// REQ-ATT-025/BR-ATT-006: Original RecordedAt is immutable; edits logged in AttendanceEditLogs.
/// BR-ATT-002: Unique constraint on (SessionOccurrenceId, TeacherStudentId) prevents duplicates.
/// BR-ATT-005: Attendance records survive session deletion via denormalized OccurrenceDate
///             and SET NULL on SessionOccurrenceId FK.
/// REQ-ATT-NFR-004: RecordedAt stored with minute precision.
/// REQ-ATT-NFR-003: All data scoped to the individual tutor account via TeacherId.
/// 
/// Multi-tenant isolation: TeacherId is the tenant key.
/// </summary>
public class AttendanceRecord : BaseEntity
{
    /// <summary>
    /// Foreign key to the owning Teacher. Tenant isolation key.
    /// CASCADE delete when teacher account is removed.
    /// </summary>
    [ForeignKey(nameof(Teacher))]
    public long TeacherId { get; set; }
    public Teacher Teacher { get; set; } = null!;

    /// <summary>
    /// Foreign key to the student being tracked.
    /// RESTRICT delete: attendance must be explicitly cleaned before permanent student purge.
    /// This ensures cumulative absence counts remain accurate during the retention window.
    /// </summary>
    [ForeignKey(nameof(TeacherStudent))]
    public long TeacherStudentId { get; set; }
    public TeacherStudent TeacherStudent { get; set; } = null!;

    /// <summary>
    /// Foreign key to the assignment period this record belongs to.
    /// Partitions attendance history per assignment period for efficient per-session history.
    /// REQ-ATT-020/044: Attendance split by assignment period.
    /// CASCADE delete: if the assignment record is removed, its attendance follows.
    /// </summary>
    [ForeignKey(nameof(StudentSessionAssignment))]
    public long StudentSessionAssignmentId { get; set; }
    public StudentSessionAssignment StudentSessionAssignment { get; set; } = null!;

    /// <summary>
    /// Foreign key to the session occurrence this attendance is for.
    /// SET NULL when the session (and its occurrences) is hard-deleted.
    /// BR-ATT-005: The record survives with OccurrenceDate still populated.
    /// </summary>
    [ForeignKey(nameof(SessionOccurrence))]
    public long? SessionOccurrenceId { get; set; }
    public SessionOccurrence? SessionOccurrence { get; set; }

    /// <summary>
    /// Denormalized occurrence date from SessionOccurrences.OccurrenceDate.
    /// Ensures the date is retained even if the session and its occurrences are deleted.
    /// BR-ATT-005: Attendance records for a deleted session are retained.
    /// REQ-ATT-NFR-004: Date precision for historical reporting.
    /// </summary>
    [Column(TypeName = "date")]
    public DateTime OccurrenceDate { get; set; }

    /// <summary>
    /// For cross-session attendance: the occurrence the student physically attended
    /// if different from their assigned session's occurrence.
    /// NULL = student attended their own session's occurrence normally.
    /// REQ-ATT-017: Captures the session they physically attended.
    /// SET NULL when the attended session is deleted.
    /// </summary>
    [ForeignKey(nameof(AttendedSessionOccurrence))]
    public long? AttendedSessionOccurrenceId { get; set; }
    public SessionOccurrence? AttendedSessionOccurrence { get; set; }

    /// <summary>
    /// The student's attendance status for this occurrence.
    /// REQ-ATT-006: Present or Absent.
    /// REQ-ATT-061: Held = deferred during attendance-taking.
    /// </summary>
    public AttendanceStatus Status { get; set; }

    /// <summary>
    /// How this attendance record was created.
    /// REQ-ATT-006: ManualCode, MultiSelect, or BarcodeScan.
    /// REQ-ATT-023: ManualEdit for records created/modified via Edit Attendance.
    /// REQ-ATT-055: MarkAllPresent for bulk present marking.
    /// </summary>
    public AttendanceMethod AttendanceMethod { get; set; }

    /// <summary>
    /// Quick flag for cross-session attendance events.
    /// TRUE when the student attended a linked session instead of their own.
    /// REQ-ATT-017: Cross-session attendance event.
    /// BR-ATT-003: Only permitted between membership-linked sessions.
    /// </summary>
    public bool IsCrossSession { get; set; } = false;

    /// <summary>
    /// Timestamp when the attendance was originally recorded.
    /// REQ-ATT-NFR-004: Minute precision.
    /// REQ-ATT-025/BR-ATT-006: Immutable — edits do NOT change this value.
    /// </summary>
    public DateTime RecordedAt { get; set; }

    /// <summary>
    /// Foreign key to the user who recorded this attendance entry.
    /// May be the teacher or an assistant.
    /// SET NULL if the recording user is deleted.
    /// </summary>
    [ForeignKey(nameof(RecordedByUser))]
    public long? RecordedByUserId { get; set; }
    public User? RecordedByUser { get; set; }

    /// <summary>
    /// Navigation property: edit history for this attendance record.
    /// REQ-ATT-025: All edits logged as modifications alongside the original entry.
    /// </summary>
    public ICollection<AttendanceEditLog> EditLogs { get; set; } = new List<AttendanceEditLog>();
}