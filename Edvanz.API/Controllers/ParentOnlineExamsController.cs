using Edvanz.Domain.Resources;
using Microsoft.Extensions.Localization;
using Edvanz.API.Attributes;
using Edvanz.Application.Dtos;
using Edvanz.Application.IservicesContract;
using Edvanz.Application.ServiceContract;
using Edvanz.Domain.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Edvanz.API.Controllers;

/// <summary>
/// Online Exam Module — parent-facing READ endpoints.
///
/// SEPARATE CONTROLLER RATIONALE: mirrors <see cref="StudentOnlineExamsController"/> — parents
/// carry no module claim, and the service needs (teacherId, teacherStudentId), which this
/// controller resolves via <see cref="ParentScopedApiBaseController.ResolveChildForParentAsync"/>
/// (parent ownership of the child, then Method A/B teacher-link branch, AAM-FR-06.3).
///
/// REUSES <see cref="IStudentOnlineExamService"/> COMPLETELY UNCHANGED — GetMyExamsAsync,
/// GetResultAsync, and GetReviewAsync are all pure reads keyed purely by (teacherId,
/// teacherStudentId[, onlineExamId]) with no caller-identity branching (confirmed by reading
/// every one of their bodies before writing this controller) — there is nothing for a
/// dedicated parent-named service method to add, so none was added. Same reasoning already
/// applied to <see cref="ParentVideoExamsController"/>'s result/review reuse (Phase 5).
///
/// NOTE: GetMyExamsAsync has no BR-ADM-010 module-active gate in this module (unlike Video's
/// CheckModuleActiveAsync) — a pre-existing inconsistency, faithfully mirrored here (not
/// introduced, not fixed — out of scope, flagged for a future pass).
///
/// READ-ONLY (D6 — locked decision, parent-parity phase plan): NO take-screen, answer-submit,
/// exam-submit, self-block, or violation-recording. A parent never starts, saves progress on,
/// or submits their child's exam.
///
/// AUTH: [ModulePermission(roles: ["Parent"], roleOnly: true)] — parent role only.
/// </summary>
[Route("api/online-exams/parent")]
[Authorize]
public sealed class ParentOnlineExamsController : ParentScopedApiBaseController
{
    private readonly IStudentOnlineExamService _service;

    public ParentOnlineExamsController(
        IStudentOnlineExamService service,
        ICurrentUserService currentUser,
        IUnitOfWork unitOfWork,
        IStringLocalizer<Messages> localizer)
        : base(currentUser, unitOfWork, localizer)
    {
        _service = service;
    }

    // ──────────────────────────────────────────────────────────────────────
    // CHILD EXAM LIST (upcoming/past split)
    // GET /api/online-exams/parent/children/{childId}/teachers/{teacherId}
    // ──────────────────────────────────────────────────────────────────────
    [HttpGet("children/{childId:long}/teachers/{teacherId:long}")]
    [ModulePermission(roles: new[] { "Parent" }, roleOnly: true)]
    [ProducesResponseType(typeof(Edvanz.Application.Dtos.Result<StudentOnlineExamListDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(object), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetChildExams([FromRoute] long childId, [FromRoute] long teacherId)
    {
        var resolution = await ResolveChildForParentAsync(childId, teacherId);
        if (resolution.ErrorResponse is not null) return resolution.ErrorResponse;

        var result = await _service.GetMyExamsAsync(
            teacherId, resolution.TeacherStudentId!.Value, resolution.ParentLanguagePreference);
        return ToResponse(result);
    }

    // ──────────────────────────────────────────────────────────────────────
    // CHILD EXAM RESULT
    // GET /api/online-exams/parent/children/{childId}/teachers/{teacherId}/{onlineExamId}/result
    // ──────────────────────────────────────────────────────────────────────
    [HttpGet("children/{childId:long}/teachers/{teacherId:long}/{onlineExamId:long}/result")]
    [ModulePermission(roles: new[] { "Parent" }, roleOnly: true)]
    [ProducesResponseType(typeof(Edvanz.Application.Dtos.Result<OnlineExamStatsDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(object), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetChildResult(
        [FromRoute] long childId, [FromRoute] long teacherId, [FromRoute] long onlineExamId)
    {
        var resolution = await ResolveChildForParentAsync(childId, teacherId);
        if (resolution.ErrorResponse is not null) return resolution.ErrorResponse;

        var result = await _service.GetResultAsync(teacherId, resolution.TeacherStudentId!.Value, onlineExamId);
        return ToResponse(result);
    }

    // ──────────────────────────────────────────────────────────────────────
    // CHILD EXAM REVIEW (answers) — matches the student route's own "answers"
    // naming (not "review" — each module keeps its own established suffix).
    // GET /api/online-exams/parent/children/{childId}/teachers/{teacherId}/{onlineExamId}/answers
    // ──────────────────────────────────────────────────────────────────────
    [HttpGet("children/{childId:long}/teachers/{teacherId:long}/{onlineExamId:long}/answers")]
    [ModulePermission(roles: new[] { "Parent" }, roleOnly: true)]
    [ProducesResponseType(typeof(Edvanz.Application.Dtos.Result<OnlineExamReviewDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(object), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetChildReview(
        [FromRoute] long childId, [FromRoute] long teacherId, [FromRoute] long onlineExamId)
    {
        var resolution = await ResolveChildForParentAsync(childId, teacherId);
        if (resolution.ErrorResponse is not null) return resolution.ErrorResponse;

        var result = await _service.GetReviewAsync(teacherId, resolution.TeacherStudentId!.Value, onlineExamId);
        return ToResponse(result);
    }
}