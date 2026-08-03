using Edvanz.API.Attributes;
using Edvanz.Application.Dtos.VideoContentManagement;
using Edvanz.Application.IservicesContract;
using Edvanz.Application.ServiceContract;
using Edvanz.Domain.Interfaces;
using Edvanz.Domain.Resources;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;

namespace Edvanz.API.Controllers;

/// <summary>
/// Video Content Management Module (Module 14) — parent-facing VIDEO-QUIZ READ endpoints.
///
/// Separate controller from <see cref="ParentVideosController"/> (which owns list/units),
/// mirroring the student split (<see cref="StudentVideoExamsController"/> vs
/// <see cref="StudentVideosController"/>). Owns ONLY the quiz result + review reads for a
/// video's VideoExam — reuses <see cref="IStudentVideoExamService.GetResultAsync"/> and
/// <see cref="IStudentVideoExamService.GetReviewAsync"/> UNCHANGED: both are pure reads keyed
/// purely by (teacherId, teacherStudentId, videoAssetId) with no caller-identity branching, so
/// there is nothing for a dedicated parent-named service method to add — the interface stays
/// exactly as it is.
///
/// READ-ONLY (D6 — locked decision): NO take-screen, submit, or retry. A parent never sees the
/// answer key before their child has attempted the quiz (review only reveals correctness once
/// the child's OWN attempt is finalized, same rule as the student path), and a parent can never
/// start or reset the child's attempt.
///
/// AUTH: [ModulePermission(roles: ["Parent"], roleOnly: true)] — parent role only.
/// </summary>
[Route("api/videos/parent")]
[Authorize]
public sealed class ParentVideoExamsController : ParentScopedApiBaseController
{
    private readonly IStudentVideoExamService _service;

    public ParentVideoExamsController(
        IStudentVideoExamService service,
        ICurrentUserService currentUser,
        IUnitOfWork unitOfWork,
        IStringLocalizer<Messages> localizer)
        : base(currentUser, unitOfWork, localizer)
    {
        _service = service;
    }

    // ──────────────────────────────────────────────────────────────────────
    // CHILD VIDEO-QUIZ RESULT
    // GET /api/videos/parent/children/{childId}/teachers/{teacherId}/videos/{videoAssetId}/exam/result
    // ──────────────────────────────────────────────────────────────────────
    [HttpGet("children/{childId:long}/teachers/{teacherId:long}/videos/{videoAssetId:long}/exam/result")]
    [ModulePermission(roles: new[] { "Parent" }, roleOnly: true)]
    [ProducesResponseType(typeof(Edvanz.Application.Dtos.Result<VideoExamStatsDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(object), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetChildResult(
        [FromRoute] long childId, [FromRoute] long teacherId, [FromRoute] long videoAssetId)
    {
        var resolution = await ResolveChildForParentAsync(childId, teacherId);
        if (resolution.ErrorResponse is not null) return resolution.ErrorResponse;

        var result = await _service.GetResultAsync(teacherId, resolution.TeacherStudentId!.Value, videoAssetId);
        return ToResponse(result);
    }

    // ──────────────────────────────────────────────────────────────────────
    // CHILD VIDEO-QUIZ REVIEW
    // GET /api/videos/parent/children/{childId}/teachers/{teacherId}/videos/{videoAssetId}/exam/review
    // ──────────────────────────────────────────────────────────────────────
    [HttpGet("children/{childId:long}/teachers/{teacherId:long}/videos/{videoAssetId:long}/exam/review")]
    [ModulePermission(roles: new[] { "Parent" }, roleOnly: true)]
    [ProducesResponseType(typeof(Edvanz.Application.Dtos.Result<VideoExamReviewDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(object), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetChildReview(
        [FromRoute] long childId, [FromRoute] long teacherId, [FromRoute] long videoAssetId)
    {
        var resolution = await ResolveChildForParentAsync(childId, teacherId);
        if (resolution.ErrorResponse is not null) return resolution.ErrorResponse;

        var result = await _service.GetReviewAsync(teacherId, resolution.TeacherStudentId!.Value, videoAssetId);
        return ToResponse(result);
    }
}