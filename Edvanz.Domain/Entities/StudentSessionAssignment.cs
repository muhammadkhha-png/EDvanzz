using Edvanz.Domain.Entities.ShareProp;
using System.ComponentModel.DataAnnotations.Schema;

namespace Edvanz.Domain.Entities;

/// <summary>
/// Tracks the complete history of a student's assignments to sessions over time.
/// The existing TeacherStudents.SessionId stores only the CURRENT session — it has
/// no memory of prior assignments. This table is the "assignment timeline" that
/// preserves the full history.
/// 
/// REQ-ATT-019/BR-ATT-001: Attendance history begins from the assignment date, not creation date.
/// REQ-ATT-020/043: Reassignment preserves complete attendance history under the previous session.
/// REQ-ATT-044: History labeled with original session name and date range.
/// REQ-ATT-045: New assignment creates a fresh attendance timeline from reassignment date.
/// REQ-ATT-046: All assignment periods displayed in chronological timeline.
/// REQ-ATT-048: Re-assignment to a previously attended session creates a NEW period.
/// BR-ATT-005: Assignment row survives session deletion (SessionId set to NULL).
/// 
/// Multi-tenant isolation: TeacherId is the tenant key.
/// </summary>
public class StudentSessionAssignment : BaseEntity
{
    /// <summary>
    /// Foreign key to the owning Teacher. Tenant isolation key.
    /// CASCADE delete when teacher account is removed.
    /// </summary>
    [ForeignKey(nameof(Teacher))]
    public long TeacherId { get; set; }
    public Teacher Teacher { get; set; } = null!;

    /// <summary>
    /// Foreign key to the student record.
    /// CASCADE delete: if the student is permanently purged, assignment history is removed.
    /// </summary>
    [ForeignKey(nameof(TeacherStudent))]
    public long TeacherStudentId { get; set; }
    public TeacherStudent TeacherStudent { get; set; } = null!;

    /// <summary>
    /// Foreign key to the session the student was assigned to.
    /// SET NULL when the session is hard-deleted — the assignment row and its
    /// attendance records are preserved per BR-ATT-005.
    /// </summary>
    [ForeignKey(nameof(Session))]
    public long? SessionId { get; set; }
    public Session? Session { get; set; }

    /// <summary>
    /// Snapshot of the session name at the time of assignment.
    /// Ensures the history remains readable even if the session is later deleted or renamed.
    /// REQ-ATT-044: History labeled with the original session name.
    /// </summary>
    public string SessionNameSnapshot { get; set; } = null!;

    /// <summary>
    /// Timestamp when the student was assigned to this session.
    /// REQ-ATT-019: Attendance obligation starts from this date.
    /// BR-ATT-001: No retroactive attendance records before this date.
    /// </summary>
    public DateTime AssignedAt { get; set; }

    /// <summary>
    /// Timestamp when the student was unassigned or reassigned away from this session.
    /// NULL if this is the currently active assignment.
    /// REQ-ATT-020: The previous assignment's history is preserved from AssignedAt to UnassignedAt.
    /// </summary>
    public DateTime? UnassignedAt { get; set; }

    /// <summary>
    /// Quick filter: only one active assignment per student at a time.
    /// TRUE = current assignment; FALSE = historical (student was reassigned or unassigned).
    /// Enforced by a filtered unique index on (TeacherStudentId) WHERE IsActive = 1.
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Navigation property: attendance records within this assignment period.
    /// REQ-ATT-044: Independently viewable per assignment period.
    /// </summary>
    public ICollection<AttendanceRecord> AttendanceRecords { get; set; } = new List<AttendanceRecord>();
}