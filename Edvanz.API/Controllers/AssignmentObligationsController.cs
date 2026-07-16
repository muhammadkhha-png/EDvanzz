using Edvanz.API.Attributes;
using Edvanz.Application.Dtos.ExamHomework;
using Edvanz.Application.IservicesContract;
using Edvanz.Application.ServiceContract;
using Edvanz.Domain.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Edvanz.API.Controllers;

/// <summary>
/// API controller for Module 6 — occurrence-level tracking, status entry, barcode scan,
/// grading, and audit history. Covers REQ-EXH-011, 023 through 030, REQ-EXH-NFR-001/002.
///
/// ROUTING: <c>api/assignmentobligations/...</c> — matches existing convention.
///
/// AUTHORIZATION:
///   - Read endpoints: <c>"Exams And Homework", "View"</c>
///   - Status / scan / bulk: <c>"Exams And Homework", "RecordExamAttendanceAndGrades"</c>
///     OR <c>"RecordHomeworkCompletion"</c> — Option B (catalog §3.2): the route itself
///     disambiguates exam vs homework, so each endpoint carries the correct permission.
///   - Grade entry: <c>"RecordExamAttendanceAndGrades"</c> (REQ-EXH-026-D / 027).
///
/// TENANT SCOPE: <c>teacherId</c> resolved from JWT in the base controller.
/// </summary>
[Authorize]
public class AssignmentObligationsController : ModuleSixApiBaseController
{
    private readonly IExamHomeworkService _service;

    public AssignmentObligationsController(
        IExamHomeworkService service,
        ICurrentUserService currentUser,
        IUnitOfWork unitOfWork)
        : base(currentUser, unitOfWork)
    {
        _service = service;
    }

    // ══════════════════════════════════════════════════════════════════════════
    // ENDPOINT 13: GET OCCURRENCE DETAIL  (REQ-EXH-011, 029)
    // GET /api/assignmentobligations/occurrences/{occurrenceId}
    // ══════════════════════════════════════════════════════════════════════════
    [HttpGet("occurrences/{occurrenceId:long}")]
    [ModulePermission("Exams And Homework", "View")]
    [ProducesResponseType(typeof(Edvanz.Application.Dtos.Result<Edvanz.Application.Dtos.ExamHomework.OccurrenceDetailDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetOccurrence([FromRoute] long occurrenceId)
    {
        long? teacherId = await ResolveTeacherIdAsync();
        if (teacherId is null) return TeacherNotResolved();

        var result = await _service.GetOccurrenceAsync(teacherId.Value, occurrenceId);
        return ToResponse(result);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // ENDPOINT 14: OCCURRENCE SUMMARY (HEADER COUNTS)  (REQ-EXH-029)
    // GET /api/assignmentobligations/occurrences/{occurrenceId}/summary
    // ══════════════════════════════════════════════════════════════════════════
    [HttpGet("occurrences/{occurrenceId:long}/summary")]
    [ModulePermission("Exams And Homework", "View")]
    [ProducesResponseType(typeof(Edvanz.Application.Dtos.Result<Edvanz.Application.Dtos.ExamHomework.OccurrenceSummaryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetOccurrenceSummary([FromRoute] long occurrenceId)
    {
        long? teacherId = await ResolveTeacherIdAsync();
        if (teacherId is null) return TeacherNotResolved();

        var result = await _service.GetOccurrenceSummaryAsync(teacherId.Value, occurrenceId);
        return ToResponse(result);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // ENDPOINT 15: TRACKING VIEW LIST  (REQ-EXH-030/031/032)
    // GET /api/assignmentobligations/occurrences/{occurrenceId}/tracking
    // ══════════════════════════════════════════════════════════════════════════
    [HttpGet("occurrences/{occurrenceId:long}/tracking")]
    [ModulePermission("Exams And Homework", "View")]
    [ProducesResponseType(typeof(Edvanz.Application.Dtos.Result<Edvanz.Application.Dtos.PaginatedResponse<System.Collections.Generic.List<Edvanz.Application.Dtos.ExamHomework.TrackingViewRowDto>>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetTrackingView(
        [FromRoute] long occurrenceId, [FromQuery] TrackingViewRequest request)
    {
        long? teacherId = await ResolveTeacherIdAsync();
        if (teacherId is null) return TeacherNotResolved();

        var result = await _service.GetTrackingViewAsync(teacherId.Value, occurrenceId, request);
        return ToResponse(result);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // ENDPOINT 16: STUDENT PICKER (TYPEAHEAD)  (REQ-EXH-023)
    // GET /api/assignmentobligations/occurrences/{occurrenceId}/students/search
    // ══════════════════════════════════════════════════════════════════════════
    [HttpGet("occurrences/{occurrenceId:long}/students/search")]
    [ModulePermission("Exams And Homework", "View")]
    [ProducesResponseType(typeof(Edvanz.Application.Dtos.Result<System.Collections.Generic.List<Edvanz.Application.Dtos.ExamHomework.StudentPickerRowDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SearchStudents(
        [FromRoute] long occurrenceId,
        [FromQuery] string q = "",
        [FromQuery] int limit = 10)
    {
        long? teacherId = await ResolveTeacherIdAsync();
        if (teacherId is null) return TeacherNotResolved();

        var result = await _service.SearchStudentsInOccurrenceAsync(
            teacherId.Value, occurrenceId, q, limit);
        return ToResponse(result);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // ENDPOINT 17: UPDATE EXAM STATUS  (REQ-EXH-023, 028 — Option B exam variant)
    // PUT /api/assignmentobligations/occurrences/{occurrenceId}/students/{teacherStudentId}/exam-status
    // ══════════════════════════════════════════════════════════════════════════
    [HttpPut("occurrences/{occurrenceId:long}/students/{teacherStudentId:long}/exam-status")]
    [ModulePermission("Exams And Homework", "RecordExamAttendanceAndGrades")]
    [ProducesResponseType(typeof(Edvanz.Application.Dtos.Result<Edvanz.Application.Dtos.ExamHomework.TrackingViewRowDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(object), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UpdateExamStatus(
        [FromRoute] long occurrenceId,
        [FromRoute] long teacherStudentId,
        [FromBody] UpdateExamStatusDto dto)
    {
        long? teacherId = await ResolveTeacherIdAsync();
        if (teacherId is null) return TeacherNotResolved();

        var result = await _service.UpdateExamStatusAsync(
            teacherId.Value, GetActingUserId(), occurrenceId, teacherStudentId, dto);
        return ToResponse(result);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // ENDPOINT 18: UPDATE HOMEWORK STATUS  (REQ-EXH-023, 028 — Option B HW variant)
    // PUT /api/assignmentobligations/occurrences/{occurrenceId}/students/{teacherStudentId}/homework-status
    // ══════════════════════════════════════════════════════════════════════════
    [HttpPut("occurrences/{occurrenceId:long}/students/{teacherStudentId:long}/homework-status")]
    [ModulePermission("Exams And Homework", "RecordHomeworkCompletion")]
    [ProducesResponseType(typeof(Edvanz.Application.Dtos.Result<Edvanz.Application.Dtos.ExamHomework.TrackingViewRowDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(object), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UpdateHomeworkStatus(
        [FromRoute] long occurrenceId,
        [FromRoute] long teacherStudentId,
        [FromBody] UpdateHomeworkStatusDto dto)
    {
        long? teacherId = await ResolveTeacherIdAsync();
        if (teacherId is null) return TeacherNotResolved();

        var result = await _service.UpdateHomeworkStatusAsync(
            teacherId.Value, GetActingUserId(), occurrenceId, teacherStudentId, dto);
        return ToResponse(result);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // ENDPOINT 19: EXAM STATUS BY STUDENT CODE  (REQ-EXH-024)
    // POST /api/assignmentobligations/occurrences/{occurrenceId}/exam-status/by-code
    // ══════════════════════════════════════════════════════════════════════════
    [HttpPost("occurrences/{occurrenceId:long}/exam-status/by-code")]
    [ModulePermission("Exams And Homework", "RecordExamAttendanceAndGrades")]
    [ProducesResponseType(typeof(Edvanz.Application.Dtos.Result<Edvanz.Application.Dtos.ExamHomework.TrackingViewRowDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(object), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UpdateExamStatusByCode(
        [FromRoute] long occurrenceId, [FromBody] UpdateStatusByCodeDto dto)
    {
        long? teacherId = await ResolveTeacherIdAsync();
        if (teacherId is null) return TeacherNotResolved();

        var result = await _service.UpdateExamStatusByCodeAsync(
            teacherId.Value, GetActingUserId(), occurrenceId, dto);
        return ToResponse(result);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // ENDPOINT 20: HOMEWORK STATUS BY STUDENT CODE  (REQ-EXH-024)
    // POST /api/assignmentobligations/occurrences/{occurrenceId}/homework-status/by-code
    // ══════════════════════════════════════════════════════════════════════════
    [HttpPost("occurrences/{occurrenceId:long}/homework-status/by-code")]
    [ModulePermission("Exams And Homework", "RecordHomeworkCompletion")]
    [ProducesResponseType(typeof(Edvanz.Application.Dtos.Result<Edvanz.Application.Dtos.ExamHomework.TrackingViewRowDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(object), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UpdateHomeworkStatusByCode(
        [FromRoute] long occurrenceId, [FromBody] UpdateStatusByCodeDto dto)
    {
        long? teacherId = await ResolveTeacherIdAsync();
        if (teacherId is null) return TeacherNotResolved();

        var result = await _service.UpdateHomeworkStatusByCodeAsync(
            teacherId.Value, GetActingUserId(), occurrenceId, dto);
        return ToResponse(result);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // ENDPOINT 21: BULK STATUS UPDATE  (REQ-EXH-025)
    // POST /api/assignmentobligations/occurrences/{occurrenceId}/bulk-status
    //
    // Accepts both exam and homework statuses; service validates against the
    // occurrence type. Permission check uses RecordExamAttendanceAndGrades because
    // bulk update is most often used for grade-bearing operations; the alternate
    // permission is enforced server-side via status validation.
    // ══════════════════════════════════════════════════════════════════════════
    [HttpPost("occurrences/{occurrenceId:long}/bulk-status")]
    [ModulePermission("Exams And Homework", "RecordExamAttendanceAndGrades")]
    [ProducesResponseType(typeof(Edvanz.Application.Dtos.Result<Edvanz.Application.Dtos.ExamHomework.BulkStatusResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(object), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> BulkUpdateStatus(
        [FromRoute] long occurrenceId, [FromBody] BulkUpdateStatusDto dto)
    {
        long? teacherId = await ResolveTeacherIdAsync();
        if (teacherId is null) return TeacherNotResolved();

        var result = await _service.BulkUpdateStatusAsync(
            teacherId.Value, GetActingUserId(), occurrenceId, dto);
        return ToResponse(result);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // ENDPOINT 22: BARCODE SCAN  (REQ-EXH-026, REQ-EXH-NFR-002)
    // POST /api/assignmentobligations/occurrences/{occurrenceId}/scan
    //
    // scannedAt is server-stamped. The atomic conditional UPDATE in the repo
    // ensures duplicate scans return 200 with AlreadyProcessed=true, no exception.
    // ══════════════════════════════════════════════════════════════════════════
    [HttpPost("occurrences/{occurrenceId:long}/scan")]
    [ModulePermission("Exams And Homework", "RecordExamAttendanceAndGrades")]
    [ProducesResponseType(typeof(Edvanz.Application.Dtos.Result<Edvanz.Application.Dtos.ExamHomework.ScanResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ScanBarcode(
        [FromRoute] long occurrenceId, [FromBody] ScanBarcodeDto dto)
    {
        long? teacherId = await ResolveTeacherIdAsync();
        if (teacherId is null) return TeacherNotResolved();

        var result = await _service.ScanBarcodeAsync(
            teacherId.Value, GetActingUserId(), occurrenceId, dto);
        return ToResponse(result);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // ENDPOINT 23: GRADE ENTRY VIEW  (REQ-EXH-026-A, 026-C)
    // GET /api/assignmentobligations/occurrences/{occurrenceId}/pending-grades
    // ══════════════════════════════════════════════════════════════════════════
    [HttpGet("occurrences/{occurrenceId:long}/pending-grades")]
    [ModulePermission("Exams And Homework", "RecordExamAttendanceAndGrades")]
    [ProducesResponseType(typeof(Edvanz.Application.Dtos.Result<Edvanz.Application.Dtos.ExamHomework.GradeEntryViewDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetPendingGrades(
        [FromRoute] long occurrenceId, [FromQuery] GradeEntryRequest request)
    {
        long? teacherId = await ResolveTeacherIdAsync();
        if (teacherId is null) return TeacherNotResolved();

        var result = await _service.GetPendingGradesAsync(teacherId.Value, occurrenceId, request);
        return ToResponse(result);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // ENDPOINT 24: ENTER / AMEND GRADE  (REQ-EXH-026-D / 026-E / 027)
    // PUT /api/assignmentobligations/{obligationId}/grade
    // ══════════════════════════════════════════════════════════════════════════
    [HttpPut("{obligationId:long}/grade")]
    [ModulePermission("Exams And Homework", "RecordExamAttendanceAndGrades")]
    [ProducesResponseType(typeof(Edvanz.Application.Dtos.Result<Edvanz.Application.Dtos.ExamHomework.TrackingViewRowDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(object), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(object), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> EnterGrade(
        [FromRoute] long obligationId, [FromBody] EnterGradeDto dto)
    {
        long? teacherId = await ResolveTeacherIdAsync();
        if (teacherId is null) return TeacherNotResolved();

        var result = await _service.EnterGradeAsync(
            teacherId.Value, GetActingUserId(), obligationId, dto);
        return ToResponse(result);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // ENDPOINT 25: REMOVE OBLIGATION (EXCUSE FROM ONE OCCURRENCE)  (REQ-EXH-036)
    // DELETE /api/assignmentobligations/{obligationId}?force=false
    // ══════════════════════════════════════════════════════════════════════════
    [HttpDelete("{obligationId:long}")]
    [ProducesResponseType(typeof(Edvanz.Application.Dtos.Result<bool>), StatusCodes.Status200OK)]
    [ModulePermission("Exams And Homework", "ManageAssignments")]
    [ProducesResponseType(typeof(object), StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(object), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> RemoveObligation(
        [FromRoute] long obligationId, [FromQuery] bool force = false)
    {
        long? teacherId = await ResolveTeacherIdAsync();
        if (teacherId is null) return TeacherNotResolved();

        var result = await _service.RemoveObligationAsync(
            teacherId.Value, GetActingUserId(), obligationId, force);
        return ToResponse(result);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // ENDPOINT 26: AUDIT HISTORY FOR ONE OBLIGATION  (REQ-EXH-028)
    // GET /api/assignmentobligations/{obligationId}/audit-log
    //
    // Tutor-only per the review note (assistants don't see other assistants' edits).
    // ══════════════════════════════════════════════════════════════════════════
    [HttpGet("{obligationId:long}/audit-log")]
    [ModulePermission(roles: new[] { "Teacher", "SuperAdmin" }, roleOnly: true)]
    [ProducesResponseType(typeof(Edvanz.Application.Dtos.Result<Edvanz.Application.Dtos.PaginatedResponse<System.Collections.Generic.List<Edvanz.Application.Dtos.ExamHomework.ObligationAuditEntryDto>>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetAuditLog(
        [FromRoute] long obligationId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        long? teacherId = await ResolveTeacherIdAsync();
        if (teacherId is null) return TeacherNotResolved();

        var result = await _service.GetObligationAuditLogAsync(
            teacherId.Value, obligationId, page, pageSize);
        return ToResponse(result);
    }
}
