using Edvanz.API.Attributes;
using Edvanz.Application.Dtos.Attendance;
using Edvanz.Application.IservicesContract;
using Edvanz.Application.ServiceContract;
using Edvanz.Domain.Constants;
using Edvanz.Domain.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Edvanz.API.Controllers;

//// <summary>
//// API controller for the Attendance Module (Module 3) — teacher and assistant
//// endpoints. Inherits <see cref="ModuleSixApiBaseController"/> for JWT-to-teacher
//// resolution (<c>ResolveTeacherIdAsync</c>, <c>GetAct    /// ══════════════════════════════════════════════════════════════════════════  /// ENDPOINTingUserId</c>, <c>TeacherNotResolved</c>).
////
//// AUTH &amp; TENANCY: class-level [Authorize] + per-action
//// [ModulePermission(AttendanceConstants.ModuleName, ...)]. The acting teacherId is
//// ALWAYS derived from the JWT via ResolveTeacherIdAsync — never read from the route
//// or body (Catalog §1.3 / REQ-ATT-NFR-003). Teachers pass on the module claim alone;
//// assistants additionally require the named permission (REQ-USR-017); SuperAdmin bypasses.
//// The acting user id comes from GetActingUserId() for audit attribution
//// (BR-USR-003 / BR-ATT-006), never from the request body.
////
//// STUDENT/PARENT-FACING ENDPOINTS LIVE ELSEWHERE: see StudentAttendanceController
//// (Phase 3) — their auth (roleOnly) and caller-scoping differ.
////
//// Endpoint documentation follows the existing pattern:
//// WHAT IT DOES → TABLES READ/WRITTEN → SAMPLE REQUEST.
//// </summary>
[Authorize]
public class AttendanceController : ModuleSixApiBaseController
{
    private readonly IAttendanceService _attendanceService;

    public AttendanceController(
        IAttendanceService attendanceService,
        ICurrentUserService currentUser,
        IUnitOfWork unitOfWork)
        : base(currentUser, unitOfWork)
    {
        _attendanceService = attendanceService;
    }
    //// <summary>
    /// ══════════════════════════════════════════════════════════════════════════
    /// ENDPOINT 1: GENERATE OCCURRENCES
    /// ══════════════════════════════════════════════════════════════════════════
    ///
    /// WHAT IT DOES:
    ///   Generates session occurrences based on recurrence rules.
    ///   Called after session creation or date range update.
    ///   REQ-ATT-001/002: Populates the occurrence schedule.
    ///
    /// TABLES WRITTEN: SessionOccurrences
    /// TABLES READ: Sessions
    ///
    /// ══════════════════════════════════════════════════════════════════════════
   //// </summary>
    [HttpPost("sessions/{sessionId:long}/occurrences/generate")]
    [ModulePermission(AttendanceConstants.ModuleName, AttendanceConstants.PermissionTake)]
    [ProducesResponseType(typeof(Edvanz.Application.Dtos.Result<int>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(object), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GenerateOccurrences([FromRoute] long sessionId)
    {
        long? teacherId = await ResolveTeacherIdAsync();
        if (teacherId is null) return TeacherNotResolved();

        var result = await _attendanceService.GenerateOccurrencesAsync(teacherId.Value, sessionId);
        return ToResponse(result);
    }

    /// ══════════════════════════════════════════════════════════════════════════
    /// ENDPOINT 2: GET ATTENDANCE DASHBOARD
    /// ══════════════════════════════════════════════════════════════════════════
    ///
    /// WHAT IT DOES:
    ///   Returns the attendance dashboard for a teacher on a specific date.
    ///   REQ-ATT-049: Daily summary — total, completed, pending.
    ///   REQ-ATT-050/051/052: Session cards with live counters and color coding.
    ///
    /// TABLES READ: SessionOccurrences, Sessions, AttendanceRecords, StudentSessionAssignments
    ///
    /// ══════════════════════════════════════════════════════════════════════════
    [HttpGet("dashboard")]
    [ModulePermission(AttendanceConstants.ModuleName, AttendanceConstants.PermissionTake)]
    [ProducesResponseType(typeof(Edvanz.Application.Dtos.Result<Edvanz.Application.Dtos.Attendance.AttendanceDashboardDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(object), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetDashboard([FromQuery] AttendanceDashboardRequest request)
    {
        long? teacherId = await ResolveTeacherIdAsync();
        if (teacherId is null) return TeacherNotResolved();

        var result = await _attendanceService.GetDashboardAsync(teacherId.Value, request);
        return ToResponse(result);
    }

    /// ══════════════════════════════════════════════════════════════════════════
    /// ENDPOINT 3: GET ATTENDANCE STUDENT LIST
    /// ══════════════════════════════════════════════════════════════════════════
    ///
    /// WHAT IT DOES:
    ///   Returns the student list for Take Attendance / Edit Attendance.
    ///   REQ-ATT-008/014/015/054/036: Marked/unmarked, linked sessions, paginated.
    ///
    /// TABLES READ: StudentSessionAssignments, AttendanceRecords, SessionLinks, StudentAbsenceCounters
    ///
    /// ══════════════════════════════════════════════════════════════════════════
    [HttpGet("sessions/{sessionId:long}/students")]
    [ModulePermission(AttendanceConstants.ModuleName, AttendanceConstants.PermissionTake)]
    [ProducesResponseType(typeof(Edvanz.Application.Dtos.Result<Edvanz.Application.Dtos.Attendance.AttendanceStudentListDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(object), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetAttendanceStudentList(
        [FromRoute] long sessionId,
        [FromQuery] DateTime? occurrenceDate,
        [FromQuery] AttendanceStudentListRequest request)
    {
        long? teacherId = await ResolveTeacherIdAsync();
        if (teacherId is null) return TeacherNotResolved();

        var result = await _attendanceService.GetAttendanceStudentListAsync(
            teacherId.Value, sessionId, occurrenceDate, request);
        return ToResponse(result);
    }

    /// ══════════════════════════════════════════════════════════════════════════
    /// ENDPOINT 3B: GET SESSION MONTH MATRIX
    /// ══════════════════════════════════════════════════════════════════════════
    ///
    /// WHAT IT DOES:
    ///   Month view for one session: students (rows) × the session's occurrences in the
    ///   month (columns), each with INDEPENDENT pagination (page/pageSize for students,
    ///   occurrencePage/occurrencePageSize for occurrences) so the client never loads
    ///   the whole roster or the whole month at once. Every row carries a cell per
    ///   returned occurrence (status or unmarked) plus whole-month present/absent totals.
    ///   Replaces the frontend's per-date iteration of ENDPOINT 3 for the month screen.
    ///   REQ-ATT-046 (chronological tabular view), REQ-ATT-019/020 (rows are the
    ///   assignment periods overlapping the month, so past months keep departed students).
    ///
    /// TABLES READ: Sessions, SessionOccurrences, StudentSessionAssignments,
    ///              TeacherStudents, AttendanceRecords
    ///
    /// ══════════════════════════════════════════════════════════════════════════
    [HttpGet("sessions/{sessionId:long}/month")]
    [ModulePermission(AttendanceConstants.ModuleName, AttendanceConstants.PermissionViewHistory)]
    [ProducesResponseType(typeof(Edvanz.Application.Dtos.Result<Edvanz.Application.Dtos.Attendance.SessionMonthAttendanceDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(object), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(object), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetSessionMonthAttendance(
        [FromRoute] long sessionId,
        [FromQuery] SessionMonthAttendanceRequest request)
    {
        long? teacherId = await ResolveTeacherIdAsync();
        if (teacherId is null) return TeacherNotResolved();

        var result = await _attendanceService.GetSessionMonthAttendanceAsync(
            teacherId.Value, sessionId, request);
        return ToResponse(result);
    }


    /// ══════════════════════════════════════════════════════════════════════════
    /// ENDPOINT 4: MARK SINGLE STUDENT ATTENDANCE
    /// ══════════════════════════════════════════════════════════════════════════
    ///
    /// WHAT IT DOES:
    ///   Marks a single student's attendance via any of the three methods.
    ///   REQ-ATT-006/012/027-031/057-061/069-071.
    ///   Returns absence alert info and duplicate detection results.
    ///
    /// TABLES WRITTEN: AttendanceRecords, StudentAbsenceCounters, SessionOccurrences (status)
    /// TABLES READ: Sessions, TeacherStudents, SessionOccurrences, SessionLinks, StudentSessionAssignments
    ///
    /// ══════════════════════════════════════════════════════════════════════════
    [HttpPost("mark")]
    [ModulePermission(AttendanceConstants.ModuleName, AttendanceConstants.PermissionTake)]
    [ProducesResponseType(typeof(Edvanz.Application.Dtos.Result<Edvanz.Application.Dtos.Attendance.MarkAttendanceResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(object), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(object), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(object), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> MarkAttendance([FromBody] MarkAttendanceDto dto)
    {
        long? teacherId = await ResolveTeacherIdAsync();
        if (teacherId is null) return TeacherNotResolved();

        dto.TeacherId = teacherId.Value;
        dto.RecordedByUserId = GetActingUserId();

        var result = await _attendanceService.MarkAttendanceAsync(dto);
        return ToResponse(result);
    }
    /// ══════════════════════════════════════════════════════════════════════════
    /// ENDPOINT 5: BULK MARK ATTENDANCE
    /// ══════════════════════════════════════════════════════════════════════════
    ///
    /// WHAT IT DOES:
    ///   Marks multiple students' attendance in one action.
    ///   REQ-ATT-006 Method 2 / REQ-ATT-055: Multi-select and Mark All Present.
    ///   REQ-ATT-056: Returns completion summary.
    ///
    /// TABLES WRITTEN: AttendanceRecords, StudentAbsenceCounters, SessionOccurrences (status)
    /// TABLES READ: Sessions, TeacherStudents, SessionOccurrences, StudentSessionAssignments
    ///
    /// ══════════════════════════════════════════════════════════════════════════
    [HttpPost("mark-bulk")]
    [ModulePermission(AttendanceConstants.ModuleName, AttendanceConstants.PermissionTake)]
    [ProducesResponseType(typeof(Edvanz.Application.Dtos.Result<Edvanz.Application.Dtos.Attendance.BulkMarkAttendanceResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(object), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(object), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> BulkMarkAttendance([FromBody] BulkMarkAttendanceDto dto)
    {
        long? teacherId = await ResolveTeacherIdAsync();
        if (teacherId is null) return TeacherNotResolved();

        dto.TeacherId = teacherId.Value;
        dto.RecordedByUserId = GetActingUserId();

        var result = await _attendanceService.BulkMarkAttendanceAsync
            (dto);
        return ToResponse(result);
    }
    /// ══════════════════════════════════════════════════════════════════════════
    /// ENDPOINT 6: GET OCCURRENCE CALENDAR
    /// ══════════════════════════════════════════════════════════════════════════
    ///
    /// WHAT IT DOES:
    ///   Returns the calendar view of session occurrences with color indicators.
    ///   REQ-ATT-065: Green/amber/grey color coding.
    ///
    /// TABLES READ: SessionOccurrences, AttendanceRecords, StudentSessionAssignments
    ///
    /// ══════════════════════════════════════════════════════════════════════════
    [HttpGet("sessions/{sessionId:long}/occurrences/calendar")]
    [ModulePermission(AttendanceConstants.ModuleName, AttendanceConstants.PermissionEdit)]
    [ProducesResponseType(typeof(Edvanz.Application.Dtos.Result<System.Collections.Generic.List<Edvanz.Application.Dtos.Attendance.OccurrenceCalendarItemDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(object), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetOccurrenceCalendar([FromRoute] long sessionId)
    {
        long? teacherId = await ResolveTeacherIdAsync();
        if (teacherId is null) return TeacherNotResolved();

        var result = await _attendanceService.GetOccurrenceCalendarAsync(teacherId.Value, sessionId);
        return ToResponse(result);
    }

    /// ══════════════════════════════════════════════════════════════════════════
    /// ENDPOINT 6B: GET OCCURRENCE STUDENTS (FIX C2)
    /// ══════════════════════════════════════════════════════════════════════════
    ///
    /// WHAT IT DOES:
    ///   Returns the student attendance records for a specific past occurrence date.
    ///   REQ-ATT-023/024: Edit Attendance view — select any past/future date and
    ///   view/modify attendance records for that date.
    ///
    ///   FIX C2: This service method (GetOccurrenceStudentsAsync) existed in
    ///   IAttendanceService and AttendanceService but had NO controller endpoint,
    ///   making the Edit Attendance student list for past dates unreachable by frontend.
    ///
    /// TABLES READ: Sessions, SessionOccurrences, AttendanceRecords
    ///
    /// ══════════════════════════════════════════════════════════════════════════
    [HttpGet("sessions/{sessionId:long}/occurrences/{occurrenceDate:datetime}/students")]
    [ModulePermission(AttendanceConstants.ModuleName, AttendanceConstants.PermissionEdit)]
    [ProducesResponseType(typeof(Edvanz.Application.Dtos.Result<System.Collections.Generic.List<Edvanz.Application.Dtos.Attendance.AttendanceRecordDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(object), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetOccurrenceStudents(
          [FromRoute] long sessionId,
          [FromRoute] DateTime occurrenceDate)
    {
        long? teacherId = await ResolveTeacherIdAsync();
        if (teacherId is null) return TeacherNotResolved();

        var result = await _attendanceService.GetOccurrenceStudentsAsync(teacherId.Value, sessionId, occurrenceDate);
        return ToResponse(result);
    }

    /// ══════════════════════════════════════════════════════════════════════════
    /// ENDPOINT 7: EDIT ATTENDANCE RECORD
    /// ══════════════════════════════════════════════════════════════════════════
    ///
    /// WHAT IT DOES:
    ///   Edits an existing attendance record. REQ-ATT-023/024/025.
    ///   Logs the edit in AttendanceEditLogs. BR-ATT-006.
    ///
    /// TABLES WRITTEN: AttendanceRecords, AttendanceEditLogs, StudentAbsenceCounters
    /// TABLES READ: AttendanceRecords
    ///
    /// ══════════════════════════════════════════════════════════════════════════
    [HttpPut("edit")]
    [ModulePermission(AttendanceConstants.ModuleName, AttendanceConstants.PermissionEdit)]
    [ProducesResponseType(typeof(Edvanz.Application.Dtos.Result<Edvanz.Application.Dtos.Attendance.AttendanceRecordDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(object), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> EditAttendance([FromBody] EditAttendanceDto dto)
    {
        long? teacherId = await ResolveTeacherIdAsync();
        if (teacherId is null) return TeacherNotResolved();

        dto.TeacherId = teacherId.Value;
        dto.EditedByUserId = GetActingUserId();

        var result = await _attendanceService.EditAttendanceAsync(dto);
        return ToResponse(result);
    }
    /// ══════════════════════════════════════════════════════════════════════════
    /// ENDPOINT 8: ADD ATTENDANCE RECORD (VIA EDIT)
    /// ══════════════════════════════════════════════════════════════════════════
    ///
    /// WHAT IT DOES:
    ///   Adds a new attendance record via Edit Attendance view.
    ///   REQ-ATT-024: Add missed records. REQ-ATT-026: Pre-record future attendance.
    ///
    /// TABLES WRITTEN: AttendanceRecords, StudentAbsenceCounters, SessionOccurrences
    /// TABLES READ: Sessions, TeacherStudents, SessionOccurrences, StudentSessionAssignments
    ///
    /// ══════════════════════════════════════════════════════════════════════════
    [HttpPost("add")]
    [ModulePermission(AttendanceConstants.ModuleName, AttendanceConstants.PermissionEdit)]
    [ProducesResponseType(typeof(Edvanz.Application.Dtos.Result<Edvanz.Application.Dtos.Attendance.AttendanceRecordDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(object), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(object), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(object), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> AddAttendanceRecord([FromBody] AddAttendanceRecordDto dto)
    {
        long? teacherId = await ResolveTeacherIdAsync();
        if (teacherId is null) return TeacherNotResolved();

        dto.TeacherId = teacherId.Value;
        dto.RecordedByUserId = GetActingUserId();

        var result = await _attendanceService.AddAttendanceRecordAsync(dto);
        return ToResponse(result);
    }

    /// ══════════════════════════════════════════════════════════════════════════
    /// ENDPOINT 9: DELETE ATTENDANCE RECORD
    /// ══════════════════════════════════════════════════════════════════════════
    ///
    /// WHAT IT DOES:
    ///   Deletes an erroneously recorded attendance record. REQ-ATT-024.
    ///
    /// TABLES WRITTEN: AttendanceRecords, StudentAbsenceCounters
    /// TABLES READ: AttendanceRecords
    ///
    /// ══════════════════════════════════════════════════════════════════════════
    [HttpDelete("delete")]
    [ModulePermission(AttendanceConstants.ModuleName, AttendanceConstants.PermissionEdit)]
    [ProducesResponseType(typeof(Edvanz.Application.Dtos.Result<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(object), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteAttendanceRecord([FromBody] DeleteAttendanceRecordDto dto)
    {
        long? teacherId = await ResolveTeacherIdAsync();
        if (teacherId is null) return TeacherNotResolved();

        dto.TeacherId = teacherId.Value;

        var result = await _attendanceService.DeleteAttendanceRecordAsync(dto);
        return ToResponse(result);
    }
    /// ══════════════════════════════════════════════════════════════════════════
    /// ENDPOINT 10: GET EDIT HISTORY
    /// ══════════════════════════════════════════════════════════════════════════
    ///
    /// WHAT IT DOES:
    ///   Returns the audit trail (edit logs) for an attendance record.
    ///   REQ-ATT-025.
    ///
    /// TABLES READ: AttendanceRecords, AttendanceEditLogs
    ///
    /// ══════════════════════════════════════════════════════════════════════════
    [HttpGet("records/{recordId:long}/history")]
    [ModulePermission(AttendanceConstants.ModuleName, AttendanceConstants.PermissionViewHistory)]
    [ProducesResponseType(typeof(Edvanz.Application.Dtos.Result<System.Collections.Generic.List<Edvanz.Application.Dtos.Attendance.AttendanceEditLogDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(object), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetEditHistory([FromRoute] long recordId)
    {
        long? teacherId = await ResolveTeacherIdAsync();
        if (teacherId is null) return TeacherNotResolved();

        var result = await _attendanceService.GetEditHistoryAsync(teacherId.Value, recordId);
        return ToResponse(result);
    }

    /// ══════════════════════════════════════════════════════════════════════════
    /// ENDPOINT 11: GET ABSENCE OVERVIEW
    /// ══════════════════════════════════════════════════════════════════════════
    ///
    /// WHAT IT DOES:
    ///   Returns the absence overview for a session and its linked sessions.
    ///   REQ-ATT-032/033/034/035/067/068.
    ///
    /// TABLES READ: StudentAbsenceCounters, TeacherStudents, SessionLinks, AttendanceRecords
    ///
    /// ══════════════════════════════════════════════════════════════════════════
    [HttpGet("sessions/{sessionId:long}/absences")]
    [ModulePermission(AttendanceConstants.ModuleName, AttendanceConstants.PermissionViewAbsenceOverview)]
    [ProducesResponseType(typeof(Edvanz.Application.Dtos.Result<Edvanz.Application.Dtos.PaginatedResponse<System.Collections.Generic.List<Edvanz.Application.Dtos.Attendance.AbsenceOverviewStudentDto>>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(object), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetAbsenceOverview(
        [FromRoute] long sessionId,
        [FromQuery] AbsenceOverviewRequest request)
    {
        long? teacherId = await ResolveTeacherIdAsync();
        if (teacherId is null) return TeacherNotResolved();

        var result = await _attendanceService.GetAbsenceOverviewAsync(teacherId.Value, sessionId, request);
        return ToResponse(result);
    }
    /// ══════════════════════════════════════════════════════════════════════════
    /// ENDPOINT 12: GET TIMELINE STUDENT LIST
    /// ══════════════════════════════════════════════════════════════════════════
    ///
    /// WHAT IT DOES:
    ///   Returns the paginated student list for the Attendance Timeline view.
    ///   REQ-ATT-072/073.
    ///
    /// TABLES READ: StudentSessionAssignments, TeacherStudents, StudentAbsenceCounters
    ///
    /// ══════════════════════════════════════════════════════════════════════════
    [HttpGet("timeline/students")]
    [ModulePermission(AttendanceConstants.ModuleName, AttendanceConstants.PermissionViewHistory)]
    [ProducesResponseType(typeof(Edvanz.Application.Dtos.Result<Edvanz.Application.Dtos.PaginatedResponse<System.Collections.Generic.List<Edvanz.Application.Dtos.Attendance.StudentAttendanceSummaryDto>>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(object), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetTimelineStudentList([FromQuery] AttendanceTimelineRequest request)
    {
        long? teacherId = await ResolveTeacherIdAsync();
        if (teacherId is null) return TeacherNotResolved();

        var result = await _attendanceService.GetTimelineStudentListAsync(teacherId.Value, request);
        return ToResponse(result);
    }

    /// ══════════════════════════════════════════════════════════════════════════
    /// ENDPOINT 13: GET STUDENT ATTENDANCE SUMMARY
    /// ══════════════════════════════════════════════════════════════════════════
    ///
    /// WHAT IT DOES:
    ///   Returns the full attendance summary for a specific student.
    ///   REQ-ATT-074/078/046.
    ///
    /// TABLES READ: TeacherStudents, StudentAbsenceCounters, StudentSessionAssignments
    ///
    /// ══════════════════════════════════════════════════════════════════════════
    [HttpGet("timeline/students/{studentId:long}")]
    [ModulePermission(AttendanceConstants.ModuleName, AttendanceConstants.PermissionViewHistory)]
    [ProducesResponseType(typeof(Edvanz.Application.Dtos.Result<Edvanz.Application.Dtos.Attendance.StudentAttendanceSummaryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(object), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetStudentAttendanceSummary([FromRoute] long studentId)
    {
        long? teacherId = await ResolveTeacherIdAsync();
        if (teacherId is null) return TeacherNotResolved();

        var result = await _attendanceService.GetStudentAttendanceSummaryAsync(teacherId.Value, studentId);
        return ToResponse(result);
    }

    /// ══════════════════════════════════════════════════════════════════════════
    /// ENDPOINT 14: GET STUDENT TIMELINE MONTH
    /// ══════════════════════════════════════════════════════════════════════════
    ///
    /// WHAT IT DOES:
    ///   Returns one month of attendance data for a student's timeline.
    ///   REQ-ATT-075/077/079.
    ///
    /// TABLES READ: AttendanceRecords, TeacherStudents
    ///
    /// ══════════════════════════════════════════════════════════════════════════
    [HttpGet("timeline/students/{studentId:long}/month")]
    [ModulePermission(AttendanceConstants.ModuleName, AttendanceConstants.PermissionViewHistory)]
    [ProducesResponseType(typeof(Edvanz.Application.Dtos.Result<Edvanz.Application.Dtos.Attendance.MonthlyAttendanceSummaryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(object), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetStudentTimelineMonth(
        [FromRoute] long studentId,
        [FromQuery] StudentTimelineMonthRequest request)
    {
        long? teacherId = await ResolveTeacherIdAsync();
        if (teacherId is null) return TeacherNotResolved();

        var result = await _attendanceService.GetStudentTimelineMonthAsync(teacherId.Value, studentId, request);
        return ToResponse(result);
    }

    /// ══════════════════════════════════════════════════════════════════════════
    /// ENDPOINT 15: GENERATE REPORT
    /// ══════════════════════════════════════════════════════════════════════════
    ///
    /// WHAT IT DOES:
    ///   Generates an attendance report. REQ-ATT-040 (6 report types).
    ///   REQ-ATT-042: Must complete within 5 seconds.
    ///
    /// TABLES READ: AttendanceRecords, TeacherStudents, Sessions, SessionLinks
    ///
    /// ══════════════════════════════════════════════════════════════════════════
    [HttpPost("reports")]
    [ModulePermission(AttendanceConstants.ModuleName, AttendanceConstants.PermissionGenerateReports)]
    [ProducesResponseType(typeof(Edvanz.Application.Dtos.Result<System.Collections.Generic.List<Edvanz.Application.Dtos.Attendance.AttendanceRecordDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(object), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GenerateReport([FromBody] AttendanceReportRequest request)
    {
        long? teacherId = await ResolveTeacherIdAsync();
        if (teacherId is null) return TeacherNotResolved();

        var result = await _attendanceService.GenerateReportAsync(teacherId.Value, request);
        return ToResponse(result);
    }



    /// ══════════════════════════════════════════════════════════════════════════
    /// ENDPOINT 18: HOLD STUDENT (FIX 4.1 — REQ-ATT-061)
    /// ══════════════════════════════════════════════════════════════════════════
    ///
    /// WHAT IT DOES:
    ///   Places a student on "hold" during attendance taking.
    ///   REQ-ATT-058: "Hold" cancels attendance recording, deferring to later.
    ///   REQ-ATT-061: Held students visually distinguished from marked/unmarked.
    ///
    /// TABLES WRITTEN: AttendanceRecords (Held status)
    /// TABLES READ: Sessions, TeacherStudents, SessionOccurrences
    ///
    /// ══════════════════════════════════════════════════════════════════════════
    [HttpPost("mark-hold")]
    [ModulePermission(AttendanceConstants.ModuleName, AttendanceConstants.PermissionTake)]
    [ProducesResponseType(typeof(Edvanz.Application.Dtos.Result<Edvanz.Application.Dtos.Attendance.MarkAttendanceResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(object), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(object), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> HoldStudent([FromBody] HoldStudentDto dto)
    {
        long? teacherId = await ResolveTeacherIdAsync();
        if (teacherId is null) return TeacherNotResolved();

        dto.TeacherId = teacherId.Value;
        dto.RecordedByUserId = GetActingUserId();

        var result = await _attendanceService.HoldStudentAsync(dto);
        return ToResponse(result);
    }

    /// ══════════════════════════════════════════════════════════════════════════
    /// ENDPOINT 19: RELEASE HOLD (FIX 4.1 — REQ-ATT-061)
    /// ══════════════════════════════════════════════════════════════════════════
    ///
    /// WHAT IT DOES:
    ///   Releases a held student — marks as Present or discards the hold.
    ///   REQ-ATT-061: Held students can be returned to and processed later.
    ///
    /// TABLES WRITTEN: AttendanceRecords, StudentAbsenceCounters
    /// TABLES READ: AttendanceRecords
    ///
    /// ══════════════════════════════════════════════════════════════════════════
    [HttpPost("release-hold")]
    [ModulePermission(AttendanceConstants.ModuleName, AttendanceConstants.PermissionTake)]
    [ProducesResponseType(typeof(Edvanz.Application.Dtos.Result<Edvanz.Application.Dtos.Attendance.MarkAttendanceResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(object), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(object), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ReleaseHold([FromBody] ReleaseHoldDto dto)
    {
        long? teacherId = await ResolveTeacherIdAsync();
        if (teacherId is null) return TeacherNotResolved();

        dto.TeacherId = teacherId.Value;
        dto.RecordedByUserId = GetActingUserId();

        var result = await _attendanceService.ReleaseHoldAsync(dto);
        return ToResponse(result);
    }
    /// ══════════════════════════════════════════════════════════════════════════
    /// ENDPOINT 20: EXPORT REPORT (FIX 4.2 — REQ-ATT-041)
    /// ══════════════════════════════════════════════════════════════════════════
    ///
    /// WHAT IT DOES:
    ///   Exports an attendance report as a downloadable PDF or Excel file.
    ///   REQ-ATT-041: All reports exportable as PDF or Excel (.xlsx).
    ///   REQ-ATT-042: Must complete within 5 seconds for up to 50K students.
    ///
    /// TABLES READ: AttendanceRecords, TeacherStudents, Sessions, SessionLinks
    ///
    /// ══════════════════════════════════════════════════════════════════════════
    [HttpPost("reports/export")]
    [ModulePermission(AttendanceConstants.ModuleName, AttendanceConstants.PermissionGenerateReports)]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(object), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ExportReport(
         [FromBody] AttendanceReportRequest request,
         [FromQuery] string format = "xlsx")
    {
        long? teacherId = await ResolveTeacherIdAsync();
        if (teacherId is null) return TeacherNotResolved();

        var result = await _attendanceService.ExportReportAsync(teacherId.Value, request, format);
        if (!result.IsSuccess)
            return ToResponse(result);

        string contentType = format.ToLower() == "pdf"
            ? "application/pdf"
            : "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
        string extension = format.ToLower() == "pdf" ? "pdf" : "xlsx";

        return File(result.Data!, contentType, $"attendance_report.{extension}");
    }
    /// ══════════════════════════════════════════════════════════════════════════
    /// ENDPOINT 21: EXPORT STUDENT TIMELINE (FIX 4.2 — REQ-ATT-081)
    /// ══════════════════════════════════════════════════════════════════════════
    ///
    /// WHAT IT DOES:
    ///   Exports a student's attendance timeline as a downloadable PDF or Excel file.
    ///   REQ-ATT-081: Preserves month-by-month structure with summary totals.
    ///
    /// TABLES READ: TeacherStudents, StudentAbsenceCounters, AttendanceRecords
    ///
    /// ══════════════════════════════════════════════════════════════════════════
    [HttpGet("timeline/students/{studentId:long}/export")]
    [ModulePermission(AttendanceConstants.ModuleName, AttendanceConstants.PermissionGenerateReports)]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(object), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ExportTimeline(
         [FromRoute] long studentId,
         [FromQuery] DateTime? startDate,
         [FromQuery] DateTime? endDate,
         [FromQuery] string format = "xlsx")
    {
        long? teacherId = await ResolveTeacherIdAsync();
        if (teacherId is null) return TeacherNotResolved();

        var result = await _attendanceService.ExportTimelineAsync(
            teacherId.Value, studentId, startDate, endDate, format);
        if (!result.IsSuccess)
            return ToResponse(result);

        string contentType = format.ToLower() == "pdf"
            ? "application/pdf"
            : "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
        string extension = format.ToLower() == "pdf" ? "pdf" : "xlsx";

        return File(result.Data!, contentType, $"attendance_timeline.{extension}");
    }

    /// ══════════════════════════════════════════════════════════════════════════
    /// ENDPOINT 22: SYNC OFFLINE RECORDS (FIX 4.3 — REQ-ATT-084/085)
    /// ══════════════════════════════════════════════════════════════════════════
    ///
    /// WHAT IT DOES:
    ///   Syncs offline-recorded attendance records to the server.
    ///   REQ-ATT-084: Automatic background sync when connectivity is restored.
    ///   REQ-ATT-085: Detects conflicts and returns both versions for resolution.
    ///
    /// TABLES WRITTEN: AttendanceRecords, StudentAbsenceCounters, SessionOccurrences
    /// TABLES READ: Sessions, TeacherStudents, SessionOccurrences, AttendanceRecords
    ///
    /// ══════════════════════════════════════════════════════════════════════════
    [HttpPost("sync")]
    [ModulePermission(AttendanceConstants.ModuleName, AttendanceConstants.PermissionTake)]
    [ProducesResponseType(typeof(Edvanz.Application.Dtos.Result<Edvanz.Application.Dtos.Attendance.SyncResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(object), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(object), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> SyncOfflineRecords([FromBody] OfflineSyncRequestDto dto)
    {
        long? teacherId = await ResolveTeacherIdAsync();
        if (teacherId is null) return TeacherNotResolved();

        dto.TeacherId = teacherId.Value;
        dto.RecordedByUserId = GetActingUserId();

        var result = await _attendanceService.SyncOfflineRecordsAsync(dto);
        return ToResponse(result);
    }

    /// ══════════════════════════════════════════════════════════════════════════
    /// ENDPOINT 23: GET UNMARKED COUNT (Audit Fix — REQ-ATT-055)
    /// ══════════════════════════════════════════════════════════════════════════
    ///
    /// WHAT IT DOES:
    ///   Returns the count of unmarked students for a session occurrence.
    ///   REQ-ATT-055: "Mark All Present" confirmation prompt shows affected count.
    ///
    /// TABLES READ: SessionOccurrences, StudentSessionAssignments, AttendanceRecords
    ///
    /// ══════════════════════════════════════════════════════════════════════════
    [HttpGet("sessions/{sessionId:long}/unmarked-count")]
    [ModulePermission(AttendanceConstants.ModuleName, AttendanceConstants.PermissionTake)]
    [ProducesResponseType(typeof(Edvanz.Application.Dtos.Result<int>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(object), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetUnmarkedCount(
         [FromRoute] long sessionId,
         [FromQuery] DateTime? occurrenceDate)
    {
        long? teacherId = await ResolveTeacherIdAsync();
        if (teacherId is null) return TeacherNotResolved();

        var result = await _attendanceService.GetUnmarkedCountAsync(teacherId.Value, sessionId, occurrenceDate);
        return ToResponse(result);
    }
}