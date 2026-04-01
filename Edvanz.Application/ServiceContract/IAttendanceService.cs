using Edvanz.Application.Dtos;
using Edvanz.Application.Dtos.Attendance;

namespace Edvanz.Application.ServiceContract;

/// <summary>
/// Defines the contract for Attendance Module operations (Module 3).
/// Handles attendance taking, editing, absence tracking, dashboards, and timeline views.
/// 
/// All methods return Result&lt;T&gt; following the project-wide Result pattern.
/// All data is teacher-scoped via TeacherId (multi-tenant isolation).
/// 
/// STUDENT/PARENT ACCESS:
/// Students and parents can view attendance for their specific records
/// if the teacher's configuration allows it (StudentVisibilityAttendance / ParentVisibilityAttendance).
/// These read-only methods accept the TeacherStudentId and validate visibility.
/// </summary>
public interface IAttendanceService
{
    // ══════════════════════════════════════════════
    // SESSION OCCURRENCE MANAGEMENT
    // ══════════════════════════════════════════════

    /// <summary>
    /// Generates all session occurrences for a session based on its recurrence configuration.
    /// Called when a session is created or its schedule is modified.
    /// REQ-ATT-001/002/005: Materializes occurrence dates within StartDate–EndDate.
    /// </summary>
    Task<Result<int>> GenerateOccurrencesAsync(long teacherId, long sessionId);

    /// <summary>
    /// Retrieves the attendance dashboard for today.
    /// REQ-ATT-049: Daily summary — total scheduled, completed, pending.
    /// REQ-ATT-050: Live student count per session card.
    /// REQ-ATT-051/052: Color coding and today's sessions at top.
    /// </summary>
    Task<Result<AttendanceDashboardDto>> GetDashboardAsync(long teacherId);

    /// <summary>
    /// Retrieves the list of occurrences for a session (calendar view).
    /// REQ-ATT-065: Color-coded date indicators for attendance status.
    /// </summary>
    Task<Result<PaginatedResponse<List<SessionOccurrenceDto>>>> GetSessionOccurrencesAsync(
        long teacherId, long sessionId, int page = 1, int pageSize = 50);

    // ══════════════════════════════════════════════
    // TAKE ATTENDANCE
    // ══════════════════════════════════════════════

    /// <summary>
    /// Loads the Take Attendance screen data for a session's occurrence.
    /// REQ-ATT-008/053/054: Student list with marked/unmarked status.
    /// REQ-ATT-014/015: Includes linked session students in secondary panel.
    /// REQ-ATT-004: Warning if today is not a scheduled occurrence.
    /// </summary>
    Task<Result<TakeAttendanceScreenDto>> GetTakeAttendanceScreenAsync(
        long teacherId, long sessionId, DateTime? occurrenceDate = null);

    /// <summary>
    /// Records attendance for a single student via manual code entry.
    /// REQ-ATT-006 Method 1: Manual Code Entry.
    /// REQ-ATT-039: Must complete in under 500 milliseconds.
    /// REQ-ATT-027/028/057/058: Absent-student alert detection.
    /// REQ-ATT-069/070: Duplicate detection.
    /// </summary>
    Task<Result<AttendanceResultDto>> RecordAttendanceByCodeAsync(RecordAttendanceByCodeDto dto);

    /// <summary>
    /// Records attendance for a single student via barcode scan.
    /// REQ-ATT-006 Method 3: Barcode Scanning.
    /// REQ-ATT-012/038: Must complete in under 1 second.
    /// REQ-ATT-013: Warning for students not assigned to the current session.
    /// REQ-ATT-057/058: Absent-student alert detection.
    /// REQ-ATT-069/070: Duplicate detection.
    /// </summary>
    Task<Result<AttendanceResultDto>> RecordAttendanceByBarcodeAsync(RecordAttendanceByBarcodeDto dto);

    /// <summary>
    /// Records attendance for multiple students via multi-select.
    /// REQ-ATT-006 Method 2: Multi-Select from Student List.
    /// </summary>
    Task<Result<List<AttendanceResultDto>>> RecordAttendanceMultiSelectAsync(RecordAttendanceMultiSelectDto dto);

    /// <summary>
    /// Marks all unmarked students as present for the current occurrence.
    /// REQ-ATT-055: Confirmation prompt before execution.
    /// REQ-ATT-056: Completion summary after execution.
    /// </summary>
    Task<Result<MarkAllPresentResultDto>> MarkAllPresentAsync(MarkAllPresentDto dto);

    /// <summary>
    /// Retrieves the completion summary after all students have been marked.
    /// REQ-ATT-056: Total present, total absent, flagged students.
    /// </summary>
    Task<Result<AttendanceCompletionSummaryDto>> GetCompletionSummaryAsync(
        long teacherId, long sessionId, long occurrenceId);

    // ══════════════════════════════════════════════
    // EDIT ATTENDANCE
    // ══════════════════════════════════════════════

    /// <summary>
    /// Creates or modifies attendance records for a specific date.
    /// REQ-ATT-023/024: Edit Attendance view — change status, add missed records, remove errors.
    /// REQ-ATT-025/BR-ATT-006: All edits logged with timestamp in AttendanceEditLogs.
    /// REQ-ATT-026: Supports future date pre-recording.
    /// </summary>
    Task<Result<EditAttendanceResultDto>> EditAttendanceAsync(EditAttendanceDto dto);

    /// <summary>
    /// Retrieves the edit history for a specific attendance record.
    /// REQ-ATT-025: Differentiates original from modified records.
    /// </summary>
    Task<Result<List<AttendanceEditLogDto>>> GetAttendanceEditLogsAsync(
        long teacherId, long attendanceRecordId);

    // ══════════════════════════════════════════════
    // ABSENCE OVERVIEW
    // ══════════════════════════════════════════════

    /// <summary>
    /// Retrieves the Absence Overview panel for a session.
    /// REQ-ATT-032: Absent students with consecutive absence counts.
    /// REQ-ATT-033: Cross-session absent students from linked sessions.
    /// REQ-ATT-034: Searchable and filterable.
    /// REQ-ATT-067: Sorted by consecutive absences descending.
    /// REQ-ATT-068: Last 5 occurrence statuses for each student.
    /// </summary>
    Task<Result<PaginatedResponse<List<AbsenceOverviewStudentDto>>>> GetAbsenceOverviewAsync(
        AbsenceOverviewRequest request);

    // ══════════════════════════════════════════════
    // STUDENT ATTENDANCE TIMELINE
    // ══════════════════════════════════════════════

    /// <summary>
    /// Retrieves the student list for the Attendance Timeline view.
    /// REQ-ATT-072: All students regardless of session assignment.
    /// REQ-ATT-073: Filterable by session, group, name, code.
    /// </summary>
    Task<Result<PaginatedResponse<List<StudentAttendanceAllTimeSummaryDto>>>> GetStudentTimelineListAsync(
        StudentTimelineListRequest request);

    /// <summary>
    /// Retrieves the all-time attendance summary for a specific student.
    /// REQ-ATT-078: Total occurrences, absences, percentage, consecutive streak.
    /// </summary>
    Task<Result<StudentAttendanceAllTimeSummaryDto>> GetStudentAllTimeSummaryAsync(
        long teacherId, long teacherStudentId);

    /// <summary>
    /// Retrieves a specific month's attendance data for a student's timeline.
    /// REQ-ATT-075/077/079: Monthly detail with lazy loading.
    /// </summary>
    Task<Result<StudentAttendanceMonthDto>> GetStudentTimelineMonthAsync(
        long teacherId, long teacherStudentId, int year, int month);

    /// <summary>
    /// Retrieves all session assignment periods for a student.
    /// REQ-ATT-022/046: Unified attendance profile with per-session history.
    /// </summary>
    Task<Result<List<StudentSessionAssignmentDto>>> GetStudentAssignmentHistoryAsync(
        long teacherId, long teacherStudentId);

    // ══════════════════════════════════════════════
    // STUDENT / PARENT READ-ONLY ACCESS
    // ══════════════════════════════════════════════

    /// <summary>
    /// Retrieves attendance summary for a student's own record (Student User access).
    /// Validates StudentVisibilityAttendance from TeacherConfiguration.
    /// AAM-FR-04.8: Visibility controlled by teacher.
    /// </summary>
    Task<Result<StudentAttendanceAllTimeSummaryDto>> GetStudentOwnAttendanceSummaryAsync(
        long studentUserId, long teacherId);

    /// <summary>
    /// Retrieves attendance summary for a child's record (Parent User access).
    /// Validates ParentVisibilityAttendance from TeacherConfiguration.
    /// AAM-FR-04.9: Parent visibility independent from student visibility.
    /// </summary>
    Task<Result<StudentAttendanceAllTimeSummaryDto>> GetParentChildAttendanceSummaryAsync(
        long parentUserId, long teacherId, long teacherStudentId);

    /// <summary>
    /// Retrieves a specific month's attendance data for student/parent view.
    /// Validates visibility before returning data.
    /// </summary>
    Task<Result<StudentAttendanceMonthDto>> GetStudentOwnTimelineMonthAsync(
        long studentUserId, long teacherId, int year, int month);

    /// <summary>
    /// Retrieves a specific month's attendance data for parent view.
    /// Validates parent visibility before returning data.
    /// </summary>
    Task<Result<StudentAttendanceMonthDto>> GetParentChildTimelineMonthAsync(
        long parentUserId, long teacherId, long teacherStudentId, int year, int month);
}