using Edvanz.Domain.Resources;
using Microsoft.Extensions.Localization;
using Edvanz.API.Attributes;
using Edvanz.Application.IservicesContract;
using Edvanz.Application.ServiceContract;
using Edvanz.Domain.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Edvanz.API.Controllers;

/// <summary>
/// Module 6 (Exams &amp; Homework) — parent-facing READ endpoints.
///
/// SEPARATE CONTROLLER RATIONALE: mirrors <see cref="StudentAssignmentObligationsController"/> —
/// parents carry no module claim, and the service needs (teacherId, teacherStudentId), which this
/// controller resolves via <see cref="ParentScopedApiBaseController.ResolveChildForParentAsync"/>
/// (parent ownership of the child, then Method A/B teacher-link branch, AAM-FR-06.3).
///
/// SCOPE — OFFLINE EXAMS + HOMEWORK: offline exams reuse
/// <see cref="IExamHomeworkService.GetMyOfflineExamsAsync"/> COMPLETELY UNCHANGED (no
/// caller-identity branching, no visibility-flag check inside the method itself — matches the
/// same precedent already found for video and online-exam lists). Homework is NEW as of this
/// phase — the student side previously had no homework list endpoint anywhere in the app
/// (<c>StudentTeacherHomeService.GetTeacherHomeAsync</c> hardcoded
/// <c>Homework = new HomeHomeworkDto { Visible = vHomework, Count = 0 }</c>, "read surface not
/// built yet"); <see cref="IExamHomeworkService.GetMyHomeworkAsync"/> was built alongside its
/// student twin (<c>StudentAssignmentObligationsController.GetMyHomework</c>) in the same
/// change, not invented parent-side-only. Neither offline exams nor homework has a separate
/// result/review endpoint — teacher-graded, not self-service like video/online quizzes, so each
/// list item already carries the full outcome (Score/MaxGrade/Rank/Status for exams;
/// Grade/MaxGrade/TrackingMode/Status for homework).
///
/// AUTH: [ModulePermission(roles: ["Parent"], roleOnly: true)] — parent role only.
/// </summary>
[Route("api/assignmentobligations/parent")]
[Authorize]
public sealed class ParentAssignmentObligationsController : ParentScopedApiBaseController
{
    private readonly IExamHomeworkService _service;

    public ParentAssignmentObligationsController(
        IExamHomeworkService service,
        ICurrentUserService currentUser,
        IUnitOfWork unitOfWork,
        IStringLocalizer<Messages> localizer)
        : base(currentUser, unitOfWork, localizer)
    {
        _service = service;
    }

    // ──────────────────────────────────────────────────────────────────────
    // CHILD OFFLINE EXAM LIST
    // GET /api/assignmentobligations/parent/children/{childId}/teachers/{teacherId}/exams
    // ──────────────────────────────────────────────────────────────────────
    [HttpGet("children/{childId:long}/teachers/{teacherId:long}/exams")]
    [ModulePermission(roles: new[] { "Parent" }, roleOnly: true)]
    [ProducesResponseType(typeof(Edvanz.Application.Dtos.Result<Edvanz.Application.Dtos.PaginatedResponse<System.Collections.Generic.List<StudentOfflineExamListItemDto>>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(object), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetChildExams(
        [FromRoute] long childId, [FromRoute] long teacherId,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var resolution = await ResolveChildForParentAsync(childId, teacherId);
        if (resolution.ErrorResponse is not null) return resolution.ErrorResponse;

        var result = await _service.GetMyOfflineExamsAsync(
            teacherId, resolution.TeacherStudentId!.Value, resolution.ParentLanguagePreference, page, pageSize);
        return ToResponse(result);
    }

    // ──────────────────────────────────────────────────────────────────────
    // CHILD HOMEWORK LIST
    // GET /api/assignmentobligations/parent/children/{childId}/teachers/{teacherId}/homework
    // ──────────────────────────────────────────────────────────────────────
    [HttpGet("children/{childId:long}/teachers/{teacherId:long}/homework")]
    [ModulePermission(roles: new[] { "Parent" }, roleOnly: true)]
    [ProducesResponseType(typeof(Edvanz.Application.Dtos.Result<Edvanz.Application.Dtos.PaginatedResponse<System.Collections.Generic.List<Edvanz.Application.Dtos.ExamHomework.StudentHomeworkListItemDto>>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(object), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetChildHomework(
        [FromRoute] long childId, [FromRoute] long teacherId,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var resolution = await ResolveChildForParentAsync(childId, teacherId);
        if (resolution.ErrorResponse is not null) return resolution.ErrorResponse;

        var result = await _service.GetMyHomeworkAsync(
            teacherId, resolution.TeacherStudentId!.Value, resolution.ParentLanguagePreference, page, pageSize);
        return ToResponse(result);
    }
}