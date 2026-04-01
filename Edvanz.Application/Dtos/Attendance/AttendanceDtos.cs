using Edvanz.Domain.Enums;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace Edvanz.Application.Dtos.Attendance;

// ══════════════════════════════════════════════
// ATTENDANCE TAKING INPUT DTOs
// ══════════════════════════════════════════════

/// <summary>
/// Input DTO for recording a single student's attendance via manual code entry.
/// REQ-ATT-006 Method 1: Manual Code Entry.
/// </summary>
public class RecordAttendanceByCodeDto
{
    [Required]
    public long TeacherId { get; set; }

    [Required]
    public long SessionId { get; set; }

    /// <summary>
    /// The student code to look up. Case-insensitive matching.
    /// </summary>
    [Required]
    public string StudentCode { get; set; } = null!;

    /// <summary>
    /// Optional: the user Id recording the attendance (teacher or assistant).
    /// </summary>
    public long? RecordedByUserId { get; set; }
}

/// <summary>
/// Input DTO for recording attendance via barcode scan.
/// REQ-ATT-006 Method 3: Barcode Scanning.
/// REQ-ATT-012: Resolve barcode within 1 second.
/// </summary>
public class RecordAttendanceByBarcodeDto
{
    [Required]
    public long TeacherId { get; set; }

    [Required]
    public long SessionId { get; set; }

    /// <summary>
    /// The scanned barcode data. Encodes the student code (REQ-ATT-009).
    /// </summary>
    [Required]
    public string BarcodeData { get; set; } = null!;

    public long? RecordedByUserId { get; set; }
}

/// <summary>
/// Input DTO for recording attendance via multi-select.
/// REQ-ATT-006 Method 2: Multi-Select from Student List.
/// </summary>
public class RecordAttendanceMultiSelectDto
{
    [Required]
    public long TeacherId { get; set; }

    [Required]
    public long SessionId { get; set; }

    /// <summary>
    /// List of student IDs to mark with the specified status.
    /// </summary>
    [Required]
    public List<long> TeacherStudentIds { get; set; } = new();

    /// <summary>
    /// The status to apply to all selected students.
    /// </summary>
    [Required]
    public AttendanceStatus Status { get; set; }

    public long? RecordedByUserId { get; set; }
}

/// <summary>
/// Input DTO for the "Mark All Present" action.
/// REQ-ATT-055: Marks all unmarked students as present in a single action.
/// </summary>
public class MarkAllPresentDto
{
    [Required]
    public long TeacherId { get; set; }

    [Required]
    public long SessionId { get; set; }

    public long? RecordedByUserId { get; set; }
}

// ══════════════════════════════════════════════
// ATTENDANCE EDITING INPUT DTOs
// ══════════════════════════════════════════════

/// <summary>
/// Input DTO for editing an existing attendance record or adding one for a past/future date.
/// REQ-ATT-023/024: Edit Attendance view.
/// REQ-ATT-026: Pre-record attendance for future occurrences.
/// </summary>
public class EditAttendanceDto
{
    [Required]
    public long TeacherId { get; set; }

    [Required]
    public long SessionId { get; set; }

    /// <summary>
    /// The occurrence date to edit attendance for.
    /// REQ-ATT-023: Any past or future session occurrence date.
    /// </summary>
    [Required]
    public DateTime OccurrenceDate { get; set; }

    /// <summary>
    /// List of student attendance entries to create or modify.
    /// </summary>
    [Required]
    public List<EditAttendanceEntryDto> Entries { get; set; } = new();

    public long? EditedByUserId { get; set; }

    /// <summary>
    /// Optional reason for the edit (audit trail).
    /// </summary>
    public string? EditReason { get; set; }
}

/// <summary>
/// A single student's attendance entry within an edit operation.
/// </summary>
public class EditAttendanceEntryDto
{
    [Required]
    public long TeacherStudentId { get; set; }

    [Required]
    public AttendanceStatus Status { get; set; }
}

// ══════════════════════════════════════════════
// ATTENDANCE OUTPUT DTOs
// ══════════════════════════════════════════════

/// <summary>
/// Output DTO for a single attendance record.
/// Used in attendance history, timeline, and report views.
/// </summary>
public class AttendanceRecordDto
{
    public long Id { get; set; }
    public long TeacherStudentId { get; set; }
    public string StudentName { get; set; } = null!;
    public string StudentCode { get; set; } = null!;
    public long? SessionOccurrenceId { get; set; }
    public DateTime OccurrenceDate { get; set; }
    public AttendanceStatus Status { get; set; }
    public AttendanceMethod AttendanceMethod { get; set; }
    public bool IsCrossSession { get; set; }

    /// <summary>
    /// Name of the session the student physically attended (for cross-session events).
    /// Null if the student attended their own session normally.
    /// REQ-ATT-018: Labeled as "Attended in Session X on Date Y".
    /// </summary>
    public string? AttendedSessionName { get; set; }

    public DateTime RecordedAt { get; set; }
    public bool HasBeenEdited { get; set; }
}

/// <summary>
/// Output DTO for the Take Attendance screen's student entry.
/// REQ-ATT-008: Shows which students have been marked and which remain unmarked.
/// REQ-ATT-054: Unmarked students displayed first.
/// REQ-ATT-071: Already-marked students show a visible indicator.
/// </summary>
public class TakeAttendanceStudentDto
{
    public long TeacherStudentId { get; set; }
    public string StudentName { get; set; } = null!;
    public string StudentCode { get; set; } = null!;
    public string? Barcode { get; set; }

    /// <summary>
    /// Current attendance status for this occurrence. Null if unmarked.
    /// </summary>
    public AttendanceStatus? CurrentStatus { get; set; }

    /// <summary>
    /// True if this student is from a linked (membership) session, not the primary session.
    /// REQ-ATT-014: Secondary panel distinction.
    /// </summary>
    public bool IsFromLinkedSession { get; set; }

    /// <summary>
    /// Name of the linked session this student belongs to (if IsFromLinkedSession).
    /// REQ-ATT-015: Labeled with the linked session name.
    /// </summary>
    public string? LinkedSessionName { get; set; }
}

/// <summary>
/// Result DTO for a single attendance recording operation.
/// Contains the alert information for absent-student detection.
/// REQ-ATT-027/028/057/058: Absent-student alert on attendance recording.
/// REQ-ATT-069/070: Duplicate detection result.
/// </summary>
public class AttendanceResultDto
{
    public bool IsSuccess { get; set; }
    public long TeacherStudentId { get; set; }
    public string StudentName { get; set; } = null!;
    public string StudentCode { get; set; } = null!;

    /// <summary>
    /// The status that was recorded. Null if the operation was blocked (duplicate).
    /// </summary>
    public AttendanceStatus? RecordedStatus { get; set; }

    /// <summary>
    /// True if the student was absent in the previous session occurrence.
    /// REQ-ATT-027/028: Triggers the absent-student alert.
    /// </summary>
    public bool WasAbsentLastSession { get; set; }

    /// <summary>
    /// Number of consecutive absences at the time of this recording.
    /// REQ-ATT-029/058: "This student has been absent for the last N consecutive sessions".
    /// </summary>
    public int ConsecutiveAbsences { get; set; }

    /// <summary>
    /// Date and session name of the last absence (for alert display).
    /// REQ-ATT-058: Shows "the date and session of their last absence".
    /// </summary>
    public DateTime? LastAbsenceDate { get; set; }
    public string? LastAbsenceSessionName { get; set; }

    /// <summary>
    /// True if the last absence was in a cross-session context.
    /// REQ-ATT-060: Additional context in the alert.
    /// </summary>
    public bool LastAbsenceWasCrossSession { get; set; }

    /// <summary>
    /// True if this was a duplicate attendance attempt (already marked).
    /// REQ-ATT-069/070: Blocked with duplicate alert.
    /// </summary>
    public bool IsDuplicate { get; set; }

    /// <summary>
    /// If duplicate, the session name where the student was already recorded.
    /// REQ-ATT-070: Identifies the session of the existing record.
    /// </summary>
    public string? DuplicateSessionName { get; set; }

    /// <summary>
    /// If duplicate, the time the original attendance was recorded.
    /// REQ-ATT-070: Shows "the time the original attendance was recorded".
    /// </summary>
    public DateTime? DuplicateRecordedAt { get; set; }
}

/// <summary>
/// Result DTO for the Mark All Present bulk operation.
/// REQ-ATT-055: Shows the count of affected students.
/// REQ-ATT-056: Completion summary.
/// </summary>
public class MarkAllPresentResultDto
{
    public int MarkedPresentCount { get; set; }
    public int AlreadyMarkedCount { get; set; }
    public int TotalStudents { get; set; }
}

// ══════════════════════════════════════════════
// SESSION OCCURRENCE & DASHBOARD DTOs
// ══════════════════════════════════════════════

/// <summary>
/// Output DTO for a session occurrence (calendar entry).
/// REQ-ATT-065: Color-coded indicators for attendance status.
/// </summary>
public class SessionOccurrenceDto
{
    public long Id { get; set; }
    public long SessionId { get; set; }
    public DateTime OccurrenceDate { get; set; }
    public int OccurrenceIndex { get; set; }

    /// <summary>
    /// Attendance completion status for this occurrence.
    /// REQ-ATT-065: Green = fully recorded, Amber = partial, Grey = unrecorded.
    /// </summary>
    public string CompletionStatus { get; set; } = null!; // "Complete", "Partial", "Unrecorded"

    public int PresentCount { get; set; }
    public int AbsentCount { get; set; }
    public int TotalAssigned { get; set; }
}

/// <summary>
/// Output DTO for the Attendance Module daily dashboard.
/// REQ-ATT-049: Daily summary — total scheduled, completed, pending.
/// REQ-ATT-052: Today's eligible sessions at the top.
/// </summary>
public class AttendanceDashboardDto
{
    public DateTime Today { get; set; }
    public int TotalSessionsToday { get; set; }
    public int CompletedSessions { get; set; }
    public int PendingSessions { get; set; }
    public List<AttendanceDashboardSessionDto> Sessions { get; set; } = new();
}

/// <summary>
/// Output DTO for a single session card on the attendance dashboard.
/// REQ-ATT-050/051: Live counter and color coding.
/// REQ-ATT-003: "Today's Session" indicator.
/// </summary>
public class AttendanceDashboardSessionDto
{
    public long SessionId { get; set; }
    public string SessionName { get; set; } = null!;
    public long? SessionGroupId { get; set; }
    public string? SessionGroupName { get; set; }
    public bool IsToday { get; set; }
    public long? TodayOccurrenceId { get; set; }

    /// <summary>
    /// REQ-ATT-050: "18 / 34" — marked vs total assigned.
    /// </summary>
    public int MarkedCount { get; set; }
    public int TotalAssigned { get; set; }

    /// <summary>
    /// REQ-ATT-051: Green/Amber/Red/Grey color code.
    /// </summary>
    public string StatusColor { get; set; } = null!; // "Green", "Amber", "Red", "Grey"
}

// ══════════════════════════════════════════════
// ABSENCE OVERVIEW DTOs
// ══════════════════════════════════════════════

/// <summary>
/// Output DTO for a student in the Absence Overview panel.
/// REQ-ATT-032: Absent students with consecutive absence count.
/// REQ-ATT-068: Visual indicator of last 5 occurrences.
/// </summary>
public class AbsenceOverviewStudentDto
{
    public long TeacherStudentId { get; set; }
    public string StudentName { get; set; } = null!;
    public string StudentCode { get; set; } = null!;
    public int ConsecutiveAbsences { get; set; }
    public int CumulativeTotalAbsences { get; set; }

    /// <summary>
    /// Session the student is currently assigned to.
    /// REQ-ATT-033: Grouped by assigned session for cross-session view.
    /// </summary>
    public string SessionName { get; set; } = null!;

    /// <summary>
    /// Last 5 attendance statuses (most recent first) for the compact visual indicator.
    /// REQ-ATT-068: Colored dots — red for absent, green for present.
    /// </summary>
    public List<AttendanceStatus> Last5Statuses { get; set; } = new();
}

/// <summary>
/// Request DTO for the Absence Overview panel query.
/// REQ-ATT-034: Supports filtering.
/// REQ-ATT-035: Date selection for historical absence viewing.
/// </summary>
public class AbsenceOverviewRequest
{
    public long TeacherId { get; set; }
    public long SessionId { get; set; }
    public string? Search { get; set; }

    /// <summary>
    /// Optional date to view absence history for. Defaults to the last occurrence.
    /// REQ-ATT-035: Any selected past occurrence date.
    /// </summary>
    public DateTime? Date { get; set; }

    [DefaultValue(1)]
    public int Page { get; set; } = 1;

    [DefaultValue(20)]
    public int PageSize { get; set; } = 20;
}

// ══════════════════════════════════════════════
// STUDENT ATTENDANCE TIMELINE DTOs
// ══════════════════════════════════════════════

/// <summary>
/// Output DTO for a student's all-time attendance summary.
/// REQ-ATT-078: Displayed at the top of the student's timeline.
/// </summary>
public class StudentAttendanceAllTimeSummaryDto
{
    public long TeacherStudentId { get; set; }
    public string StudentName { get; set; } = null!;
    public string StudentCode { get; set; } = null!;
    public int TotalOccurrences { get; set; }
    public int TotalAbsences { get; set; }

    /// <summary>
    /// Overall attendance percentage across all time.
    /// Calculated as: (TotalOccurrences - TotalAbsences) / TotalOccurrences * 100.
    /// </summary>
    public decimal AttendancePercentage { get; set; }

    public int CurrentConsecutiveAbsences { get; set; }
}

/// <summary>
/// Output DTO for a monthly section within the student's attendance timeline.
/// REQ-ATT-075/077: Organized by month with monthly summary.
/// REQ-ATT-080: Collapsible — header shows summary even when collapsed.
/// </summary>
public class StudentAttendanceMonthDto
{
    public int Year { get; set; }
    public int Month { get; set; }

    /// <summary>
    /// REQ-ATT-077: Monthly summary totals.
    /// </summary>
    public int TotalOccurrences { get; set; }
    public int TotalPresent { get; set; }
    public int TotalAbsences { get; set; }
    public decimal AttendancePercentage { get; set; }

    /// <summary>
    /// Individual occurrence records within this month.
    /// REQ-ATT-075: Each occurrence shows status, session name, and date.
    /// </summary>
    public List<AttendanceRecordDto> Records { get; set; } = new();
}

/// <summary>
/// Request DTO for the Student Attendance Timeline list view.
/// REQ-ATT-072/073: Filterable student list.
/// </summary>
public class StudentTimelineListRequest
{
    public long TeacherId { get; set; }
    public long? SessionId { get; set; }
    public long? SessionGroupId { get; set; }
    public string? Search { get; set; }

    [DefaultValue(1)]
    public int Page { get; set; } = 1;

    [DefaultValue(20)]
    public int PageSize { get; set; } = 20;
}

// ══════════════════════════════════════════════
// STUDENT SESSION ASSIGNMENT DTOs
// ══════════════════════════════════════════════

/// <summary>
/// Output DTO for a student's session assignment period.
/// REQ-ATT-046: Displayed in the chronological timeline.
/// </summary>
public class StudentSessionAssignmentDto
{
    public long Id { get; set; }
    public long SessionId { get; set; }
    public string SessionName { get; set; } = null!;
    public DateTime AssignedAt { get; set; }
    public DateTime? UnassignedAt { get; set; }
    public bool IsActive { get; set; }
    public int AttendanceRecordCount { get; set; }
    public int AbsenceCount { get; set; }
}

// ══════════════════════════════════════════════
// TAKE ATTENDANCE SCREEN DTOs
// ══════════════════════════════════════════════

/// <summary>
/// Output DTO for the Take Attendance screen header and student list.
/// REQ-ATT-053: Sticky header with session name, date, and live counts.
/// REQ-ATT-008: Real-time marked/unmarked status.
/// </summary>
public class TakeAttendanceScreenDto
{
    public long SessionId { get; set; }
    public string SessionName { get; set; } = null!;
    public long SessionOccurrenceId { get; set; }
    public DateTime OccurrenceDate { get; set; }
    public bool IsScheduledToday { get; set; }

    /// <summary>
    /// REQ-ATT-053: Live present / absent / unmarked counts.
    /// </summary>
    public int PresentCount { get; set; }
    public int AbsentCount { get; set; }
    public int HeldCount { get; set; }
    public int UnmarkedCount { get; set; }
    public int TotalStudents { get; set; }

    /// <summary>
    /// Primary session students.
    /// REQ-ATT-054: Unmarked students first.
    /// </summary>
    public List<TakeAttendanceStudentDto> PrimaryStudents { get; set; } = new();

    /// <summary>
    /// Students from membership-linked sessions.
    /// REQ-ATT-014/015: Secondary panel, grouped by linked session name.
    /// </summary>
    public List<TakeAttendanceStudentDto> LinkedStudents { get; set; } = new();
}

/// <summary>
/// Output DTO for the completion summary shown when all students are marked.
/// REQ-ATT-056: Total present, total absent, flagged students.
/// </summary>
public class AttendanceCompletionSummaryDto
{
    public int TotalPresent { get; set; }
    public int TotalAbsent { get; set; }
    public int TotalHeld { get; set; }

    /// <summary>
    /// Students flagged for consecutive absences requiring follow-up.
    /// REQ-ATT-056: Listed in the completion summary.
    /// </summary>
    public List<FlaggedAbsentStudentDto> FlaggedStudents { get; set; } = new();
}

/// <summary>
/// A student flagged in the completion summary for consecutive absences.
/// </summary>
public class FlaggedAbsentStudentDto
{
    public long TeacherStudentId { get; set; }
    public string StudentName { get; set; } = null!;
    public string StudentCode { get; set; } = null!;
    public int ConsecutiveAbsences { get; set; }
}

// ══════════════════════════════════════════════
// EDIT ATTENDANCE OUTPUT DTOs
// ══════════════════════════════════════════════

/// <summary>
/// Output DTO for the edit attendance result.
/// REQ-ATT-025: Includes edit count for unsaved changes prompt (REQ-ATT-066).
/// </summary>
public class EditAttendanceResultDto
{
    public int CreatedCount { get; set; }
    public int UpdatedCount { get; set; }
    public int TotalProcessed { get; set; }
}

/// <summary>
/// Output DTO for an attendance edit log entry.
/// REQ-ATT-025: Audit trail for modified records.
/// </summary>
public class AttendanceEditLogDto
{
    public long Id { get; set; }
    public AttendanceStatus PreviousStatus { get; set; }
    public AttendanceStatus NewStatus { get; set; }
    public DateTime EditedAt { get; set; }
    public string? EditedByUserName { get; set; }
    public string? EditReason { get; set; }
}