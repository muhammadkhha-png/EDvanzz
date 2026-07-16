using Edvanz.API.Attributes;
using Edvanz.Application.Dtos.ExamHomework;
using Edvanz.Application.IservicesContract;
using Edvanz.Application.ServiceContract;
using Edvanz.Domain.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Edvanz.API.Controllers;

/// <summary>
/// API controller for Module 6 (Exams &amp; Homework) — assignment template lifecycle
/// and scope management. Covers REQ-EXH-001 through 015, 020/021, 029, 033 through 037.
///
/// ROUTING: <c>api/assignmenttemplates/...</c> — matches the existing codebase convention
/// (no <c>v1</c> segment per the review decision).
///
/// AUTHORIZATION: every endpoint is <c>[Authorize]</c>. Permission checks are layered via
/// the existing <see cref="ModulePermissionAttribute"/>:
///   - Read endpoints: <c>"Exams And Homework", "View"</c>
///   - Write endpoints: <c>"Exams And Homework", "ManageAssignments"</c>
/// Audit-trail and management endpoints that should be tutor-only carry
/// <c>roles: new[] { "Teacher", "SuperAdmin" }, roleOnly: true</c>.
///
/// TENANT SCOPE: <c>teacherId</c> is resolved from the JWT in
/// <see cref="ModuleSixApiBaseController.ResolveTeacherIdAsync"/> — never from the body
/// or route (catalog §1.3).
/// </summary>
[Authorize]
public class AssignmentTemplatesController : ModuleSixApiBaseController
{
    private readonly IExamHomeworkService _service;

    public AssignmentTemplatesController(
        IExamHomeworkService service,
        ICurrentUserService currentUser,
        IUnitOfWork unitOfWork)
        : base(currentUser, unitOfWork)
    {
        _service = service;
    }

    // ══════════════════════════════════════════════════════════════════════════
    // ENDPOINT 1: CREATE TEMPLATE  (REQ-EXH-001 through 010, 014/015, 020/021)
    // POST /api/assignmenttemplates
    // ══════════════════════════════════════════════════════════════════════════
    [HttpPost]
    [ModulePermission("Exams And Homework", "ManageAssignments")]
    [ProducesResponseType(typeof(Edvanz.Application.Dtos.Result<Edvanz.Application.Dtos.ExamHomework.AssignmentTemplateDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CreateTemplate([FromBody] CreateAssignmentTemplateDto dto)
    {
        long? teacherId = await ResolveTeacherIdAsync();
        if (teacherId is null) return TeacherNotResolved();

        var result = await _service.CreateTemplateAsync(teacherId.Value, GetActingUserId(), dto);
        return ToResponse(result);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // ENDPOINT 2: ASSIGNMENT OVERVIEW LIST  (REQ-EXH-033)
    // GET /api/assignmenttemplates
    // ══════════════════════════════════════════════════════════════════════════
    [HttpGet]
    [ModulePermission("Exams And Homework", "View")]
    [ProducesResponseType(typeof(Edvanz.Application.Dtos.Result<Edvanz.Application.Dtos.PaginatedResponse<System.Collections.Generic.List<Edvanz.Application.Dtos.ExamHomework.AssignmentOverviewItemDto>>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetOverview([FromQuery] AssignmentOverviewRequest request)
    {
        long? teacherId = await ResolveTeacherIdAsync();
        if (teacherId is null) return TeacherNotResolved();

        var result = await _service.GetOverviewAsync(teacherId.Value, request);
        return ToResponse(result);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // ENDPOINT 3: GET TEMPLATE DETAIL  (REQ-EXH-029, 034)
    // GET /api/assignmenttemplates/{templateId}
    // ══════════════════════════════════════════════════════════════════════════
    [HttpGet("{templateId:long}")]
    [ModulePermission("Exams And Homework", "View")]
    [ProducesResponseType(typeof(Edvanz.Application.Dtos.Result<Edvanz.Application.Dtos.ExamHomework.AssignmentTemplateDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetTemplate([FromRoute] long templateId)
    {
        long? teacherId = await ResolveTeacherIdAsync();
        if (teacherId is null) return TeacherNotResolved();

        var result = await _service.GetTemplateByIdAsync(teacherId.Value, templateId);
        return ToResponse(result);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // ENDPOINT 4: UPDATE TEMPLATE  (REQ-EXH-013, 034)
    // PUT /api/assignmenttemplates/{templateId}
    // ══════════════════════════════════════════════════════════════════════════
    [HttpPut("{templateId:long}")]
    [ModulePermission("Exams And Homework", "ManageAssignments")]
    [ProducesResponseType(typeof(Edvanz.Application.Dtos.Result<Edvanz.Application.Dtos.ExamHomework.AssignmentTemplateDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(object), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(object), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> UpdateTemplate(
        [FromRoute] long templateId, [FromBody] UpdateAssignmentTemplateDto dto)
    {
        long? teacherId = await ResolveTeacherIdAsync();
        if (teacherId is null) return TeacherNotResolved();

        var result = await _service.UpdateTemplateAsync(
            teacherId.Value, GetActingUserId(), templateId, dto);
        return ToResponse(result);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // ENDPOINT 5: DELETE TEMPLATE  (REQ-EXH-037)
    // DELETE /api/assignmenttemplates/{templateId}
    //   Body: { "confirm": true }   ← intentional: query string would be cacheable
    // ══════════════════════════════════════════════════════════════════════════
    [HttpDelete("{templateId:long}")]
    [ProducesResponseType(typeof(Edvanz.Application.Dtos.Result<bool>), StatusCodes.Status200OK)]
    [ModulePermission("Exams And Homework", "ManageAssignments")]
    [ProducesResponseType(typeof(object), StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteTemplate(
        [FromRoute] long templateId, [FromBody] DeleteConfirmationDto dto)
    {
        long? teacherId = await ResolveTeacherIdAsync();
        if (teacherId is null) return TeacherNotResolved();

        var result = await _service.DeleteTemplateAsync(
            teacherId.Value, GetActingUserId(), templateId, dto.Confirm);
        return ToResponse(result);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // ENDPOINT 6: STOP RECURRENCE  (REQ-EXH-012, BR-EXH-002)
    // POST /api/assignmenttemplates/{templateId}/stop-recurrence
    // ══════════════════════════════════════════════════════════════════════════
    [HttpPost("{templateId:long}/stop-recurrence")]
    [ModulePermission("Exams And Homework", "ManageAssignments")]
    [ProducesResponseType(typeof(Edvanz.Application.Dtos.Result<Edvanz.Application.Dtos.ExamHomework.StopRecurrenceResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(object), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> StopRecurrence(
        [FromRoute] long templateId, [FromBody] StopRecurrenceDto dto)
    {
        long? teacherId = await ResolveTeacherIdAsync();
        if (teacherId is null) return TeacherNotResolved();

        var result = await _service.StopRecurrenceAsync(
            teacherId.Value, GetActingUserId(), templateId, dto);
        return ToResponse(result);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // ENDPOINT 7: LIST OCCURRENCES OF A TEMPLATE  (REQ-EXH-011)
    // GET /api/assignmenttemplates/{templateId}/occurrences
    // ══════════════════════════════════════════════════════════════════════════
    [HttpGet("{templateId:long}/occurrences")]
    [ModulePermission("Exams And Homework", "View")]
    [ProducesResponseType(typeof(Edvanz.Application.Dtos.Result<Edvanz.Application.Dtos.PaginatedResponse<System.Collections.Generic.List<Edvanz.Application.Dtos.ExamHomework.OccurrenceSummaryItemDto>>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetOccurrences(
        [FromRoute] long templateId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        long? teacherId = await ResolveTeacherIdAsync();
        if (teacherId is null) return TeacherNotResolved();

        var result = await _service.GetOccurrencesAsync(teacherId.Value, templateId, page, pageSize);
        return ToResponse(result);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // ENDPOINT 8: ADD SCOPES  (REQ-EXH-035)
    // POST /api/assignmenttemplates/{templateId}/scopes
    // ══════════════════════════════════════════════════════════════════════════
    [HttpPost("{templateId:long}/scopes")]
    [ModulePermission("Exams And Homework", "ManageAssignments")]
    [ProducesResponseType(typeof(Edvanz.Application.Dtos.Result<Edvanz.Application.Dtos.ExamHomework.AddScopesResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AddScopes(
        [FromRoute] long templateId, [FromBody] AddScopesDto dto)
    {
        long? teacherId = await ResolveTeacherIdAsync();
        if (teacherId is null) return TeacherNotResolved();

        var result = await _service.AddScopesAsync(
            teacherId.Value, GetActingUserId(), templateId, dto);
        return ToResponse(result);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // ENDPOINT 9: REMOVE A SCOPE  (REQ-EXH-034)
    // DELETE /api/assignmenttemplates/{templateId}/scopes/{scopeId}
    // ══════════════════════════════════════════════════════════════════════════
    [HttpDelete("{templateId:long}/scopes/{scopeId:long}")]
    [ProducesResponseType(typeof(Edvanz.Application.Dtos.Result<bool>), StatusCodes.Status200OK)]
    [ModulePermission("Exams And Homework", "ManageAssignments")]
    [ProducesResponseType(typeof(object), StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RemoveScope(
        [FromRoute] long templateId, [FromRoute] long scopeId)
    {
        long? teacherId = await ResolveTeacherIdAsync();
        if (teacherId is null) return TeacherNotResolved();

        var result = await _service.RemoveScopeAsync(
            teacherId.Value, GetActingUserId(), templateId, scopeId);
        return ToResponse(result);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // ENDPOINT 10: ADD STUDENTS  (REQ-EXH-035, BR-EXH-005)
    // POST /api/assignmenttemplates/{templateId}/students
    // ══════════════════════════════════════════════════════════════════════════
    [HttpPost("{templateId:long}/students")]
    [ModulePermission("Exams And Homework", "ManageAssignments")]
    [ProducesResponseType(typeof(Edvanz.Application.Dtos.Result<Edvanz.Application.Dtos.ExamHomework.AddStudentsResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AddStudents(
        [FromRoute] long templateId, [FromBody] AddStudentsToTemplateDto dto)
    {
        long? teacherId = await ResolveTeacherIdAsync();
        if (teacherId is null) return TeacherNotResolved();

        var result = await _service.AddStudentsToTemplateAsync(
            teacherId.Value, GetActingUserId(), templateId, dto);
        return ToResponse(result);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // ENDPOINT 11: ELIGIBLE STUDENTS PICKER  (REQ-EXH-035)
    // GET /api/assignmenttemplates/{templateId}/eligible-students
    // ══════════════════════════════════════════════════════════════════════════
    [HttpGet("{templateId:long}/eligible-students")]
    [ModulePermission("Exams And Homework", "ManageAssignments")]
    [ProducesResponseType(typeof(Edvanz.Application.Dtos.Result<Edvanz.Application.Dtos.PaginatedResponse<System.Collections.Generic.List<Edvanz.Application.Dtos.ExamHomework.EligibleStudentDto>>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetEligibleStudents(
        [FromRoute] long templateId, [FromQuery] EligibleStudentsRequest request)
    {
        long? teacherId = await ResolveTeacherIdAsync();
        if (teacherId is null) return TeacherNotResolved();

        var result = await _service.GetEligibleStudentsAsync(teacherId.Value, templateId, request);
        return ToResponse(result);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // ENDPOINT 12: REMOVE STUDENT FROM TEMPLATE  (REQ-EXH-036)
    // DELETE /api/assignmenttemplates/{templateId}/students/{teacherStudentId}?force=false
    // ══════════════════════════════════════════════════════════════════════════
    [HttpDelete("{templateId:long}/students/{teacherStudentId:long}")]
    [ModulePermission("Exams And Homework", "ManageAssignments")]
    [ProducesResponseType(typeof(Edvanz.Application.Dtos.Result<Edvanz.Application.Dtos.ExamHomework.RemoveStudentResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(object), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> RemoveStudent(
        [FromRoute] long templateId,
        [FromRoute] long teacherStudentId,
        [FromQuery] bool force = false)
    {
        long? teacherId = await ResolveTeacherIdAsync();
        if (teacherId is null) return TeacherNotResolved();

        var result = await _service.RemoveStudentFromTemplateAsync(
            teacherId.Value, GetActingUserId(), templateId, teacherStudentId, force);
        return ToResponse(result);
    }
}

/// <summary>
/// Body DTO for the DELETE template endpoint. Confirmation is in the body, not the
/// query string, per the review note (DELETE confirmations sent via query become
/// cacheable; placing them in the body is safer even if non-RESTful).
/// </summary>
public class DeleteConfirmationDto
{
    public bool Confirm { get; set; }
}
