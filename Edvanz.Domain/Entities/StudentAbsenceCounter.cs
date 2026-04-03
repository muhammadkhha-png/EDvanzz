using Edvanz.Domain.Entities.ShareProp;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Edvanz.Domain.Entities;

/// <summary>
/// Materialized aggregate counters per student per teacher for instant absence lookups.
/// REQ-ATT-021: Cumulative total absence counter across all sessions (never resets).
/// REQ-ATT-029/030: Consecutive absence streak — resets to 0 on any presence.
/// REQ-ATT-031: Counters preserved across session reassignments.
/// REQ-ATT-028: LastAbsenceDate used for "absent in last session" alerts during attendance taking.
/// REQ-ATT-047: TotalAbsences continues aggregating without interruption across reassignments.
/// REQ-ATT-067: Sorted by ConsecutiveAbsences DESC for absence overview.
/// REQ-ATT-078: TotalOccurrences/TotalPresent enable attendance percentage calculation.
///
/// PERFORMANCE RATIONALE:
/// At 50K concurrent users, REQ-ATT-028/029 requires checking consecutive absences on every
/// single attendance mark (each barcode scan, each manual code entry). Scanning AttendanceRecords
/// with ORDER BY DESC per student per scan would be O(N). A single-row counter table makes it O(1).
///
/// COUNTER UPDATE RULES:
/// - Mark Present: ConsecutiveAbsences = 0, TotalPresent++, TotalOccurrences++
/// - Mark Absent:  ConsecutiveAbsences++, TotalAbsences++, TotalOccurrences++
/// - Edit Present→Absent: TotalPresent--, TotalAbsences++, recalculate consecutive from records
/// - Edit Absent→Present: TotalAbsences--, TotalPresent++, recalculate consecutive from records
/// </summary>
public class StudentAbsenceCounter : BaseEntity
{
    /// <summary>
    /// Foreign key to the owning Teacher. Stored directly for tenant-scoped index performance.
    /// REQ-ATT-NFR-003: All attendance data scoped to individual tutor account.
    /// </summary>
    [ForeignKey(nameof(Teacher))]
    public long TeacherId { get; set; }
    public Teacher Teacher { get; set; } = null!;

    /// <summary>
    /// Foreign key to the student record.
    /// NO ACTION on delete: cleaned up via application logic on permanent purge.
    /// One counter per student per teacher (unique constraint enforced in DbContext).
    /// </summary>
    [ForeignKey(nameof(TeacherStudent))]
    public long TeacherStudentId { get; set; }
    public TeacherStudent TeacherStudent { get; set; } = null!;

    /// <summary>
    /// Rolling counter: number of consecutive session occurrences the student was absent.
    /// REQ-ATT-029: Displayed as "absent for the last N consecutive sessions".
    /// REQ-ATT-030: Resets to 0 the moment student is marked present.
    /// REQ-ATT-031: Preserved across session reassignments.
    /// BR-ATT-004: Independent from TotalAbsences — this resets, TotalAbsences never does.
    /// </summary>
    public int ConsecutiveAbsences { get; set; } = 0;

    /// <summary>
    /// Cumulative total absences across all sessions the student has ever been assigned to.
    /// REQ-ATT-021/047: Never resets, never lost when student moves between sessions.
    /// BR-ATT-004: Permanent, never resets.
    /// </summary>
    public int TotalAbsences { get; set; } = 0;

    /// <summary>
    /// Cumulative total present count across all sessions.
    /// REQ-ATT-078: Used for overall attendance percentage calculation.
    /// </summary>
    public int TotalPresent { get; set; } = 0;

    /// <summary>
    /// Total occurrences the student was obligated to attend.
    /// REQ-ATT-078: Denominator for attendance percentage.
    /// </summary>
    public int TotalOccurrences { get; set; } = 0;

    /// <summary>
    /// Date of the most recent absence.
    /// REQ-ATT-028: "The date they were last absent" shown in alert during attendance taking.
    /// </summary>
    [Column(TypeName = "date")]
    public DateTime? LastAbsenceDate { get; set; }

    /// <summary>
    /// Session name where the last absence occurred.
    /// REQ-ATT-060: Cross-session absence alert identifies the linked session by name.
    /// </summary>
    public string? LastAbsenceSessionName { get; set; }

    /// <summary>
    /// Session Id where the last absence occurred.
    /// Used to determine if the absence was in a cross-session context (REQ-ATT-060).
    /// </summary>
    public long? LastAbsenceSessionId { get; set; }

    /// <summary>
    /// Date of the most recent presence.
    /// Used for streak calculation validation during edit operations.
    /// </summary>
    [Column(TypeName = "date")]
    public DateTime? LastAttendanceDate { get; set; }

    /// <summary>
    /// Optimistic concurrency token. Prevents lost updates when multiple
    /// concurrent requests modify the same counter (e.g., two assistants
    /// taking attendance for linked sessions simultaneously).
    /// Audit Fix: Added to prevent counter race conditions at scale.
    /// </summary>
    [Timestamp]
    public byte[] RowVersion { get; set; } = null!;
}