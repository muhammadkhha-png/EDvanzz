using Edvanz.API.Attributes;
using Edvanz.Application.Dtos.VideoContentManagement;
using Edvanz.Application.IservicesContract;
using Edvanz.Application.ServiceContract;
using Edvanz.Domain.Constants;
using Edvanz.Domain.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Edvanz.API.Controllers;

/// <summary>
/// Video Content Management Module (Module 14) — unit hierarchy endpoints
/// (Track C / G-UNIT). Teacher/assistant only, same
/// <c>[ModulePermission(VideoConstants.ModuleName, ...)]</c> convention as
/// <see cref="VideosController"/>. A unit is a purely organizational grouping
/// of videos — it carries no scope/recipients of its own.
/// </summary>
[Route("api/video-units")]
[Authorize]
public sealed class VideoUnitsController : ModuleSixApiBaseController
{
    private readonly IVideoUnitService _service;

    public VideoUnitsController(
        IVideoUnitService service,
        ICurrentUserService currentUser,
        IUnitOfWork unitOfWork)
        : base(currentUser, unitOfWork)
    {
        _service = service;
    }

    // POST /api/video-units — create a new unit.
    [HttpPost]
    [ModulePermission(VideoConstants.ModuleName, VideoConstants.PermissionManageVideos)]
    [ProducesResponseType(typeof(Edvanz.Application.Dtos.Result<Edvanz.Application.Dtos.VideoContentManagement.CreateVideoUnitResponse>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(object), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(object), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> CreateUnit([FromBody] VideoUnitRequest request)
    {
        long? teacherId = await ResolveTeacherIdAsync();
        if (teacherId is null) return TeacherNotResolved();

        var result = await _service.CreateUnitAsync(teacherId.Value, GetActingUserId(), request);
        return ToResponse(result);
    }

    // PUT /api/video-units/{unitId} — rename/update a unit.
    [HttpPut("{unitId:long}")]
    [ModulePermission(VideoConstants.ModuleName, VideoConstants.PermissionManageVideos)]
    [ProducesResponseType(typeof(Edvanz.Application.Dtos.Result<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(object), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateUnit(
        [FromRoute] long unitId, [FromBody] VideoUnitRequest request)
    {
        long? teacherId = await ResolveTeacherIdAsync();
        if (teacherId is null) return TeacherNotResolved();

        var result = await _service.UpdateUnitAsync(teacherId.Value, unitId, request);
        return ToResponse(result);
    }

    // DELETE /api/video-units/{unitId} — soft-delete; videos become loose, never orphaned/cascaded.
    [HttpDelete("{unitId:long}")]
    [ModulePermission(VideoConstants.ModuleName, VideoConstants.PermissionManageVideos)]
    [ProducesResponseType(typeof(Edvanz.Application.Dtos.Result<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(object), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteUnit([FromRoute] long unitId)
    {
        long? teacherId = await ResolveTeacherIdAsync();
        if (teacherId is null) return TeacherNotResolved();

        var result = await _service.DeleteUnitAsync(teacherId.Value, unitId);
        return ToResponse(result);
    }

    // GET /api/video-units — paged unit list with rolled-up aggregates (backs S2).
    [HttpGet]
    [ModulePermission(VideoConstants.ModuleName, VideoConstants.PermissionView)]
    [ProducesResponseType(typeof(Edvanz.Application.Dtos.Result<Edvanz.Application.Dtos.PaginatedResponse<System.Collections.Generic.List<Edvanz.Application.Dtos.VideoContentManagement.TeacherVideoUnitListItemDto>>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(object), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetTeacherUnits([FromQuery] TeacherVideoUnitListRequest request)
    {
        long? teacherId = await ResolveTeacherIdAsync();
        if (teacherId is null) return TeacherNotResolved();

        var result = await _service.GetTeacherUnitsAsync(teacherId.Value, request);
        return ToResponse(result);
    }

    // GET /api/video-units/{unitId}/videos — videos-in-unit drill-down (backs S6).
    [HttpGet("{unitId:long}/videos")]
    [ModulePermission(VideoConstants.ModuleName, VideoConstants.PermissionView)]
    [ProducesResponseType(typeof(Edvanz.Application.Dtos.Result<Edvanz.Application.Dtos.PaginatedResponse<System.Collections.Generic.List<Edvanz.Application.Dtos.VideoContentManagement.TeacherVideoListItemDto>>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(object), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetVideosInUnit(
        [FromRoute] long unitId, [FromQuery] TeacherVideoListRequest request)
    {
        long? teacherId = await ResolveTeacherIdAsync();
        if (teacherId is null) return TeacherNotResolved();

        var result = await _service.GetVideosInUnitAsync(teacherId.Value, unitId, request);
        return ToResponse(result);
    }
    // GET /api/video-units/{unitId} — get unit details with its scopes.
    [HttpGet("{unitId:long}")]
    [ModulePermission(VideoConstants.ModuleName, VideoConstants.PermissionView)]
    [ProducesResponseType(typeof(Edvanz.Application.Dtos.Result<Edvanz.Application.Dtos.VideoContentManagement.VideoUnitResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(object), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetUnitById([FromRoute] long unitId)
    {
        long? teacherId = await ResolveTeacherIdAsync();
        if (teacherId is null) return TeacherNotResolved();

        var result = await _service.GetUnitWithScopesAsync(unitId, teacherId.Value);
        return ToResponse(result);
    }

    // POST /api/video-units/{unitId}/scopes — append session/group targets to a unit's scope.
    [HttpPost("{unitId:long}/scopes")]
    [ModulePermission(VideoConstants.ModuleName, VideoConstants.PermissionManageVideos)]
    [ProducesResponseType(typeof(Edvanz.Application.Dtos.Result<Edvanz.Application.Dtos.VideoContentManagement.AppendUnitScopesResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(object), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(object), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AppendUnitScopes(
        [FromRoute] long unitId, [FromBody] AssignUnitScopesRequest request)
    {
        long? teacherId = await ResolveTeacherIdAsync();
        if (teacherId is null) return TeacherNotResolved();

        var result = await _service.AppendUnitScopesAsync(teacherId.Value, GetActingUserId(), unitId, request);
        return ToResponse(result);
    }

    // PUT /api/video-units/{unitId}/scopes — replace a unit's scope. 409 (naming the videos)
    // if the new, smaller scope would leave a member video targeting sessions it no longer covers.
    [HttpPut("{unitId:long}/scopes")]
    [ModulePermission(VideoConstants.ModuleName, VideoConstants.PermissionManageVideos)]
    [ProducesResponseType(typeof(Edvanz.Application.Dtos.Result<Edvanz.Application.Dtos.VideoContentManagement.ReplaceUnitScopesResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(object), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(object), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(object), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ReplaceUnitScopes(
        [FromRoute] long unitId, [FromBody] AssignUnitScopesRequest request)
    {
        long? teacherId = await ResolveTeacherIdAsync();
        if (teacherId is null) return TeacherNotResolved();

        var result = await _service.ReplaceUnitScopesAsync(teacherId.Value, GetActingUserId(), unitId, request);
        return ToResponse(result);
    }

    // DELETE /api/video-units/{unitId}/scopes/{scopeId} — remove one scope row. 409 (naming the
    // videos) if removing it would leave a member video uncovered.
    [HttpDelete("{unitId:long}/scopes/{scopeId:long}")]
    [ModulePermission(VideoConstants.ModuleName, VideoConstants.PermissionManageVideos)]
    [ProducesResponseType(typeof(Edvanz.Application.Dtos.Result<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(object), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(object), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> RemoveUnitScope([FromRoute] long unitId, [FromRoute] long scopeId)
    {
        long? teacherId = await ResolveTeacherIdAsync();
        if (teacherId is null) return TeacherNotResolved();

        var result = await _service.RemoveUnitScopeAsync(teacherId.Value, unitId, scopeId);
        return ToResponse(result);
    }

    // GET /api/video-units/allowed-scope-targets?videoId= OR ?unitIds=1&unitIds=2 —
    // the sessions/groups a video may be scoped to (its units' coverage). Powers the
    // video-scope picker: pass videoId on the edit screen, or the chosen unitIds on create.
    [HttpGet("allowed-scope-targets")]
    [ModulePermission(VideoConstants.ModuleName, VideoConstants.PermissionView)]
    [ProducesResponseType(typeof(Edvanz.Application.Dtos.Result<Edvanz.Application.Dtos.VideoContentManagement.AllowedScopeTargetsDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(object), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetAllowedScopeTargets(
        [FromQuery] long? videoId, [FromQuery] List<long>? unitIds)
    {
        long? teacherId = await ResolveTeacherIdAsync();
        if (teacherId is null) return TeacherNotResolved();

        var result = await _service.GetAllowedScopeTargetsAsync(teacherId.Value, unitIds, videoId);
        return ToResponse(result);
    }
}
