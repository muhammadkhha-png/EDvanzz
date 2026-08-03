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
/// Video Content Management Module (Module 14) — parent-facing READ endpoints.
///
/// SEPARATE CONTROLLER RATIONALE: mirrors <see cref="StudentVideosController"/> — parents carry
/// no module claim, and the service needs (teacherId, teacherStudentId), which this controller
/// resolves via <see cref="ParentScopedApiBaseController.ResolveChildForParentAsync"/> (parent
/// ownership of the child, then Method A/B teacher-link branch, AAM-FR-06.3).
///
/// READ-ONLY (D6 — locked decision, parent-parity phase plan): list / units / unit drill-down
/// only. No start/stop-watch — a parent never triggers a VideoAnalytics write. The child's own
/// watch state (HasOpened, LastOpenedAt, WatchStatus) is still visible on each row — a parent
/// can see THAT their child watched something, they just can't watch it themselves through this
/// surface or influence the child's own watch analytics.
///
/// AUTH: [ModulePermission(roles: ["Parent"], roleOnly: true)] — parent role only.
///
/// SECURITY: route childId and teacherId are untrusted — see
/// <see cref="ParentScopedApiBaseController.ResolveChildForParentAsync"/> for the full resolution
/// chain. Teacher-controlled parent visibility (ParentVisibilityVideo) governs the home-aggregate
/// tile only (Phase 8) — like the student endpoints it mirrors, this list itself is gated purely
/// by module-active + video scope, not by the visibility flag (established precedent, see
/// VideoService — GetStudentVideosAsync has never checked StudentVisibilityVideo either).
/// </summary>
[Route("api/videos/parent")]
[Authorize]
public sealed class ParentVideosController : ParentScopedApiBaseController
{
    private readonly IVideoService _service;

    public ParentVideosController(
        IVideoService service,
        ICurrentUserService currentUser,
        IUnitOfWork unitOfWork,
        IStringLocalizer<Messages> localizer)
        : base(currentUser, unitOfWork, localizer)
    {
        _service = service;
    }

    // ──────────────────────────────────────────────────────────────────────
    // CHILD VIDEO LIST
    // GET /api/videos/parent/children/{childId}/teachers/{teacherId}
    // ──────────────────────────────────────────────────────────────────────
    [HttpGet("children/{childId:long}/teachers/{teacherId:long}")]
    [ModulePermission(roles: new[] { "Parent" }, roleOnly: true)]
    [ProducesResponseType(typeof(Edvanz.Application.Dtos.Result<Edvanz.Application.Dtos.PaginatedResponse<System.Collections.Generic.List<StudentVideoListItemDto>>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(object), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetChildVideos(
        [FromRoute] long childId, [FromRoute] long teacherId, [FromQuery] StudentVideoListRequest request)
    {
        var resolution = await ResolveChildForParentAsync(childId, teacherId);
        if (resolution.ErrorResponse is not null) return resolution.ErrorResponse;

        var result = await _service.GetParentVideosAsync(
            teacherId, resolution.TeacherStudentId!.Value, request, resolution.ParentLanguagePreference);
        return ToResponse(result);
    }

    // ──────────────────────────────────────────────────────────────────────
    // CHILD VIDEO UNITS
    // GET /api/videos/parent/children/{childId}/teachers/{teacherId}/units
    // ──────────────────────────────────────────────────────────────────────
    [HttpGet("children/{childId:long}/teachers/{teacherId:long}/units")]
    [ModulePermission(roles: new[] { "Parent" }, roleOnly: true)]
    [ProducesResponseType(typeof(Edvanz.Application.Dtos.Result<System.Collections.Generic.List<StudentVideoUnitDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(object), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetChildUnits([FromRoute] long childId, [FromRoute] long teacherId)
    {
        var resolution = await ResolveChildForParentAsync(childId, teacherId);
        if (resolution.ErrorResponse is not null) return resolution.ErrorResponse;

        var result = await _service.GetParentUnitsAsync(
            teacherId, resolution.TeacherStudentId!.Value, resolution.ParentLanguagePreference);
        return ToResponse(result);
    }

    // ──────────────────────────────────────────────────────────────────────
    // CHILD VIDEOS IN A UNIT
    // GET /api/videos/parent/children/{childId}/teachers/{teacherId}/units/{unitId}/videos
    // ──────────────────────────────────────────────────────────────────────
    [HttpGet("children/{childId:long}/teachers/{teacherId:long}/units/{unitId:long}/videos")]
    [ModulePermission(roles: new[] { "Parent" }, roleOnly: true)]
    [ProducesResponseType(typeof(Edvanz.Application.Dtos.Result<Edvanz.Application.Dtos.PaginatedResponse<System.Collections.Generic.List<StudentVideoListItemDto>>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(object), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetChildVideosInUnit(
        [FromRoute] long childId, [FromRoute] long teacherId, [FromRoute] long unitId,
        [FromQuery] StudentVideoListRequest request)
    {
        var resolution = await ResolveChildForParentAsync(childId, teacherId);
        if (resolution.ErrorResponse is not null) return resolution.ErrorResponse;

        var result = await _service.GetParentVideosInUnitAsync(
            teacherId, resolution.TeacherStudentId!.Value, unitId, request, resolution.ParentLanguagePreference);
        return ToResponse(result);
    }
}