using Edvanz.Domain.Enums;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Edvanz.Application.Dtos.Attendance;

// ══════════════════════════════════════════════
// ENUMS FOR SORTING / REPORTING
// ══════════════════════════════════════════════

/// <summary>
/// Report types available in the Attendance Module.
/// REQ-ATT-040: Six distinct report types.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum AttendanceReportType
{
    /// <summary>REQ-ATT-040 Type 1: Single student absence report across all sessions.</summary>
    SingleStudentAbsence = 1,
    /// <summary>REQ-ATT-040 Type 2: Session absence report (single session).</summary>
    SessionAbsence = 2,
    /// <summary>REQ-ATT-040 Type 3: All sessions absence report (aggregated).</summary>
    AllSessionsAbsence = 3,
    /// <summary>REQ-ATT-040 Type 4: Full attendance history for a specific session.</summary>
    SessionAttendanceHistory = 4,
    /// <summary>REQ-ATT-040 Type 5: Attendance report for a session group.</summary>
    SessionGroupAttendance = 5,
    /// <summary>REQ-ATT-040 Type 6: Linked sessions attendance report.</summary>
    LinkedSessionsAttendance = 6
}

/// <summary>
/// Sort options for the attendance timeline view.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum AttendanceSortBy
{
    OccurrenceDate,
    StudentName,
    StudentCode
}

// ══════════════════════════════════════════════
// TAKE ATTENDANCE INPUT DTOs
// ══════════════════════════════════════════════

/// <summary>
/// Input DTO for marking a single student's attendance.
/// REQ-ATT-006: Supports all three methods via AttendanceMethod field.
/// REQ-ATT-017: Cross-session attendance indicated by the student not being in the session.
/// </summary>
public class MarkAttendanceDto
{
    /// <summary>The owning teacher's Id. All data scoped to this teacher.</summary>
    [Required]
    public long TeacherId { get; set; }

    /// <summary>The session to take attendance for.</summary>
    [Required]
    public long SessionId { get; set; }

    /// <summary>The student to mark attendance for.</summary>
    [Required]
    public long TeacherStudentId { get; set; }

    /// <summary>
    /// The attendance status to record.
    /// REQ-ATT-006: Present or Absent.
    /// </summary>
    [Required]
    public AttendanceStatus Status { get; set; }

    /// <summary>
    /// Which method was used. REQ-ATT-006.
    /// </summary>
    [Required]
    public AttendanceMethod AttendanceMethod { get; set; }

    /// <summary>
    /// The date of the session occurrence. Defaults to today if not specified.
    /// REQ-ATT-026: Future dates allowed via Edit Attendance.
    /// </summary>
    public DateTime? OccurrenceDate { get; set; }

    /// <summary>
    /// The user performing the action (teacher or assistant).
    /// </summary>
    public long? RecordedByUserId { get; set; }

    /// <summary>
    /// If true, the user has confirmed they want to mark this student
    /// even though they were absent in the last session (REQ-ATT-028/058).
    /// </summary>
    public bool AbsenceAlertConfirmed { get; set; } = false;
}

/// <summary>
/// Input DTO for marking multiple students' attendance in one action.
/// REQ-ATT-006 Method 2: Multi-select from student list.
/// REQ-ATT-055: "Mark All Present" uses this with all student Ids.
/// </summary>
public class BulkMarkAttendanceDto
{
    /// <summary>The owning teacher's Id.</summary>
    [Required]
    public long TeacherId { get; set; }

    /// <summary>The session to take attendance for.</summary>
    [Required]
    public long SessionId { get; set; }

    /// <summary>List of student Ids to mark.</summary>
    [Required]
    public List<long> TeacherStudentIds { get; set; } = new();

    /// <summary>The status to apply to all selected students.</summary>
    [Required]
    public AttendanceStatus Status { get; set; }

    /// <summary>The method used (typically MultiSelect).</summary>
    [Required]
    public AttendanceMethod AttendanceMethod { get; set; }

    /// <summary>The occurrence date. Defaults to today.</summary>
    public DateTime? OccurrenceDate { get; set; }

    /// <summary>Who is recording.</summary>
    public long? RecordedByUserId { get; set; }
}

// ══════════════════════════════════════════════
// EDIT ATTENDANCE INPUT DTOs
// ══════════════════════════════════════════════

/// <summary>
/// Input DTO for editing an existing attendance record.
/// REQ-ATT-023/024: Distinct from Take Attendance — selects past/future date.
/// REQ-ATT-025: Edit timestamp preserved; original RecordedAt never altered.
/// </summary>
public class EditAttendanceDto
{
    /// <summary>The owning teacher's Id.</summary>
    [Required]
    public long TeacherId { get; set; }

    /// <summary>The attendance record to edit.</summary>
    [Required]
    public long AttendanceRecordId { get; set; }

    /// <summary>The new status to apply.</summary>
    [Required]
    public AttendanceStatus NewStatus { get; set; }

    /// <summary>Optional reason for the edit (audit trail).</summary>
    public string? EditReason { get; set; }

    /// <summary>Who is making the edit.</summary>
    public long? EditedByUserId { get; set; }
}

/// <summary>
/// Input DTO for adding a new attendance record via Edit Attendance.
/// REQ-ATT-024: Add records for students missed during original attendance-taking.
/// REQ-ATT-026: Pre-record attendance for future occurrence dates.
/// </summary>
public class AddAttendanceRecordDto
{
    /// <summary>The owning teacher's Id.</summary>
    [Required]
    public long TeacherId { get; set; }

    /// <summary>The session this record belongs to.</summary>
    [Required]
    public long SessionId { get; set; }

    /// <summary>The student to add a record for.</summary>
    [Required]
    public long TeacherStudentId { get; set; }

    /// <summary>The occurrence date to add the record for.</summary>
    [Required]
    public DateTime OccurrenceDate { get; set; }

    /// <summary>The status to record.</summary>
    [Required]
    public AttendanceStatus Status { get; set; }

    /// <summary>Who is adding the record.</summary>
    public long? RecordedByUserId { get; set; }
}

/// <summary>
/// Input DTO for deleting an attendance record via Edit Attendance.
/// REQ-ATT-024: Remove erroneously recorded entries.
/// </summary>
public class DeleteAttendanceRecordDto
{
    /// <summary>The owning teacher's Id.</summary>
    [Required]
    public long TeacherId { get; set; }

    /// <summary>The attendance record to delete.</summary>
    [Required]
    public long AttendanceRecordId { get; set; }
}

// ══════════════════════════════════════════════
// REQUEST DTOs (PAGINATION / FILTERING)
// ══════════════════════════════════════════════

/// <summary>
/// Request DTO for the attendance dashboard.
/// REQ-ATT-049: Daily summary — sessions for today with status.
/// </summary>
public class AttendanceDashboardRequest
{
    /// <summary>The date to show the dashboard for. Defaults to today.</summary>
    public DateTime? Date { get; set; }
}

/// <summary>
/// Request DTO for listing sessions with attendance context.
/// REQ-ATT-003: Shows "Today's Session" badge for eligible sessions.
/// </summary>
public class AttendanceSessionListRequest
{
    private int _page = 1;
    private int _pageSize = 20;

    public int Page
    {
        get => _page;
        set => _page = value < 1 ? 1 : value;
    }

    public int PageSize
    {
        get => _pageSize;
        set => _pageSize = value < 1 ? 20 : value > 100 ? 100 : value;
    }

    /// <summary>Search by session name.</summary>
    public string? Search { get; set; }

    /// <summary>Filter by group Id.</summary>
    public long? GroupId { get; set; }
}

/// <summary>
/// Request DTO for the student list within Take Attendance / Edit Attendance screens.
/// REQ-ATT-036: Supports pagination for up to 50K students.
/// REQ-ATT-054: Separates unmarked from marked students.
/// </summary>
public class AttendanceStudentListRequest
{
    private int _page = 1;
    private int _pageSize = 50;

    public int Page
    {
        get => _page;
        set => _page = value < 1 ? 1 : value;
    }

    public int PageSize
    {
        get => _pageSize;
        set => _pageSize = value < 1 ? 50 : value > 100 ? 100 : value;
    }

    /// <summary>Search by student name or code.</summary>
    public string? Search { get; set; }

    /// <summary>Filter to show only unmarked students.</summary>
    public bool UnmarkedOnly { get; set; } = false;
}

/// <summary>
/// Request DTO for the Absence Overview panel.
/// REQ-ATT-032/034: Filterable, searchable absence overview.
/// </summary>
public class AbsenceOverviewRequest
{
    private int _page = 1;
    private int _pageSize = 20;

    public int Page
    {
        get => _page;
        set => _page = value < 1 ? 1 : value;
    }

    public int PageSize
    {
        get => _pageSize;
        set => _pageSize = value < 1 ? 20 : value > 100 ? 100 : value;
    }

    /// <summary>Search by student name or code. REQ-ATT-034.</summary>
    public string? Search { get; set; }

    /// <summary>Filter by session Id. REQ-ATT-034.</summary>
    public long? SessionId { get; set; }

    /// <summary>Filter students with missing phone number. REQ-ATT-034.</summary>
    public bool MissingStudentPhone { get; set; } = false;

    /// <summary>Filter students with missing parent phone number. REQ-ATT-034.</summary>
    public bool MissingParentPhone { get; set; } = false;

    /// <summary>
    /// View absence history for a specific past occurrence date.
    /// REQ-ATT-035: Default is latest occurrence.
    /// </summary>
    public DateTime? OccurrenceDate { get; set; }
}

/// <summary>
/// Request DTO for the Student Attendance Timeline view.
/// REQ-ATT-072/073: Filterable student list.
/// </summary>
public class AttendanceTimelineRequest
{
    private int _page = 1;
    private int _pageSize = 20;

    public int Page
    {
        get => _page;
        set => _page = value < 1 ? 1 : value;
    }

    public int PageSize
    {
        get => _pageSize;
        set => _pageSize = value < 1 ? 20 : value > 100 ? 100 : value;
    }

    /// <summary>Filter by specific session. REQ-ATT-073.</summary>
    public long? SessionId { get; set; }

    /// <summary>Filter by specific session group. REQ-ATT-073.</summary>
    public long? SessionGroupId { get; set; }

    /// <summary>Search by student name (partial match). REQ-ATT-073.</summary>
    public string? StudentName { get; set; }

    /// <summary>Search by student code (partial match). REQ-ATT-073.</summary>
    public string? StudentCode { get; set; }
}

/// <summary>
/// Request DTO for a student's timeline month data.
/// REQ-ATT-079: Load one month at a time on scroll.
/// </summary>
public class StudentTimelineMonthRequest
{
    /// <summary>Year of the month to load.</summary>
    [Required]
    public int Year { get; set; }

    /// <summary>Month number (1-12) to load.</summary>
    [Required]
    [Range(1, 12)]
    public int Month { get; set; }
}

/// <summary>
/// Request DTO for report generation.
/// REQ-ATT-040: Multiple report types with date range.
/// </summary>
public class AttendanceReportRequest
{
    /// <summary>The type of report to generate. REQ-ATT-040.</summary>
    [Required]
    public AttendanceReportType ReportType { get; set; }

    /// <summary>Teacher Student Id for Type 1 (single student report).</summary>
    public long? TeacherStudentId { get; set; }

    /// <summary>Session Id for Type 2, 4 reports.</summary>
    public long? SessionId { get; set; }

    /// <summary>Session Group Id for Type 5 report.</summary>
    public long? SessionGroupId { get; set; }

    /// <summary>Start of the report date range.</summary>
    public DateTime? StartDate { get; set; }

    /// <summary>End of the report date range.</summary>
    public DateTime? EndDate { get; set; }
}

// ══════════════════════════════════════════════
// OUTPUT DTOs
// ══════════════════════════════════════════════

/// <summary>
/// Output DTO for the attendance dashboard.
/// REQ-ATT-049: Daily summary at the top.
/// </summary>
public class AttendanceDashboardDto
{
    /// <summary>The date this dashboard is for.</summary>
    public DateTime Date { get; set; }

    /// <summary>Total sessions scheduled for this date. REQ-ATT-049.</summary>
    public int TotalSessionsToday { get; set; }

    /// <summary>Sessions with completed attendance. REQ-ATT-049.</summary>
    public int CompletedSessions { get; set; }

    /// <summary>Sessions still pending. REQ-ATT-049.</summary>
    public int PendingSessions { get; set; }

    /// <summary>Sessions currently in progress.</summary>
    public int InProgressSessions { get; set; }

    /// <summary>Session cards with attendance status. REQ-ATT-050/051/052.</summary>
    public List<AttendanceSessionCardDto> SessionCards { get; set; } = new();
}

/// <summary>
/// Session card DTO for the attendance dashboard.
/// REQ-ATT-050: Live counter showing marked vs. total.
/// REQ-ATT-051: Color-coded status.
/// REQ-ATT-052: Today's sessions at top.
/// </summary>
public class AttendanceSessionCardDto
{
    public long SessionId { get; set; }
    public string SessionName { get; set; } = null!;
    public long? SessionGroupId { get; set; }
    public string? SessionGroupName { get; set; }

    /// <summary>Whether this session occurs today. REQ-ATT-003.</summary>
    public bool IsToday { get; set; }

    /// <summary>The occurrence Id for today (if applicable).</summary>
    public long? TodayOccurrenceId { get; set; }

    /// <summary>Attendance-taking progress. REQ-ATT-051: green/amber/red/grey.</summary>
    public OccurrenceStatus Status { get; set; }

    /// <summary>Number of students already marked. REQ-ATT-050.</summary>
    public int MarkedCount { get; set; }

    /// <summary>Total students assigned to this session. REQ-ATT-050.</summary>
    public int TotalStudents { get; set; }

    /// <summary>Session start time for ordering.</summary>
    public TimeSpan StartTime { get; set; }
}

/// <summary>
/// Output DTO for a single attendance record.
/// Used in Take Attendance, Edit Attendance, and history views.
/// </summary>
public class AttendanceRecordDto
{
    public long Id { get; set; }
    public long TeacherStudentId { get; set; }
    public string StudentName { get; set; } = null!;
    public string StudentCode { get; set; } = null!;
    public long? SessionOccurrenceId { get; set; }
    public long? SessionId { get; set; }
    public string SessionName { get; set; } = null!;
    public DateTime OccurrenceDate { get; set; }
    public AttendanceStatus Status { get; set; }
    public AttendanceMethod AttendanceMethod { get; set; }
    public bool IsCrossSession { get; set; }
    public long? CrossSessionId { get; set; }
    public string? CrossSessionName { get; set; }
    public DateTime? CrossSessionOccurrenceDate { get; set; }
    public DateTime RecordedAt { get; set; }
    public bool IsEdited { get; set; }
    public DateTime? LastEditedAt { get; set; }
}

/// <summary>
/// Output DTO returned when marking attendance, includes absence alert info.
/// REQ-ATT-028/029: Absence alert data for the frontend to display.
/// REQ-ATT-069/070: Duplicate detection info.
/// </summary>
public class MarkAttendanceResultDto
{
    /// <summary>The created or updated attendance record.</summary>
    public AttendanceRecordDto? Record { get; set; }

    /// <summary>Whether an absence alert should be shown. REQ-ATT-028.</summary>
    public bool HasAbsenceAlert { get; set; }

    /// <summary>Number of consecutive absences. REQ-ATT-029.</summary>
    public int ConsecutiveAbsences { get; set; }

    /// <summary>Date of last absence. REQ-ATT-028.</summary>
    public DateTime? LastAbsenceDate { get; set; }

    /// <summary>Session name of last absence. REQ-ATT-060.</summary>
    public string? LastAbsenceSessionName { get; set; }

    /// <summary>Whether the last absence was in a cross-session. REQ-ATT-060.</summary>
    public bool LastAbsenceWasCrossSession { get; set; }

    /// <summary>Whether a duplicate was detected. REQ-ATT-069/070.</summary>
    public bool IsDuplicate { get; set; }

    /// <summary>If duplicate: which session already recorded attendance. REQ-ATT-070.</summary>
    public string? DuplicateSessionName { get; set; }

    /// <summary>If duplicate: when the original attendance was recorded. REQ-ATT-070.</summary>
    public DateTime? DuplicateRecordedAt { get; set; }
}

/// <summary>
/// Output DTO for bulk mark attendance result.
/// REQ-ATT-055/056: Completion summary.
/// </summary>
public class BulkMarkAttendanceResultDto
{
    /// <summary>Number of students successfully marked.</summary>
    public int SuccessCount { get; set; }

    /// <summary>Number of students skipped (already marked/duplicate).</summary>
    public int SkippedCount { get; set; }

    /// <summary>Number of students with absence alerts. REQ-ATT-056.</summary>
    public int AbsenceAlertCount { get; set; }

    /// <summary>Students flagged for consecutive absences. REQ-ATT-056.</summary>
    public List<AbsenceAlertStudentDto> AbsenceAlerts { get; set; } = new();

    /// <summary>Total present after this operation. REQ-ATT-056.</summary>
    public int TotalPresent { get; set; }

    /// <summary>Total absent after this operation. REQ-ATT-056.</summary>
    public int TotalAbsent { get; set; }
}

/// <summary>
/// Absence alert info for a single student.
/// REQ-ATT-028/029/057/058: Alert data for frontend.
/// </summary>
public class AbsenceAlertStudentDto
{
    public long TeacherStudentId { get; set; }
    public string StudentName { get; set; } = null!;
    public string StudentCode { get; set; } = null!;
    public int ConsecutiveAbsences { get; set; }
    public DateTime? LastAbsenceDate { get; set; }
    public string? LastAbsenceSessionName { get; set; }
    public bool WasCrossSession { get; set; }
}

/// <summary>
/// Student row DTO for the Take Attendance screen.
/// REQ-ATT-008: Real-time marked vs. unmarked display.
/// REQ-ATT-054: Unmarked students displayed first.
/// REQ-ATT-071: Already-marked indicator badge.
/// </summary>
public class AttendanceStudentRowDto
{
    public long TeacherStudentId { get; set; }
    public string StudentName { get; set; } = null!;
    public string StudentCode { get; set; } = null!;
    public string? Barcode { get; set; }

    /// <summary>Current attendance status for this occurrence. Null if unmarked.</summary>
    public AttendanceStatus? CurrentStatus { get; set; }

    /// <summary>Whether this student is already marked. REQ-ATT-071.</summary>
    public bool IsMarked { get; set; }

    /// <summary>Whether this student is "held" (REQ-ATT-061).</summary>
    public bool IsHeld { get; set; }

    /// <summary>Whether this student is from a linked session (secondary panel). REQ-ATT-014.</summary>
    public bool IsCrossSessionStudent { get; set; }

    /// <summary>The linked session name (if cross-session student). REQ-ATT-015.</summary>
    public string? SourceSessionName { get; set; }

    /// <summary>Consecutive absences for alert display. REQ-ATT-029.</summary>
    public int ConsecutiveAbsences { get; set; }

    /// <summary>Total absences for display.</summary>
    public int TotalAbsences { get; set; }
}

/// <summary>
/// Output DTO for Absence Overview panel.
/// REQ-ATT-032: Absent students with consecutive absence count.
/// REQ-ATT-068: Last 5 occurrence status indicators.
/// </summary>
public class AbsenceOverviewStudentDto
{
    public long TeacherStudentId { get; set; }
    public string StudentName { get; set; } = null!;
    public string StudentCode { get; set; } = null!;
    public long? SessionId { get; set; }
    public string? SessionName { get; set; }
    public int ConsecutiveAbsences { get; set; }
    public int TotalAbsences { get; set; }
    public DateTime? LastAbsenceDate { get; set; }

    /// <summary>
    /// Last 5 occurrence statuses for compact visual indicator.
    /// REQ-ATT-068: Red dot = absent, green dot = present.
    /// </summary>
    public List<AttendanceStatus> RecentStatuses { get; set; } = new();
}

/// <summary>
/// Output DTO for the Student Attendance Timeline all-time summary.
/// REQ-ATT-078: Displayed at top of student's timeline.
/// </summary>
public class StudentAttendanceSummaryDto
{
    public long TeacherStudentId { get; set; }
    public string StudentName { get; set; } = null!;
    public string StudentCode { get; set; } = null!;

    /// <summary>Total occurrences across entire history. REQ-ATT-078.</summary>
    public int TotalOccurrences { get; set; }

    /// <summary>Total absences across all sessions and all time. REQ-ATT-078.</summary>
    public int TotalAbsences { get; set; }

    /// <summary>Overall attendance percentage. REQ-ATT-078.</summary>
    public decimal AttendancePercentage { get; set; }

    /// <summary>Current consecutive absence streak. REQ-ATT-078.</summary>
    public int ConsecutiveAbsences { get; set; }

    /// <summary>All assignment periods, chronologically ordered. REQ-ATT-046.</summary>
    public List<AssignmentPeriodDto> AssignmentPeriods { get; set; } = new();
}

/// <summary>
/// A single assignment period in a student's timeline.
/// REQ-ATT-046: Session name, assignment start/end date, and records within.
/// </summary>
public class AssignmentPeriodDto
{
    public long StudentSessionAssignmentId { get; set; }
    public long? SessionId { get; set; }
    public string SessionName { get; set; } = null!;
    public DateTime AssignedAt { get; set; }
    public DateTime? UnassignedAt { get; set; }
    public bool IsActive { get; set; }
}

/// <summary>
/// Monthly summary within a student's timeline.
/// REQ-ATT-077: Displayed beneath month header.
/// REQ-ATT-080: Shown even when month is collapsed.
/// </summary>
public class MonthlyAttendanceSummaryDto
{
    public int Year { get; set; }
    public int Month { get; set; }
    public int TotalOccurrences { get; set; }
    public int TotalPresent { get; set; }
    public int TotalAbsences { get; set; }

    /// <summary>Attendance percentage for this month. REQ-ATT-077: "7 / 9 — 77%".</summary>
    public decimal AttendancePercentage { get; set; }

    /// <summary>Individual attendance records for this month. REQ-ATT-075.</summary>
    public List<AttendanceRecordDto> Records { get; set; } = new();
}

/// <summary>
/// Output DTO for the Edit Attendance calendar view.
/// REQ-ATT-065: Color-coded occurrence dates.
/// </summary>
public class OccurrenceCalendarItemDto
{
    public long OccurrenceId { get; set; }
    public DateTime OccurrenceDate { get; set; }
    public OccurrenceStatus Status { get; set; }
    public int MarkedCount { get; set; }
    public int TotalStudents { get; set; }
}

/// <summary>
/// Output DTO for attendance edit log entry.
/// REQ-ATT-025: Audit trail display.
/// </summary>
public class AttendanceEditLogDto
{
    public long Id { get; set; }
    public AttendanceStatus PreviousStatus { get; set; }
    public AttendanceStatus NewStatus { get; set; }
    public DateTime EditedAt { get; set; }
    public long? EditedByUserId { get; set; }
    public string? EditReason { get; set; }
}

/// <summary>
/// FIX 4.1: Input DTO for placing a student on "hold" during attendance taking.
/// REQ-ATT-061: Held students are visually distinguished from marked and unmarked students.
/// REQ-ATT-058: "Hold" cancels the attendance action without recording anything.
/// </summary>
public class HoldStudentDto
{
    /// <summary>The owning teacher's Id.</summary>
    [Required]
    public long TeacherId { get; set; }

    /// <summary>The session where attendance is being taken.</summary>
    [Required]
    public long SessionId { get; set; }

    /// <summary>The student to place on hold.</summary>
    [Required]
    public long TeacherStudentId { get; set; }

    /// <summary>The occurrence date. Defaults to today.</summary>
    public DateTime? OccurrenceDate { get; set; }

    /// <summary>The user performing the action (teacher or assistant).</summary>
    public long? RecordedByUserId { get; set; }
}

/// <summary>
/// FIX 4.1: Input DTO for releasing a held student.
/// REQ-ATT-061: Held students can be returned to and processed later in the same session.
/// </summary>
public class ReleaseHoldDto
{
    /// <summary>The owning teacher's Id.</summary>
    [Required]
    public long TeacherId { get; set; }

    /// <summary>The session where the student is held.</summary>
    [Required]
    public long SessionId { get; set; }

    /// <summary>The held student to release.</summary>
    [Required]
    public long TeacherStudentId { get; set; }

    /// <summary>The occurrence date.</summary>
    public DateTime? OccurrenceDate { get; set; }

    /// <summary>
    /// True = mark as Present (confirm attendance).
    /// False = discard the hold (return to unmarked).
    /// </summary>
    [Required]
    public bool MarkAsPresent { get; set; }

    /// <summary>The user performing the action.</summary>
    public long? RecordedByUserId { get; set; }
}

// ══════════════════════════════════════════════
// EXPORT DTOs (FIX 4.2 — REQ-ATT-041/081)
// ══════════════════════════════════════════════

/// <summary>
/// FIX 4.2: Request DTO for exporting a student's attendance timeline.
/// REQ-ATT-081: Exportable as PDF or Excel covering a date range or full history.
/// </summary>
public class ExportTimelineRequest
{
    /// <summary>Optional start of export date range. Null = from first assignment.</summary>
    public DateTime? StartDate { get; set; }

    /// <summary>Optional end of export date range. Null = to current date.</summary>
    public DateTime? EndDate { get; set; }

    /// <summary>Export format: "xlsx" or "pdf".</summary>
    [Required]
    public string Format { get; set; } = "xlsx";
}

// ══════════════════════════════════════════════
// OFFLINE SYNC DTOs (FIX 4.3 — REQ-ATT-084/085)
// ══════════════════════════════════════════════

/// <summary>
/// FIX 4.3: A single offline-recorded attendance entry.
/// REQ-ATT-082: Records stored locally and synced when connectivity is restored.
/// </summary>
 public class OfflineAttendanceEntryDto
 {
     /// <summary>The student to mark attendance for.</summary>
     [Required]
     public long TeacherStudentId { get; set; }
 
     /// <summary>The session the attendance was taken for.</summary>
     [Required]
     public long SessionId { get; set; }
 
     /// <summary>The attendance status recorded offline.</summary>
     [Required]
     public AttendanceStatus Status { get; set; }
 
     /// <summary>The method used to record (ManualCode, MultiSelect, BarcodeScan).</summary>
     [Required]
     public AttendanceMethod AttendanceMethod { get; set; }
 
     /// <summary>The occurrence date for this attendance entry.</summary>
     [Required]
     public DateTime OccurrenceDate { get; set; }
 
     /// <summary>The client-side timestamp when the entry was recorded.</summary>
     [Required]
     public DateTime ClientRecordedAt { get; set; }
 
     /// <summary>Client-generated unique Id for conflict detection.</summary>
     [Required]
     public string ClientEntryId { get; set; } = null!;
 }

/// <summary>
/// FIX 4.3: Batch request for syncing offline attendance records.
/// REQ-ATT-084: Automatic sync when connectivity is restored.
/// </summary>
 public class OfflineSyncRequestDto
 {
     /// <summary>The owning teacher's Id.</summary>
     [Required]
     public long TeacherId { get; set; }
 
     /// <summary>The user who recorded the attendance offline.</summary>
     public long? RecordedByUserId { get; set; }
 
     /// <summary>The batch of offline entries to sync.</summary>
     [Required]
     public List<OfflineAttendanceEntryDto> Entries { get; set; } = new();
 }

/// <summary>
/// FIX 4.3: Result of a sync operation for a single entry.
/// </summary>
public class SyncEntryResultDto
{
    /// <summary>The client-generated unique Id from the offline entry.</summary>
    public string ClientEntryId { get; set; } = null!;

    /// <summary>Whether this entry was successfully synced.</summary>
    public bool Success { get; set; }

    /// <summary>If not successful, whether this is a conflict requiring resolution.</summary>
    public bool IsConflict { get; set; }

    /// <summary>The server-side record if a conflict was detected.</summary>
    public AttendanceRecordDto? ServerRecord { get; set; }

    /// <summary>Error message if sync failed for non-conflict reasons.</summary>
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// FIX 4.3: Batch result of the offline sync operation.
/// REQ-ATT-085: Presents conflicts for tutor resolution.
/// </summary>
public class SyncResultDto
{
    /// <summary>Total entries submitted for sync.</summary>
    public int TotalSubmitted { get; set; }

    /// <summary>Entries successfully synced without conflicts.</summary>
    public int SuccessCount { get; set; }

    /// <summary>Entries with conflicts requiring resolution.</summary>
    public int ConflictCount { get; set; }

    /// <summary>Entries that failed for non-conflict reasons.</summary>
    public int FailedCount { get; set; }

    /// <summary>Detailed result per entry.</summary>
    public List<SyncEntryResultDto> EntryResults { get; set; } = new();
}