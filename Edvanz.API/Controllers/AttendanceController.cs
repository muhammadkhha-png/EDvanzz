using Edvanz.Application.Dtos.Attendance;
using Edvanz.Application.ServiceContract;
using Microsoft.AspNetCore.Mvc;

namespace Edvanz.API.Controllers;

/// <summary>
/// API controller for the Attendance Module (Module 3).
/// Manages attendance taking, editing, absence tracking, cross-session attendance,
/// student attendance timeline, and reporting.
/// All endpoints are teacher-scoped via the teacherId route parameter.
///
/// All endpoint documentation follows the existing project pattern:
/// WHAT IT DOES → TABLES READ/WRITTEN → SAMPLE REQUEST.
/// </summary>
public class AttendanceController : ApiBaseController
{
    private readonly IAttendanceService _attendanceService;

    public AttendanceController(IAttendanceService attendanceService)
    {
        _attendanceService = attendanceService;
    }

    // ══════════════════════════════════════════════════════════════════════════
    // ENDPOINT 1: GENERATE OCCURRENCES
    // ══════════════════════════════════════════════════════════════════════════
    //
    // WHAT IT DOES:
    //   Generates session occurrences based on recurrence rules.
    //   Called after session creation or date range update.
    //   REQ-ATT-001/002: Populates the occurrence schedule.
    //
    // TABLES WRITTEN: SessionOccurrences
    // TABLES READ: Sessions
    //
    // ══════════════════════════════════════════════════════════════════════════
    [HttpPost("{teacherId:long}/sessions/{sessionId:long}/occurrences/generate")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GenerateOccurrences(
        [FromRoute] long teacherId,
        [FromRoute] long sessionId)
    {
        var result = await _attendanceService.GenerateOccurrencesAsync(teacherId, sessionId);
        return ToResponse(result);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // ENDPOINT 2: GET ATTENDANCE DASHBOARD
    // ══════════════════════════════════════════════════════════════════════════
    //
    // WHAT IT DOES:
    //   Returns the attendance dashboard for a teacher on a specific date.
    //   REQ-ATT-049: Daily summary — total, completed, pending.
    //   REQ-ATT-050/051/052: Session cards with live counters and color coding.
    //
    // TABLES READ: SessionOccurrences, Sessions, AttendanceRecords, StudentSessionAssignments
    //
    // ══════════════════════════════════════════════════════════════════════════
    [HttpGet("{teacherId:long}/dashboard")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetDashboard(
        [FromRoute] long teacherId,
        [FromQuery] AttendanceDashboardRequest request)
    {
        var result = await _attendanceService.GetDashboardAsync(teacherId, request);
        return ToResponse(result);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // ENDPOINT 3: GET ATTENDANCE STUDENT LIST
    // ══════════════════════════════════════════════════════════════════════════
    //
    // WHAT IT DOES:
    //   Returns the student list for Take Attendance / Edit Attendance.
    //   REQ-ATT-008/014/015/054/036: Marked/unmarked, linked sessions, paginated.
    //
    // TABLES READ: StudentSessionAssignments, AttendanceRecords, SessionLinks, StudentAbsenceCounters
    //
    // ══════════════════════════════════════════════════════════════════════════
    [HttpGet("{teacherId:long}/sessions/{sessionId:long}/students")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetAttendanceStudentList(
        [FromRoute] long teacherId,
        [FromRoute] long sessionId,
        [FromQuery] DateTime? occurrenceDate,
        [FromQuery] AttendanceStudentListRequest request)
    {
        var result = await _attendanceService.GetAttendanceStudentListAsync(
            teacherId, sessionId, occurrenceDate, request);
        return ToResponse(result);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // ENDPOINT 4: MARK SINGLE STUDENT ATTENDANCE
    // ══════════════════════════════════════════════════════════════════════════
    //
    // WHAT IT DOES:
    //   Marks a single student's attendance via any of the three methods.
    //   REQ-ATT-006/012/027-031/057-061/069-071.
    //   Returns absence alert info and duplicate detection results.
    //
    // TABLES WRITTEN: AttendanceRecords, StudentAbsenceCounters, SessionOccurrences (status)
    // TABLES READ: Sessions, TeacherStudents, SessionOccurrences, SessionLinks, StudentSessionAssignments
    //
    // ══════════════════════════════════════════════════════════════════════════
    [HttpPost("mark")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(object), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> MarkAttendance([FromBody] MarkAttendanceDto dto)
    {
        var result = await _attendanceService.MarkAttendanceAsync(dto);
        return ToResponse(result);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // ENDPOINT 5: BULK MARK ATTENDANCE
    // ══════════════════════════════════════════════════════════════════════════
    //
    // WHAT IT DOES:
    //   Marks multiple students' attendance in one action.
    //   REQ-ATT-006 Method 2 / REQ-ATT-055: Multi-select and Mark All Present.
    //   REQ-ATT-056: Returns completion summary.
    //
    // TABLES WRITTEN: AttendanceRecords, StudentAbsenceCounters, SessionOccurrences (status)
    // TABLES READ: Sessions, TeacherStudents, SessionOccurrences, StudentSessionAssignments
    //
    // ══════════════════════════════════════════════════════════════════════════
    [HttpPost("mark-bulk")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> BulkMarkAttendance([FromBody] BulkMarkAttendanceDto dto)
    {
        var result = await _attendanceService.BulkMarkAttendanceAsync(dto);
        return ToResponse(result);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // ENDPOINT 6: GET OCCURRENCE CALENDAR
    // ══════════════════════════════════════════════════════════════════════════
    //
    // WHAT IT DOES:
    //   Returns the calendar view of session occurrences with color indicators.
    //   REQ-ATT-065: Green/amber/grey color coding.
    //
    // TABLES READ: SessionOccurrences, AttendanceRecords, StudentSessionAssignments
    //
    // ══════════════════════════════════════════════════════════════════════════
    [HttpGet("{teacherId:long}/sessions/{sessionId:long}/occurrences/calendar")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetOccurrenceCalendar(
        [FromRoute] long teacherId,
        [FromRoute] long sessionId)
    {
        var result = await _attendanceService.GetOccurrenceCalendarAsync(teacherId, sessionId);
        return ToResponse(result);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // ENDPOINT 7: EDIT ATTENDANCE RECORD
    // ══════════════════════════════════════════════════════════════════════════
    //
    // WHAT IT DOES:
    //   Edits an existing attendance record. REQ-ATT-023/024/025.
    //   Logs the edit in AttendanceEditLogs. BR-ATT-006.
    //
    // TABLES WRITTEN: AttendanceRecords, AttendanceEditLogs, StudentAbsenceCounters
    // TABLES READ: AttendanceRecords
    //
    // ══════════════════════════════════════════════════════════════════════════
    [HttpPut("edit")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> EditAttendance([FromBody] EditAttendanceDto dto)
    {
        var result = await _attendanceService.EditAttendanceAsync(dto);
        return ToResponse(result);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // ENDPOINT 8: ADD ATTENDANCE RECORD (VIA EDIT)
    // ══════════════════════════════════════════════════════════════════════════
    //
    // WHAT IT DOES:
    //   Adds a new attendance record via Edit Attendance view.
    //   REQ-ATT-024: Add missed records. REQ-ATT-026: Pre-record future attendance.
    //
    // TABLES WRITTEN: AttendanceRecords, SessionOccurrences, StudentAbsenceCounters
    // TABLES READ: Sessions, TeacherStudents, SessionOccurrences, StudentSessionAssignments
    //
    // ══════════════════════════════════════════════════════════════════════════
    [HttpPost("add")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(object), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> AddAttendanceRecord([FromBody] AddAttendanceRecordDto dto)
    {
        var result = await _attendanceService.AddAttendanceRecordAsync(dto);
        return ToResponse(result);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // ENDPOINT 9: DELETE ATTENDANCE RECORD
    // ══════════════════════════════════════════════════════════════════════════
    //
    // WHAT IT DOES:
    //   Removes an erroneously recorded attendance entry. REQ-ATT-024.
    //
    // TABLES WRITTEN: AttendanceRecords, StudentAbsenceCounters
    // TABLES READ: AttendanceRecords
    //
    // ══════════════════════════════════════════════════════════════════════════
    [HttpDelete("{teacherId:long}/records/{recordId:long}")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteAttendanceRecord(
        [FromRoute] long teacherId,
        [FromRoute] long recordId)
    {
        var dto = new DeleteAttendanceRecordDto
        {
            TeacherId = teacherId,
            AttendanceRecordId = recordId
        };
        var result = await _attendanceService.DeleteAttendanceRecordAsync(dto);
        return ToResponse(result);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // ENDPOINT 10: GET EDIT HISTORY
    // ══════════════════════════════════════════════════════════════════════════
    //
    // WHAT IT DOES:
    //   Returns the edit audit trail for an attendance record. REQ-ATT-025.
    //
    // TABLES READ: AttendanceRecords, AttendanceEditLogs
    //
    // ══════════════════════════════════════════════════════════════════════════
    [HttpGet("{teacherId:long}/records/{recordId:long}/history")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetEditHistory(
        [FromRoute] long teacherId,
        [FromRoute] long recordId)
    {
        var result = await _attendanceService.GetEditHistoryAsync(teacherId, recordId);
        return ToResponse(result);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // ENDPOINT 11: GET ABSENCE OVERVIEW
    // ══════════════════════════════════════════════════════════════════════════
    //
    // WHAT IT DOES:
    //   Returns the absence overview for a session and its linked sessions.
    //   REQ-ATT-032/033/034/035/067/068.
    //
    // TABLES READ: StudentAbsenceCounters, TeacherStudents, SessionLinks, AttendanceRecords
    //
    // ══════════════════════════════════════════════════════════════════════════
    [HttpGet("{teacherId:long}/sessions/{sessionId:long}/absences")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetAbsenceOverview(
        [FromRoute] long teacherId,
        [FromRoute] long sessionId,
        [FromQuery] AbsenceOverviewRequest request)
    {
        var result = await _attendanceService.GetAbsenceOverviewAsync(teacherId, sessionId, request);
        return ToResponse(result);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // ENDPOINT 12: GET TIMELINE STUDENT LIST
    // ══════════════════════════════════════════════════════════════════════════
    //
    // WHAT IT DOES:
    //   Returns the paginated student list for the Attendance Timeline view.
    //   REQ-ATT-072/073.
    //
    // TABLES READ: StudentSessionAssignments, TeacherStudents, StudentAbsenceCounters
    //
    // ══════════════════════════════════════════════════════════════════════════
    [HttpGet("{teacherId:long}/timeline/students")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetTimelineStudentList(
        [FromRoute] long teacherId,
        [FromQuery] AttendanceTimelineRequest request)
    {
        var result = await _attendanceService.GetTimelineStudentListAsync(teacherId, request);
        return ToResponse(result);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // ENDPOINT 13: GET STUDENT ATTENDANCE SUMMARY
    // ══════════════════════════════════════════════════════════════════════════
    //
    // WHAT IT DOES:
    //   Returns the full attendance summary for a specific student.
    //   REQ-ATT-074/078/046.
    //
    // TABLES READ: TeacherStudents, StudentAbsenceCounters, StudentSessionAssignments
    //
    // ══════════════════════════════════════════════════════════════════════════
    [HttpGet("{teacherId:long}/timeline/students/{studentId:long}")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetStudentAttendanceSummary(
        [FromRoute] long teacherId,
        [FromRoute] long studentId)
    {
        var result = await _attendanceService.GetStudentAttendanceSummaryAsync(teacherId, studentId);
        return ToResponse(result);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // ENDPOINT 14: GET STUDENT TIMELINE MONTH
    // ══════════════════════════════════════════════════════════════════════════
    //
    // WHAT IT DOES:
    //   Returns one month of attendance data for a student's timeline.
    //   REQ-ATT-075/077/079.
    //
    // TABLES READ: AttendanceRecords, TeacherStudents
    //
    // ══════════════════════════════════════════════════════════════════════════
    [HttpGet("{teacherId:long}/timeline/students/{studentId:long}/month")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetStudentTimelineMonth(
        [FromRoute] long teacherId,
        [FromRoute] long studentId,
        [FromQuery] StudentTimelineMonthRequest request)
    {
        var result = await _attendanceService.GetStudentTimelineMonthAsync(teacherId, studentId, request);
        return ToResponse(result);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // ENDPOINT 15: GENERATE REPORT
    // ══════════════════════════════════════════════════════════════════════════
    //
    // WHAT IT DOES:
    //   Generates an attendance report. REQ-ATT-040 (6 report types).
    //   REQ-ATT-042: Must complete within 5 seconds.
    //
    // TABLES READ: AttendanceRecords, TeacherStudents, Sessions, SessionLinks
    //
    // ══════════════════════════════════════════════════════════════════════════
    [HttpPost("{teacherId:long}/reports")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GenerateReport(
        [FromRoute] long teacherId,
        [FromBody] AttendanceReportRequest request)
    {
        var result = await _attendanceService.GenerateReportAsync(teacherId, request);
        return ToResponse(result);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // ENDPOINT 16: STUDENT VIEW — ATTENDANCE SUMMARY
    // ══════════════════════════════════════════════════════════════════════════
    //
    // WHAT IT DOES:
    //   Returns attendance summary for a student/parent view.
    //   Gated by TeacherConfiguration visibility settings.
    //
    // TABLES READ: TeacherConfigurations, StudentAbsenceCounters, StudentSessionAssignments
    //
    // ══════════════════════════════════════════════════════════════════════════
    [HttpGet("{teacherId:long}/student-view/{studentId:long}/summary")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetStudentViewSummary(
        [FromRoute] long teacherId,
        [FromRoute] long studentId)
    {
        var result = await _attendanceService.GetStudentViewAttendanceSummaryAsync(teacherId, studentId);
        return ToResponse(result);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // ENDPOINT 17: STUDENT VIEW — MONTHLY DATA
    // ══════════════════════════════════════════════════════════════════════════
    //
    // WHAT IT DOES:
    //   Returns one month of attendance for student/parent view.
    //   Gated by TeacherConfiguration visibility settings.
    //
    // TABLES READ: TeacherConfigurations, AttendanceRecords
    //
    // ══════════════════════════════════════════════════════════════════════════
    [HttpGet("{teacherId:long}/student-view/{studentId:long}/month")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetStudentViewMonth(
        [FromRoute] long teacherId,
        [FromRoute] long studentId,
        [FromQuery] StudentTimelineMonthRequest request)
    {
        var result = await _attendanceService.GetStudentViewAttendanceAsync(teacherId, studentId, request);
        return ToResponse(result);
    }
}