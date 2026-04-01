using Edvanz.Application.Dtos.Attendance;
using Edvanz.Application.ServiceContract;
using Microsoft.AspNetCore.Mvc;

namespace Edvanz.API.Controllers;

/// <summary>
/// API controller for the Attendance Module (Module 3).
/// Manages attendance taking, editing, absence tracking, dashboards, and student timelines.
/// All teacher endpoints are scoped via the teacherId route parameter.
/// Student/Parent read-only endpoints validate visibility via TeacherConfiguration.
/// 
/// All endpoint documentation follows the existing project pattern:
/// WHAT IT DOES → TABLES READ/WRITTEN → SAMPLE REQUEST → SAMPLE RESPONSE.
/// </summary>
public class AttendanceController : ApiBaseController
{
    private readonly IAttendanceService _attendanceService;

    public AttendanceController(IAttendanceService attendanceService)
    {
        _attendanceService = attendanceService;
    }

    // ══════════════════════════════════════════════════════════════════════════
    // ENDPOINT 1: GENERATE SESSION OCCURRENCES
    // ══════════════════════════════════════════════════════════════════════════
    //
    // WHAT IT DOES:
    //   Generates all occurrence dates for a session based on its recurrence config.
    //   Called when a session is created or its schedule is modified.
    //   REQ-ATT-001/002/005: Materializes dates within StartDate–EndDate.
    //
    // TABLES WRITTEN: SessionOccurrences
    // TABLES READ: Sessions
    //
    // SAMPLE REQUEST:
    //   POST /api/attendance/1/sessions/5/occurrences
    //
    // SAMPLE RESPONSE (200 OK):
    //   { "success": true, "message": "Occurrences generated", "data": 52 }
    //
    // ══════════════════════════════════════════════════════════════════════════
    [HttpPost("{teacherId:long}/sessions/{sessionId:long}/occurrences")]
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
    //   Returns the daily attendance summary: today's sessions, completion status,
    //   live student counts, and color-coded session cards.
    //   REQ-ATT-049/050/051/052.
    //
    // TABLES READ: Sessions, SessionOccurrences, AttendanceRecords, StudentSessionAssignments
    //
    // SAMPLE REQUEST:
    //   GET /api/attendance/1/dashboard
    //
    // ══════════════════════════════════════════════════════════════════════════
    [HttpGet("{teacherId:long}/dashboard")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetDashboard([FromRoute] long teacherId)
    {
        var result = await _attendanceService.GetDashboardAsync(teacherId);
        return ToResponse(result);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // ENDPOINT 3: GET SESSION OCCURRENCES (CALENDAR VIEW)
    // ══════════════════════════════════════════════════════════════════════════
    //
    // WHAT IT DOES:
    //   Returns the list of occurrences for a session with attendance completion status.
    //   REQ-ATT-065: Color-coded date indicators.
    //
    // SAMPLE REQUEST:
    //   GET /api/attendance/1/sessions/5/occurrences?page=1&pageSize=50
    //
    // ══════════════════════════════════════════════════════════════════════════
    [HttpGet("{teacherId:long}/sessions/{sessionId:long}/occurrences")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetSessionOccurrences(
        [FromRoute] long teacherId,
        [FromRoute] long sessionId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        var result = await _attendanceService.GetSessionOccurrencesAsync(teacherId, sessionId, page, pageSize);
        return ToResponse(result);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // ENDPOINT 4: GET TAKE ATTENDANCE SCREEN
    // ══════════════════════════════════════════════════════════════════════════
    //
    // WHAT IT DOES:
    //   Loads the Take Attendance screen data: student list with marked/unmarked status,
    //   linked session students, and live counts.
    //   REQ-ATT-004/008/014/015/053/054.
    //
    // SAMPLE REQUEST:
    //   GET /api/attendance/1/sessions/5/take?occurrenceDate=2026-04-01
    //
    // ══════════════════════════════════════════════════════════════════════════
    [HttpGet("{teacherId:long}/sessions/{sessionId:long}/take")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetTakeAttendanceScreen(
        [FromRoute] long teacherId,
        [FromRoute] long sessionId,
        [FromQuery] DateTime? occurrenceDate = null)
    {
        var result = await _attendanceService.GetTakeAttendanceScreenAsync(teacherId, sessionId, occurrenceDate);
        return ToResponse(result);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // ENDPOINT 5: RECORD ATTENDANCE BY CODE
    // ══════════════════════════════════════════════════════════════════════════
    //
    // WHAT IT DOES:
    //   Records attendance for a single student via manual code entry.
    //   REQ-ATT-006 Method 1. REQ-ATT-039: < 500ms.
    //   Returns absent-student alert info and duplicate detection result.
    //
    // TABLES WRITTEN: AttendanceRecords, StudentAbsenceCounters
    // TABLES READ: TeacherStudents, SessionOccurrences, AttendanceRecords, SessionLinks
    //
    // SAMPLE REQUEST:
    //   POST /api/attendance/record-by-code
    //   { "teacherId": 1, "sessionId": 5, "studentCode": "A1" }
    //
    // ══════════════════════════════════════════════════════════════════════════
    [HttpPost("record-by-code")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(object), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> RecordAttendanceByCode([FromBody] RecordAttendanceByCodeDto dto)
    {
        var result = await _attendanceService.RecordAttendanceByCodeAsync(dto);
        return ToResponse(result);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // ENDPOINT 6: RECORD ATTENDANCE BY BARCODE
    // ══════════════════════════════════════════════════════════════════════════
    //
    // WHAT IT DOES:
    //   Records attendance for a single student via barcode scan.
    //   REQ-ATT-006 Method 3. REQ-ATT-012/038: < 1 second.
    //
    // SAMPLE REQUEST:
    //   POST /api/attendance/record-by-barcode
    //   { "teacherId": 1, "sessionId": 5, "barcodeData": "A1" }
    //
    // ══════════════════════════════════════════════════════════════════════════
    [HttpPost("record-by-barcode")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RecordAttendanceByBarcode([FromBody] RecordAttendanceByBarcodeDto dto)
    {
        var result = await _attendanceService.RecordAttendanceByBarcodeAsync(dto);
        return ToResponse(result);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // ENDPOINT 7: RECORD ATTENDANCE MULTI-SELECT
    // ══════════════════════════════════════════════════════════════════════════
    //
    // WHAT IT DOES:
    //   Records attendance for multiple students at once via multi-select.
    //   REQ-ATT-006 Method 2.
    //
    // SAMPLE REQUEST:
    //   POST /api/attendance/record-multi
    //   { "teacherId": 1, "sessionId": 5, "teacherStudentIds": [1,2,3], "status": "Present" }
    //
    // ══════════════════════════════════════════════════════════════════════════
    [HttpPost("record-multi")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RecordAttendanceMultiSelect([FromBody] RecordAttendanceMultiSelectDto dto)
    {
        var result = await _attendanceService.RecordAttendanceMultiSelectAsync(dto);
        return ToResponse(result);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // ENDPOINT 8: MARK ALL PRESENT
    // ══════════════════════════════════════════════════════════════════════════
    //
    // WHAT IT DOES:
    //   Marks all unmarked students as present in a single action.
    //   REQ-ATT-055: Confirmation prompt shown by frontend before calling this.
    //
    // SAMPLE REQUEST:
    //   POST /api/attendance/mark-all-present
    //   { "teacherId": 1, "sessionId": 5 }
    //
    // ══════════════════════════════════════════════════════════════════════════
    [HttpPost("mark-all-present")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> MarkAllPresent([FromBody] MarkAllPresentDto dto)
    {
        var result = await _attendanceService.MarkAllPresentAsync(dto);
        return ToResponse(result);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // ENDPOINT 9: GET COMPLETION SUMMARY
    // ══════════════════════════════════════════════════════════════════════════
    //
    // WHAT IT DOES:
    //   Returns the completion summary after all students have been marked.
    //   REQ-ATT-056: Present/absent counts and flagged students.
    //
    // SAMPLE REQUEST:
    //   GET /api/attendance/1/sessions/5/occurrences/100/summary
    //
    // ══════════════════════════════════════════════════════════════════════════
    [HttpGet("{teacherId:long}/sessions/{sessionId:long}/occurrences/{occurrenceId:long}/summary")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCompletionSummary(
        [FromRoute] long teacherId,
        [FromRoute] long sessionId,
        [FromRoute] long occurrenceId)
    {
        var result = await _attendanceService.GetCompletionSummaryAsync(teacherId, sessionId, occurrenceId);
        return ToResponse(result);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // ENDPOINT 10: EDIT ATTENDANCE
    // ══════════════════════════════════════════════════════════════════════════
    //
    // WHAT IT DOES:
    //   Creates or modifies attendance records for a specific past or future date.
    //   REQ-ATT-023/024/025/026.
    //
    // SAMPLE REQUEST:
    //   PUT /api/attendance/edit
    //   { "teacherId":1, "sessionId":5, "occurrenceDate":"2026-03-28",
    //     "entries":[{"teacherStudentId":1,"status":"Absent"}] }
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
    // ENDPOINT 11: GET ATTENDANCE EDIT LOGS
    // ══════════════════════════════════════════════════════════════════════════
    //
    // WHAT IT DOES:
    //   Returns the edit history for a specific attendance record.
    //   REQ-ATT-025: Audit trail.
    //
    // SAMPLE REQUEST:
    //   GET /api/attendance/1/records/100/edit-logs
    //
    // ══════════════════════════════════════════════════════════════════════════
    [HttpGet("{teacherId:long}/records/{recordId:long}/edit-logs")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetEditLogs(
        [FromRoute] long teacherId,
        [FromRoute] long recordId)
    {
        var result = await _attendanceService.GetAttendanceEditLogsAsync(teacherId, recordId);
        return ToResponse(result);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // ENDPOINT 12: GET ABSENCE OVERVIEW
    // ══════════════════════════════════════════════════════════════════════════
    //
    // WHAT IT DOES:
    //   Returns the Absence Overview panel for a session with cross-session data.
    //   REQ-ATT-032/033/034/067/068.
    //
    // SAMPLE REQUEST:
    //   GET /api/attendance/absence-overview?teacherId=1&sessionId=5&page=1&pageSize=20
    //
    // ══════════════════════════════════════════════════════════════════════════
    [HttpGet("absence-overview")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetAbsenceOverview([FromQuery] AbsenceOverviewRequest request)
    {
        var result = await _attendanceService.GetAbsenceOverviewAsync(request);
        return ToResponse(result);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // ENDPOINT 13: GET STUDENT TIMELINE LIST
    // ══════════════════════════════════════════════════════════════════════════
    //
    // WHAT IT DOES:
    //   Returns a filterable list of all students with attendance summaries.
    //   REQ-ATT-072/073.
    //
    // SAMPLE REQUEST:
    //   GET /api/attendance/student-timeline?teacherId=1&page=1&pageSize=20
    //
    // ══════════════════════════════════════════════════════════════════════════
    [HttpGet("student-timeline")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetStudentTimelineList([FromQuery] StudentTimelineListRequest request)
    {
        var result = await _attendanceService.GetStudentTimelineListAsync(request);
        return ToResponse(result);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // ENDPOINT 14: GET STUDENT ALL-TIME SUMMARY
    // ══════════════════════════════════════════════════════════════════════════
    //
    // WHAT IT DOES:
    //   Returns a student's all-time attendance summary.
    //   REQ-ATT-078.
    //
    // SAMPLE REQUEST:
    //   GET /api/attendance/1/students/5/summary
    //
    // ══════════════════════════════════════════════════════════════════════════
    [HttpGet("{teacherId:long}/students/{studentId:long}/summary")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetStudentAllTimeSummary(
        [FromRoute] long teacherId,
        [FromRoute] long studentId)
    {
        var result = await _attendanceService.GetStudentAllTimeSummaryAsync(teacherId, studentId);
        return ToResponse(result);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // ENDPOINT 15: GET STUDENT TIMELINE MONTH
    // ══════════════════════════════════════════════════════════════════════════
    //
    // WHAT IT DOES:
    //   Returns a specific month's attendance data for a student.
    //   REQ-ATT-075/077/079: Lazy-loaded monthly detail.
    //
    // SAMPLE REQUEST:
    //   GET /api/attendance/1/students/5/timeline/2026/3
    //
    // ══════════════════════════════════════════════════════════════════════════
    [HttpGet("{teacherId:long}/students/{studentId:long}/timeline/{year:int}/{month:int}")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetStudentTimelineMonth(
        [FromRoute] long teacherId,
        [FromRoute] long studentId,
        [FromRoute] int year,
        [FromRoute] int month)
    {
        var result = await _attendanceService.GetStudentTimelineMonthAsync(teacherId, studentId, year, month);
        return ToResponse(result);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // ENDPOINT 16: GET STUDENT ASSIGNMENT HISTORY
    // ══════════════════════════════════════════════════════════════════════════
    //
    // WHAT IT DOES:
    //   Returns all session assignment periods for a student.
    //   REQ-ATT-022/046.
    //
    // SAMPLE REQUEST:
    //   GET /api/attendance/1/students/5/assignments
    //
    // ══════════════════════════════════════════════════════════════════════════
    [HttpGet("{teacherId:long}/students/{studentId:long}/assignments")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetStudentAssignmentHistory(
        [FromRoute] long teacherId,
        [FromRoute] long studentId)
    {
        var result = await _attendanceService.GetStudentAssignmentHistoryAsync(teacherId, studentId);
        return ToResponse(result);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // ENDPOINT 17: STUDENT OWN ATTENDANCE SUMMARY (Student User Access)
    // ══════════════════════════════════════════════════════════════════════════
    //
    // WHAT IT DOES:
    //   Returns attendance summary for a student viewing their own data.
    //   Validates StudentVisibilityAttendance from TeacherConfiguration.
    //   AAM-FR-04.8: Visibility controlled by teacher.
    //
    // SAMPLE REQUEST:
    //   GET /api/attendance/student-view/10/teacher/1/summary
    //
    // ══════════════════════════════════════════════════════════════════════════
    [HttpGet("student-view/{studentUserId:long}/teacher/{teacherId:long}/summary")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetStudentOwnAttendanceSummary(
        [FromRoute] long studentUserId,
        [FromRoute] long teacherId)
    {
        var result = await _attendanceService.GetStudentOwnAttendanceSummaryAsync(studentUserId, teacherId);
        return ToResponse(result);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // ENDPOINT 18: PARENT CHILD ATTENDANCE SUMMARY (Parent User Access)
    // ══════════════════════════════════════════════════════════════════════════
    //
    // WHAT IT DOES:
    //   Returns attendance summary for a parent viewing their child's data.
    //   Validates ParentVisibilityAttendance from TeacherConfiguration.
    //   AAM-FR-04.9: Parent visibility independent from student visibility.
    //
    // SAMPLE REQUEST:
    //   GET /api/attendance/parent-view/20/teacher/1/student/5/summary
    //
    // ══════════════════════════════════════════════════════════════════════════
    [HttpGet("parent-view/{parentUserId:long}/teacher/{teacherId:long}/student/{studentId:long}/summary")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetParentChildAttendanceSummary(
        [FromRoute] long parentUserId,
        [FromRoute] long teacherId,
        [FromRoute] long studentId)
    {
        var result = await _attendanceService.GetParentChildAttendanceSummaryAsync(parentUserId, teacherId, studentId);
        return ToResponse(result);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // ENDPOINT 19: STUDENT OWN TIMELINE MONTH (Student User Access)
    // ══════════════════════════════════════════════════════════════════════════
    //
    // SAMPLE REQUEST:
    //   GET /api/attendance/student-view/10/teacher/1/timeline/2026/3
    //
    // ══════════════════════════════════════════════════════════════════════════
    [HttpGet("student-view/{studentUserId:long}/teacher/{teacherId:long}/timeline/{year:int}/{month:int}")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetStudentOwnTimelineMonth(
        [FromRoute] long studentUserId,
        [FromRoute] long teacherId,
        [FromRoute] int year,
        [FromRoute] int month)
    {
        var result = await _attendanceService.GetStudentOwnTimelineMonthAsync(studentUserId, teacherId, year, month);
        return ToResponse(result);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // ENDPOINT 20: PARENT CHILD TIMELINE MONTH (Parent User Access)
    // ══════════════════════════════════════════════════════════════════════════
    //
    // SAMPLE REQUEST:
    //   GET /api/attendance/parent-view/20/teacher/1/student/5/timeline/2026/3
    //
    // ══════════════════════════════════════════════════════════════════════════
    [HttpGet("parent-view/{parentUserId:long}/teacher/{teacherId:long}/student/{studentId:long}/timeline/{year:int}/{month:int}")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetParentChildTimelineMonth(
        [FromRoute] long parentUserId,
        [FromRoute] long teacherId,
        [FromRoute] long studentId,
        [FromRoute] int year,
        [FromRoute] int month)
    {
        var result = await _attendanceService.GetParentChildTimelineMonthAsync(
            parentUserId, teacherId, studentId, year, month);
        return ToResponse(result);
    }
}