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
/// Video Content Management Module (Module 14) — teacher and assistant
/// endpoints. Inherits <see cref="ModuleSixApiBaseController"/> for the
/// JWT-to-teacher resolution helper (<c>ResolveTeacherIdAsync</c>,
/// <c>GetActingUserId</c>, <c>TeacherNotResolved</c>).
///
/// Every endpoint enforces:
/// <list type="bullet">
///   <item><c>[Authorize]</c> — JWT required (class-level).</item>
///   <item><c>[ModulePermission(VideoConstants.ModuleName, ...)]</c> — module
///         must be active in the JWT, and (for assistants) the named
///         permission must be present in the assistant's claims. Teachers
///         pass with the module claim alone; SuperAdmin bypasses.</item>
/// </list>
///
/// TENANT SCOPE: <c>teacherId</c> always derived from JWT via
/// <see cref="ModuleSixApiBaseController.ResolveTeacherIdAsync"/>; never read
/// from the request body or route. Catalog §1.3.
///
/// STUDENT-FACING ENDPOINTS LIVE ELSEWHERE: see
/// <see cref="StudentVideosController"/>.
/// </summary>
[Route("api/videos")]
[Authorize]
public sealed class VideosController : ModuleSixApiBaseController
{
    private readonly IVideoService _service;

    public VideosController(
        IVideoService service,
        ICurrentUserService currentUser,
        IUnitOfWork unitOfWork)
        : base(currentUser, unitOfWork)
    {
        _service = service;
    }

    // ══════════════════════════════════════════════════════════════════════
    // ENDPOINT 1 — CREATE VIDEO  (Story A, REQ-VCM-FR-01)
    // POST /api/videos
    // ══════════════════════════════════════════════════════════════════════
    //
    // WHAT IT DOES:
    //   Creates a new video reference for the calling teacher. Parses the
    //   teacher-supplied URL into (provider, externalId). Returns the new
    //   video id; Flutter then chains a POST /scopes call.
    //
    // TABLES WRITTEN:
    //   VideoAssets (1 row)
    //
    // SAMPLE REQUEST:
    //   POST /api/videos
    //   {
    //     "title": "Newton's Laws — Lecture 1",
    //     "description": "Watch before next class.",
    //     "sourceUrl": "https://www.youtube.com/watch?v=dQw4w9WgXcQ"
    //   }
    //
    // ══════════════════════════════════════════════════════════════════════
    [HttpPost]
    [ModulePermission(VideoConstants.ModuleName, VideoConstants.PermissionManageVideos)]
    [ProducesResponseType(typeof(object), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(object), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(object), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CreateVideo([FromBody] CreateVideoRequest request)
    {
        long? teacherId = await ResolveTeacherIdAsync();
        if (teacherId is null) return TeacherNotResolved();

        var result = await _service.CreateVideoAsync(
            teacherId.Value, GetActingUserId(), request);
        return ToResponse(result);
    }

    // ══════════════════════════════════════════════════════════════════════
    // ENDPOINT 2 — APPEND SCOPES  (Story A step 7, idempotent on duplicates)
    // POST /api/videos/{videoAssetId}/scopes
    // ══════════════════════════════════════════════════════════════════════
    //
    // WHAT IT DOES:
    //   Adds one or more scope rows to an existing video. Duplicate rows are
    //   silently skipped (counted in scopesSkipped). Service validates each
    //   scope target belongs to the calling teacher.
    //
    // TABLES WRITTEN: VideoScopes
    //
    // SAMPLE REQUEST:
    //   POST /api/videos/42/scopes
    //   {
    //     "scopes": [
    //       { "scopeType": "IndividualStudent", "teacherStudentId": 101 },
    //       { "scopeType": "Session", "sessionId": 33 }
    //     ]
    //   }
    //
    // ══════════════════════════════════════════════════════════════════════
    [HttpPost("{videoAssetId:long}/scopes")]
    [ModulePermission(VideoConstants.ModuleName, VideoConstants.PermissionManageVideos)]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(object), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(object), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AppendScopes(
        [FromRoute] long videoAssetId, [FromBody] AssignScopesRequest request)
    {
        long? teacherId = await ResolveTeacherIdAsync();
        if (teacherId is null) return TeacherNotResolved();

        var result = await _service.AppendScopesAsync(
            teacherId.Value, GetActingUserId(), videoAssetId, request);
        return ToResponse(result);
    }

    // ══════════════════════════════════════════════════════════════════════
    // ENDPOINT 3 — REPLACE ALL SCOPES  (Story D, transactional)
    // PUT /api/videos/{videoAssetId}/scopes
    // ══════════════════════════════════════════════════════════════════════
    //
    // WHAT IT DOES:
    //   Atomically replaces all scope rows for a video. Old scopes are
    //   deleted; new scopes are inserted; VideoAnalytics is deliberately
    //   untouched so previously-targeted students' watch history survives.
    //
    // TABLES WRITTEN: VideoScopes (DELETE all, then INSERT new)
    //
    // ══════════════════════════════════════════════════════════════════════
    [HttpPut("{videoAssetId:long}/scopes")]
    [ModulePermission(VideoConstants.ModuleName, VideoConstants.PermissionManageVideos)]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(object), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(object), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ReplaceScopes(
        [FromRoute] long videoAssetId, [FromBody] AssignScopesRequest request)
    {
        long? teacherId = await ResolveTeacherIdAsync();
        if (teacherId is null) return TeacherNotResolved();

        var result = await _service.ReplaceScopesAsync(
            teacherId.Value, GetActingUserId(), videoAssetId, request);
        return ToResponse(result);
    }

    // ══════════════════════════════════════════════════════════════════════
    // ENDPOINT 4 — REMOVE SINGLE SCOPE  (Story D, endpoint #4)
    // DELETE /api/videos/{videoAssetId}/scopes/{scopeId}
    // ══════════════════════════════════════════════════════════════════════
    //
    // WHAT IT DOES:
    //   Removes one scope row from a video. Refuses if it would leave the
    //   video with zero scopes — teacher should hard-delete the video instead.
    //
    // ══════════════════════════════════════════════════════════════════════
    [HttpDelete("{videoAssetId:long}/scopes/{scopeId:long}")]
    [ModulePermission(VideoConstants.ModuleName, VideoConstants.PermissionManageVideos)]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(object), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(object), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RemoveScope(
        [FromRoute] long videoAssetId, [FromRoute] long scopeId)
    {
        long? teacherId = await ResolveTeacherIdAsync();
        if (teacherId is null) return TeacherNotResolved();

        var result = await _service.RemoveScopeAsync(teacherId.Value, videoAssetId, scopeId);
        return ToResponse(result);
    }

    // ══════════════════════════════════════════════════════════════════════
    // ENDPOINT 5 — DELETE VIDEO  (Story E, REQ-VCM-BR-03)
    // DELETE /api/videos/{videoAssetId}
    // ══════════════════════════════════════════════════════════════════════
    //
    // WHAT IT DOES:
    //   Hard-deletes the video and writes a JSON audit snapshot in the same
    //   transaction. NoAction FKs remove scopes, analytics, and watch events.
    //
    // TABLES WRITTEN:
    //   VideoAssetAudits (INSERT 1)
    //   VideoAssets (DELETE — NoAction removes children)
    //
    // ══════════════════════════════════════════════════════════════════════
    [HttpDelete("{videoAssetId:long}")]
    [ModulePermission(VideoConstants.ModuleName, VideoConstants.PermissionManageVideos)]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(object), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteVideo([FromRoute] long videoAssetId)
    {
        long? teacherId = await ResolveTeacherIdAsync();
        if (teacherId is null) return TeacherNotResolved();

        var result = await _service.DeleteVideoAsync(
            teacherId.Value, GetActingUserId(), videoAssetId);
        return ToResponse(result);
    }

    // ══════════════════════════════════════════════════════════════════════
    // ENDPOINT 6 — TEACHER VIDEO LIST  (Story B teacher endpoint)
    // GET /api/videos/teacher
    // ══════════════════════════════════════════════════════════════════════
    //
    // WHAT IT DOES:
    //   Paged list of the calling teacher's own videos with per-video
    //   aggregates (StudentsInScope, TotalOpens). Newest first.
    //
    // ══════════════════════════════════════════════════════════════════════
    [HttpGet("teacher")]
    [ModulePermission(VideoConstants.ModuleName, VideoConstants.PermissionView)]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(object), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetTeacherVideos([FromQuery] TeacherVideoListRequest request)
    {
        long? teacherId = await ResolveTeacherIdAsync();
        if (teacherId is null) return TeacherNotResolved();

        var result = await _service.GetTeacherVideosAsync(teacherId.Value, request);
        return ToResponse(result);
    }

    // ══════════════════════════════════════════════════════════════════════
    // ENDPOINT 10 — ANALYTICS REPORT  (Story F, REQ-VCM-FR-04)
    // GET /api/videos/{videoAssetId}/analytics
    // ══════════════════════════════════════════════════════════════════════
    //
    // WHAT IT DOES:
    //   Per-video analytics report. One row per student in the resolved
    //   scope, including students who never opened the video. Aggregates
    //   appear at the top level.
    //
    // ══════════════════════════════════════════════════════════════════════
    [HttpGet("{videoAssetId:long}/analytics")]
    [ModulePermission(VideoConstants.ModuleName, VideoConstants.PermissionView)]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(object), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetAnalytics(
        [FromRoute] long videoAssetId, [FromQuery] VideoAnalyticsRequest request)
    {
        long? teacherId = await ResolveTeacherIdAsync();
        if (teacherId is null) return TeacherNotResolved();

        var result = await _service.GetAnalyticsAsync(teacherId.Value, videoAssetId, request);
        return ToResponse(result);
    }
}
