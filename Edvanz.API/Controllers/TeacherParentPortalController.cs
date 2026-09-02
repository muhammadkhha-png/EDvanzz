using Edvanz.API.Attributes;
using Edvanz.Application.Dtos;
using Edvanz.Application.Dtos.ParentPortal;
using Edvanz.Application.IservicesContract;
using Edvanz.Application.ServiceContract;
using Edvanz.Domain.Constants;
using Edvanz.Domain.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Edvanz.API.Controllers;

/// <summary>
/// TEACHER side of the public parent portal: the pending-request inbox, approve / reject (single
/// and bulk), the per-student follower list, revocation, and the settings-screen counters.
///
/// AUTH &amp; TENANCY: class-level <c>[Authorize]</c> plus a per-action
/// <c>[ModulePermission(Student, Edit)]</c>, so an assistant already trusted to edit students can
/// clear the inbox too. The acting <c>teacherId</c> comes ONLY from the JWT via
/// <c>ResolveTeacherIdAsync</c> (CLAUDE.md §3.3) — never from a route or body — and every service
/// call is tenant-scoped, so a grant id from another teacher resolves to nothing.
///
/// PRIVACY: parent phone numbers are returned MASKED (010•••••678) on every route here.
///
/// The parent-facing half lives in <see cref="ParentPortalController"/> under api/parent-portal.
/// </summary>
[Authorize]
[Route("api/teacher/parent-portal")]
public class TeacherParentPortalController : ModuleSixApiBaseController
{
    private readonly ITeacherParentPortalService _portalService;

    public TeacherParentPortalController(
        ITeacherParentPortalService portalService,
        ICurrentUserService currentUser,
        IUnitOfWork unitOfWork)
        : base(currentUser, unitOfWork)
    {
        _portalService = portalService;
    }

    /// <summary>Pages the PENDING parent requests for this teacher, newest first.</summary>
    /// <response code="200">Paginated pending requests.</response>
    [HttpGet("requests")]
    [ModulePermission(StudentConstants.ModuleName, StudentConstants.PermissionEdit)]
    [ProducesResponseType(typeof(Result<PaginatedResponse<List<ParentPortalRequestListItemDto>>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(object), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetPendingRequests(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        long? teacherId = await ResolveTeacherIdAsync();
        if (teacherId is null) return TeacherNotResolved();

        return ToResponse(await _portalService.GetPendingRequestsAsync(teacherId.Value, page, pageSize));
    }

    /// <summary>Approves one pending request — the parent's device gains read access on its next call.</summary>
    /// <response code="200">Approved; returns the follower row.</response>
    /// <response code="404">Request not found, already resolved, or its student has been removed.</response>
    [HttpPost("requests/{id:long}/approve")]
    [ModulePermission(StudentConstants.ModuleName, StudentConstants.PermissionEdit)]
    [ProducesResponseType(typeof(Result<ParentPortalFollowerListItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(object), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ApproveRequest([FromRoute] long id)
    {
        long? teacherId = await ResolveTeacherIdAsync();
        if (teacherId is null) return TeacherNotResolved();

        return ToResponse(await _portalService.ApproveRequestAsync(teacherId.Value, id, GetActingUserId()));
    }

    /// <summary>Rejects one pending request. The row is kept for audit and the parent may try again later.</summary>
    /// <response code="200">Rejected; returns the resolved row.</response>
    /// <response code="404">Request not found or already resolved.</response>
    [HttpPost("requests/{id:long}/reject")]
    [ModulePermission(StudentConstants.ModuleName, StudentConstants.PermissionEdit)]
    [ProducesResponseType(typeof(Result<ParentPortalFollowerListItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(object), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RejectRequest([FromRoute] long id)
    {
        long? teacherId = await ResolveTeacherIdAsync();
        if (teacherId is null) return TeacherNotResolved();

        return ToResponse(await _portalService.RejectRequestAsync(teacherId.Value, id, GetActingUserId()));
    }

    /// <summary>
    /// Approves or rejects many requests at once. Ids that are not this teacher's, or are already
    /// resolved, come back in <c>skippedIds</c> rather than failing the whole call.
    /// </summary>
    /// <response code="200">Counts of affected and skipped ids.</response>
    /// <response code="400">Unknown action, or an empty id list.</response>
    [HttpPost("requests/bulk")]
    [ModulePermission(StudentConstants.ModuleName, StudentConstants.PermissionEdit)]
    [ProducesResponseType(typeof(Result<ParentPortalBulkResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(object), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(object), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> BulkResolve([FromBody] ParentPortalBulkActionDto dto)
    {
        long? teacherId = await ResolveTeacherIdAsync();
        if (teacherId is null) return TeacherNotResolved();

        return ToResponse(await _portalService.BulkResolveAsync(teacherId.Value, dto, GetActingUserId()));
    }

    /// <summary>Everyone (Active or Pending) currently following one of this teacher's students.</summary>
    /// <response code="200">Follower rows, newest first.</response>
    /// <response code="404">The student is not on this teacher's list.</response>
    [HttpGet("students/{teacherStudentId:long}/followers")]
    [ModulePermission(StudentConstants.ModuleName, StudentConstants.PermissionEdit)]
    [ProducesResponseType(typeof(Result<List<ParentPortalFollowerListItemDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(object), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetFollowers([FromRoute] long teacherStudentId)
    {
        long? teacherId = await ResolveTeacherIdAsync();
        if (teacherId is null) return TeacherNotResolved();

        return ToResponse(await _portalService.GetFollowersAsync(teacherId.Value, teacherStudentId));
    }

    /// <summary>Ends a follower's access. Their device sees "the teacher removed your access" on its next call.</summary>
    /// <response code="200">Access revoked.</response>
    /// <response code="404">No live grant with this id under this teacher.</response>
    [HttpPost("followers/{id:long}/revoke")]
    [ModulePermission(StudentConstants.ModuleName, StudentConstants.PermissionEdit)]
    [ProducesResponseType(typeof(Result<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(object), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RevokeFollower([FromRoute] long id)
    {
        long? teacherId = await ResolveTeacherIdAsync();
        if (teacherId is null) return TeacherNotResolved();

        return ToResponse(await _portalService.RevokeFollowerAsync(teacherId.Value, id, GetActingUserId()));
    }

    /// <summary>Counters for the parent-portal settings screen: pending requests, followed students, students with no parent phone, and the current opt-in state.</summary>
    /// <response code="200">Summary counters.</response>
    [HttpGet("summary")]
    [ModulePermission(StudentConstants.ModuleName, StudentConstants.PermissionEdit)]
    [ProducesResponseType(typeof(Result<ParentPortalSummaryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(object), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetSummary()
    {
        long? teacherId = await ResolveTeacherIdAsync();
        if (teacherId is null) return TeacherNotResolved();

        return ToResponse(await _portalService.GetSummaryAsync(teacherId.Value));
    }
}
