using Edvanz.API.Attributes;
using Edvanz.Application.Dtos.ParentUser;
using Edvanz.Application.IservicesContract;
using Edvanz.Application.ServiceContract;
using Edvanz.Domain.Interfaces;
using Edvanz.Domain.Resources;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;

namespace Edvanz.API.Controllers;

/// <summary>
/// API endpoints for the Parent User module.
///
/// Handles parent-specific operations: profile management, child management,
/// and teacher linking for children without Student User accounts.
///
/// Registration, login, password, and account-level operations live in the User module.
///
/// SECURITY â€” tenant isolation (mirrors <see cref="ParentAttendanceController"/> /
/// <see cref="ParentPaymentController"/>): the acting parent is ALWAYS resolved from the
/// JWT (User.Id â†’ active <c>ParentUser</c>) via <see cref="ResolveParentUserIdAsync"/>.
/// The <c>{parentUserId}</c> route segment is retained for backward-compatible URLs but its
/// value is IGNORED â€” sending 0, a wrong id, a mismatched id, or the real id all behave the
/// same, so a caller can never read or mutate another parent's data (previously every action
/// trusted the route <c>parentUserId</c> with no authorization â†’ mass horizontal IDOR).
/// Each <c>childId</c> is scoped to the JWT-resolved parent inside the service
/// (<c>GetActiveChildAsync(parentUserId, childId)</c>). Role gated to Parent.
/// </summary>
[Authorize]
public class ParentUserController : ParentScopedApiBaseController
{
    private readonly IParentUserService _parentUserService;

    public ParentUserController(
        IParentUserService parentUserService,
        ICurrentUserService currentUser,
        IUnitOfWork unitOfWork,
        IStringLocalizer<Messages> localizer)
        : base(currentUser, unitOfWork, localizer)
    {
        _parentUserService = parentUserService;
    }

    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
    // ENDPOINT 1: INITIALIZE PARENT USER
    // Called AFTER User module creates a User with UserType = Parent.
    // Creates the ParentUser record.
    // POST /api/parentuser
    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
    [HttpPost]
    [ModulePermission(roles: new[] { "Parent" }, roleOnly: true)]
    [ProducesResponseType(typeof(Edvanz.Application.Dtos.Result<Edvanz.Application.Dtos.ParentUser.ParentUserProfileDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(object), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> InitializeParentUser([FromBody] CreateParentUserDto dto)
    {
        // Tenant isolation: the ParentUser is always created for the AUTHENTICATED user.
        // dto.UserId from the body is ignored (kept for wire compatibility). The registration
        // flow initializes parents server-side via UserService â€” this HTTP path is self-init only.
        long? userId = GetCurrentUserId();
        if (userId is null) return UserNotResolved();
        dto.UserId = userId.Value;

        var result = await _parentUserService.InitializeParentUserAsync(dto);
        return ToResponse(result);
    }

    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
    // ENDPOINT 2: GET PARENT USER PROFILE
    // GET /api/parentuser/{parentUserId}
    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
    [HttpGet("{parentUserId:long}")]
    [ModulePermission(roles: new[] { "Parent" }, roleOnly: true)]
    [ProducesResponseType(typeof(Edvanz.Application.Dtos.Result<Edvanz.Application.Dtos.ParentUser.ParentUserProfileDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetParentUserProfile([FromRoute] long parentUserId)
    {
        // Route parentUserId ignored â€” identity comes from the JWT.
        long? resolvedParentId = await ResolveParentUserIdAsync();
        if (resolvedParentId is null) return ParentNotResolved();

        var result = await _parentUserService.GetParentUserProfileAsync(resolvedParentId.Value);
        return ToResponse(result);
    }

    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
    // ENDPOINT 3: UPDATE PARENT USER PROFILE
    // Updates language preference. name/phone/password go through User module.
    // PUT /api/parentuser/{parentUserId}/profile
    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
    [HttpPut("{parentUserId:long}/profile")]
    [ModulePermission(roles: new[] { "Parent" }, roleOnly: true)]
    [ProducesResponseType(typeof(Edvanz.Application.Dtos.Result<Edvanz.Application.Dtos.ParentUser.ParentUserProfileDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateParentUserProfile(
        [FromRoute] long parentUserId,
        [FromBody] UpdateParentUserProfileDto dto)
    {
        long? resolvedParentId = await ResolveParentUserIdAsync();
        if (resolvedParentId is null) return ParentNotResolved();

        var result = await _parentUserService.UpdateParentUserProfileAsync(resolvedParentId.Value, dto);
        return ToResponse(result);
    }

    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
    // ENDPOINT 4: GET DASHBOARD
    // Returns all children with their linked teachers and visibility settings.
    // GET /api/parentuser/{parentUserId}/dashboard
    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
    [HttpGet("{parentUserId:long}/dashboard")]
    [ModulePermission(roles: new[] { "Parent" }, roleOnly: true)]
    [ProducesResponseType(typeof(Edvanz.Application.Dtos.Result<Edvanz.Application.Dtos.ParentUser.ParentDashboardDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetDashboard([FromRoute] long parentUserId)
    {
        long? resolvedParentId = await ResolveParentUserIdAsync();
        if (resolvedParentId is null) return ParentNotResolved();

        var result = await _parentUserService.GetDashboardAsync(resolvedParentId.Value);
        return ToResponse(result);
    }

    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
    // ENDPOINT 5: ADD CHILD â€” METHOD A (child has a Student User account)
    // Parent scans or enters the StudentAccountCode.
    // Inherits all teachers already linked to that student.
    // POST /api/parentuser/{parentUserId}/children/by-code
    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
    [HttpPost("{parentUserId:long}/children/by-code")]
    [ModulePermission(roles: new[] { "Parent" }, roleOnly: true)]
    [ProducesResponseType(typeof(Edvanz.Application.Dtos.Result<Edvanz.Application.Dtos.ParentUser.ParentChildDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(object), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> AddChildByAccountCode(
        [FromRoute] long parentUserId,
        [FromBody] AddChildByAccountCodeDto dto)
    {
        long? resolvedParentId = await ResolveParentUserIdAsync();
        if (resolvedParentId is null) return ParentNotResolved();

        var result = await _parentUserService.AddChildByAccountCodeAsync(resolvedParentId.Value, dto);
        return ToResponse(result);
    }

    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
    // ENDPOINT 6: ADD CHILD â€” METHOD B (child has no account)
    // Parent enters the child's name manually. Teachers added separately.
    // POST /api/parentuser/{parentUserId}/children/manual
    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
    [HttpPost("{parentUserId:long}/children/manual")]
    [ModulePermission(roles: new[] { "Parent" }, roleOnly: true)]
    [ProducesResponseType(typeof(Edvanz.Application.Dtos.Result<Edvanz.Application.Dtos.ParentUser.ParentChildDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AddChildManual(
        [FromRoute] long parentUserId,
        [FromBody] AddChildManualDto dto)
    {
        long? resolvedParentId = await ResolveParentUserIdAsync();
        if (resolvedParentId is null) return ParentNotResolved();

        var result = await _parentUserService.AddChildManualAsync(resolvedParentId.Value, dto);
        return ToResponse(result);
    }

    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
    // ENDPOINT 7: LINK TEACHER TO CHILD (Method B only)
    // Same 3 credentials as AAM-FR-05.5.
    // Not allowed for Method A children (their teachers come from StudentTeacherLink).
    // POST /api/parentuser/{parentUserId}/children/{childId}/teachers
    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
    [HttpPost("{parentUserId:long}/children/{childId:long}/teachers")]
    [ModulePermission(roles: new[] { "Parent" }, roleOnly: true)]
    [ProducesResponseType(typeof(Edvanz.Application.Dtos.Result<Edvanz.Application.Dtos.ParentUser.ParentChildTeacherDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(object), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> LinkTeacherToChild(
        [FromRoute] long parentUserId,
        [FromRoute] long childId,
        [FromBody] LinkTeacherToChildDto dto)
    {
        // childId is scoped to the resolved parent inside the service.
        long? resolvedParentId = await ResolveParentUserIdAsync();
        if (resolvedParentId is null) return ParentNotResolved();

        var result = await _parentUserService.LinkTeacherToChildAsync(resolvedParentId.Value, childId, dto);
        return ToResponse(result);
    }

    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
    // ENDPOINT 8: UNLINK TEACHER FROM CHILD (Method B only)
    // DELETE /api/parentuser/{parentUserId}/children/{childId}/teachers/{teacherId}
    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
    [HttpDelete("{parentUserId:long}/children/{childId:long}/teachers/{teacherId:long}")]
    [ModulePermission(roles: new[] { "Parent" }, roleOnly: true)]
    [ProducesResponseType(typeof(Edvanz.Application.Dtos.Result<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UnlinkTeacherFromChild(
        [FromRoute] long parentUserId,
        [FromRoute] long childId,
        [FromRoute] long teacherId)
    {
        // parentUserId is ignored (resolved from JWT); childId is scoped to that parent in
        // the service; teacherId here selects WHICH linked teacher to unlink from the child.
        long? resolvedParentId = await ResolveParentUserIdAsync();
        if (resolvedParentId is null) return ParentNotResolved();

        var result = await _parentUserService.UnlinkTeacherFromChildAsync(resolvedParentId.Value, childId, teacherId);
        return ToResponse(result);
    }

    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
    // ENDPOINT 9: GET SINGLE CHILD
    // Returns a single child with their linked teachers.
    // GET /api/parentuser/{parentUserId}/children/{childId}
    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
    [HttpGet("{parentUserId:long}/children/{childId:long}")]
    [ModulePermission(roles: new[] { "Parent" }, roleOnly: true)]
    [ProducesResponseType(typeof(Edvanz.Application.Dtos.Result<Edvanz.Application.Dtos.ParentUser.ParentChildDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetChild(
        [FromRoute] long parentUserId,
        [FromRoute] long childId)
    {
        long? resolvedParentId = await ResolveParentUserIdAsync();
        if (resolvedParentId is null) return ParentNotResolved();

        var result = await _parentUserService.GetChildAsync(resolvedParentId.Value, childId);
        return ToResponse(result);
    }

    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
    // ENDPOINT 10: REMOVE CHILD
    // Soft-deactivates the child link. Preserves the record for audit.
    // DELETE /api/parentuser/{parentUserId}/children/{childId}
    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
    [HttpDelete("{parentUserId:long}/children/{childId:long}")]
    [ModulePermission(roles: new[] { "Parent" }, roleOnly: true)]
    [ProducesResponseType(typeof(Edvanz.Application.Dtos.Result<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RemoveChild(
        [FromRoute] long parentUserId,
        [FromRoute] long childId)
    {
        long? resolvedParentId = await ResolveParentUserIdAsync();
        if (resolvedParentId is null) return ParentNotResolved();

        var result = await _parentUserService.RemoveChildAsync(resolvedParentId.Value, childId);
        return ToResponse(result);
    }
}
