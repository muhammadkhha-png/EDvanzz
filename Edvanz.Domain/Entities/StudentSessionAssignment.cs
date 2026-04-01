using Edvanz.Domain.Entities.ShareProp;
using System.ComponentModel.DataAnnotations.Schema;

namespace Edvanz.Domain.Entities;

/// <summary>
/// Tracks the full history of a student's assignment to sessions over time.
/// REQ-ATT-019: Attendance starts from AssignedAt date, not student creation date.
/// REQ-ATT-020: Separate histories per assignment period, independently viewable.
/// REQ-ATT-043/044: History permanently preserved across reassignment and session deletion.
/// REQ-ATT-045: New assignment creates a new record starting from reassignment date.
/// REQ-ATT-046: Chronological timeline across all session periods.
/// REQ-ATT-048: Re-assignment to a previously attended session creates a NEW period.
/// BR-ATT-001: No retroactive attendance before AssignedAt.
/// BR-ATT-005: SessionName denormalized so records survive session hard-delete.
///
/// Multi-tenant isolation: TeacherId stored directly for tenant-scoped index performance.
/// </summary>
public class StudentSessionAssignment : BaseEntity
{
    /// <summary>
    /// Foreign key to the owning Teacher. Stored directly for tenant-scoped index performance.
    /// REQ-ATT-NFR-003: All data scoped to individual tutor account.
    /// </summary>
    [ForeignKey(nameof(Teacher))]
    public long TeacherId { get; set; }
    public Teacher Teacher { get; set; } = null!;

    /// <summary>
    /// Foreign key to the student record.
    /// NO ACTION on delete: assignment history preserved after student soft/hard delete (BR-ATT-005).
    /// </summary>
    [ForeignKey(nameof(TeacherStudent))]
    public long TeacherStudentId { get; set; }
    public TeacherStudent TeacherStudent { get; set; } = null!;

    /// <summary>
    /// Foreign key to the session assigned to.
    /// Nullable: set to null by application logic when session is hard-deleted (BR-ATT-005).
    /// NO ACTION on delete: application handles cleanup before session deletion.
    /// </summary>
    public long? SessionId { get; set; }

    /// <summary>
    /// Navigation property to the assigned Session.
    /// Nullable because session can be hard-deleted while assignment history is preserved.
    /// </summary>
    [ForeignKey(nameof(SessionId))]
    public Session? Session { get; set; }

    /// <summary>
    /// Snapshot of the session name at assignment time.
    /// BR-ATT-005: Survives session hard-deletion — ensures historical display is always possible.
    /// REQ-ATT-044: Displayed as label on the student's attendance profile.
    /// </summary>
    public string SessionName { get; set; } = null!;

    /// <summary>
    /// When the student was assigned to this session.
    /// REQ-ATT-019/BR-ATT-001: Attendance obligation starts from this date.
    /// </summary>
    public DateTime AssignedAt { get; set; }

    /// <summary>
    /// When the student left this session. NULL means currently active.
    /// REQ-ATT-020: Set when student is reassigned to a different session.
    /// </summary>
    public DateTime? UnassignedAt { get; set; }

    /// <summary>
    /// Convenience flag: true when UnassignedAt is null (currently active assignment).
    /// REQ-ATT-046: Used to quickly identify the current assignment period.
    /// </summary>
    public bool IsActive { get; set; } = true;

    // Navigation property
    public ICollection<AttendanceRecord> AttendanceRecords { get; set; } = new List<AttendanceRecord>();
}