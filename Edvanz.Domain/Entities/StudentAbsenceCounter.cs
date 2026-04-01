using Edvanz.Domain.Entities.ShareProp;
using Edvanz.Domain.Enums;
using System.ComponentModel.DataAnnotations.Schema;

namespace Edvanz.Domain.Entities;

/// <summary>
/// Denormalized running counters for consecutive and cumulative absences per student.
/// These counters are maintained in real-time (updated in the same transaction as
/// each attendance write) to support sub-second attendance operations.
/// 
/// REQ-ATT-021/047: Cumulative total absence count — permanent, never resets.
/// REQ-ATT-029/030: Consecutive absence counter — resets to zero on any presence.
/// REQ-ATT-031: Both counters preserved across session reassignments.
/// REQ-ATT-027/028: Last attendance info for immediate absent-student alerts.
/// REQ-ATT-038/039: Barcode scan (&lt;1s) and manual entry (&lt;500ms) need instant lookup.
/// REQ-ATT-067: Absence Overview sorts by consecutive absences descending.
/// BR-ATT-004: Consecutive counter is independent from cumulative count.
/// 
/// DESIGN RATIONALE:
/// Computing these values from the full AttendanceRecords table on every scan/entry
/// would require aggregation queries across potentially millions of rows at scale
/// (50K students × 100+ occurrences). The denormalized counters provide O(1) reads.
/// 
/// Multi-tenant isolation: TeacherId is the tenant key.
/// One row per student (UNIQUE on TeacherStudentId).
/// </summary>
public class StudentAbsenceCounter : BaseEntity
{
    /// <summary>
    /// Foreign key to the owning Teacher. Tenant isolation key.
    /// CASCADE delete when teacher account is removed.
    /// </summary>
    [ForeignKey(nameof(Teacher))]
    public long TeacherId { get; set; }
    public Teacher Teacher { get; set; } = null!;

    /// <summary>
    /// Foreign key to the student. One counter row per student.
    /// CASCADE delete: if the student is permanently purged, the counter is removed.
    /// </summary>
    [ForeignKey(nameof(TeacherStudent))]
    public long TeacherStudentId { get; set; }
    public TeacherStudent TeacherStudent { get; set; } = null!;

    /// <summary>
    /// Rolling consecutive absence counter.
    /// REQ-ATT-029: Incremented each time the student is marked absent.
    /// REQ-ATT-030: Resets to zero the moment the student is marked present.
    /// REQ-ATT-031: Preserved across session reassignments.
    /// BR-ATT-004: Independent from CumulativeTotalAbsences.
    /// </summary>
    public int ConsecutiveAbsences { get; set; } = 0;

    /// <summary>
    /// Permanent cumulative total of all absences across all sessions and all time.
    /// REQ-ATT-021: Aggregates absences across all sessions the student has ever been assigned to.
    /// REQ-ATT-047: Never lost, reset, or recalculated from zero upon reassignment.
    /// </summary>
    public int CumulativeTotalAbsences { get; set; } = 0;

    /// <summary>
    /// Status of the student's most recent attendance record.
    /// Used for quick "was absent last session?" check during attendance-taking.
    /// REQ-ATT-027: Check attendance for the immediately preceding occurrence.
    /// </summary>
    public AttendanceStatus? LastAttendanceStatus { get; set; }

    /// <summary>
    /// Date of the student's most recent attendance record.
    /// Displayed in the absent-student alert notification.
    /// REQ-ATT-058: Shows "the date of their last absence".
    /// </summary>
    [Column(TypeName = "date")]
    public DateTime? LastAttendanceDate { get; set; }

    /// <summary>
    /// Session name where the last attendance was recorded.
    /// For cross-session context in alerts.
    /// REQ-ATT-060: Identifies the linked session by name in the alert prompt.
    /// </summary>
    public string? LastAttendanceSessionName { get; set; }
}