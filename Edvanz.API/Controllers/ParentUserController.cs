using Edvanz.Application.Dtos.ParentUser;
using Edvanz.Application.ServiceContract;
using Microsoft.AspNetCore.Mvc;

namespace Edvanz.API.Controllers;

/// <summary>
/// API endpoints for the Parent User module.
/// 
/// Handles parent-specific operations: profile management, child management,
/// and teacher linking for children without Student User accounts.
/// 
/// Registration, login, password, and account-level operations live in the User module.
/// </summary>
public class ParentUserController : ApiBaseController
{
    private readonly IParentUserService _parentUserService;

    public ParentUserController(IParentUserService parentUserService)
    {
        _parentUserService = parentUserService;
    }

    // ══════════════════════════════════════════════════════════════════════════
    // ENDPOINT 1: INITIALIZE PARENT USER
    // Called AFTER User module creates a User with UserType = Parent.
    // Creates the ParentUser record.
    // POST /api/parentuser
    // ══════════════════════════════════════════════════════════════════════════
    [HttpPost]
    [ProducesResponseType(typeof(object), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(object), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> InitializeParentUser([FromBody] CreateParentUserDto dto)
    {
        var result = await _parentUserService.InitializeParentUserAsync(dto);
        return ToResponse(result);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // ENDPOINT 2: GET PARENT USER PROFILE
    // GET /api/parentuser/{parentUserId}
    // ══════════════════════════════════════════════════════════════════════════
    [HttpGet("{parentUserId:long}")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetParentUserProfile([FromRoute] long parentUserId)
    {
        var result = await _parentUserService.GetParentUserProfileAsync(parentUserId);
        return ToResponse(result);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // ENDPOINT 3: UPDATE PARENT USER PROFILE
    // Updates language preference. name/phone/password go through User module.
    // PUT /api/parentuser/{parentUserId}/profile
    // ══════════════════════════════════════════════════════════════════════════
    [HttpPut("{parentUserId:long}/profile")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateParentUserProfile(
        [FromRoute] long parentUserId,
        [FromBody] UpdateParentUserProfileDto dto)
    {
        var result = await _parentUserService.UpdateParentUserProfileAsync(parentUserId, dto);
        return ToResponse(result);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // ENDPOINT 4: GET DASHBOARD
    // Returns all children with their linked teachers and visibility settings.
    // GET /api/parentuser/{parentUserId}/dashboard
    // ══════════════════════════════════════════════════════════════════════════
    [HttpGet("{parentUserId:long}/dashboard")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetDashboard([FromRoute] long parentUserId)
    {
        var result = await _parentUserService.GetDashboardAsync(parentUserId);
        return ToResponse(result);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // ENDPOINT 5: ADD CHILD — METHOD A (child has a Student User account)
    // Parent scans or enters the StudentAccountCode.
    // Inherits all teachers already linked to that student.
    // POST /api/parentuser/{parentUserId}/children/by-code
    // ══════════════════════════════════════════════════════════════════════════
    [HttpPost("{parentUserId:long}/children/by-code")]
    [ProducesResponseType(typeof(object), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(object), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> AddChildByAccountCode(
        [FromRoute] long parentUserId,
        [FromBody] AddChildByAccountCodeDto dto)
    {
        var result = await _parentUserService.AddChildByAccountCodeAsync(parentUserId, dto);
        return ToResponse(result);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // ENDPOINT 6: ADD CHILD — METHOD B (child has no account)
    // Parent enters the child's name manually. Teachers added separately.
    // POST /api/parentuser/{parentUserId}/children/manual
    // ══════════════════════════════════════════════════════════════════════════
    [HttpPost("{parentUserId:long}/children/manual")]
    [ProducesResponseType(typeof(object), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AddChildManual(
        [FromRoute] long parentUserId,
        [FromBody] AddChildManualDto dto)
    {
        var result = await _parentUserService.AddChildManualAsync(parentUserId, dto);
        return ToResponse(result);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // ENDPOINT 7: LINK TEACHER TO CHILD (Method B only)
    // Same 3 credentials as AAM-FR-05.5.
    // Not allowed for Method A children (their teachers come from StudentTeacherLink).
    // POST /api/parentuser/{parentUserId}/children/{childId}/teachers
    // ══════════════════════════════════════════════════════════════════════════
    [HttpPost("{parentUserId:long}/children/{childId:long}/teachers")]
    [ProducesResponseType(typeof(object), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(object), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> LinkTeacherToChild(
        [FromRoute] long parentUserId,
        [FromRoute] long childId,
        [FromBody] LinkTeacherToChildDto dto)
    {
        var result = await _parentUserService.LinkTeacherToChildAsync(parentUserId, childId, dto);
        return ToResponse(result);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // ENDPOINT 8: UNLINK TEACHER FROM CHILD (Method B only)
    // DELETE /api/parentuser/{parentUserId}/children/{childId}/teachers/{teacherId}
    // ══════════════════════════════════════════════════════════════════════════
    [HttpDelete("{parentUserId:long}/children/{childId:long}/teachers/{teacherId:long}")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UnlinkTeacherFromChild(
        [FromRoute] long parentUserId,
        [FromRoute] long childId,
        [FromRoute] long teacherId)
    {
        var result = await _parentUserService.UnlinkTeacherFromChildAsync(parentUserId, childId, teacherId);
        return ToResponse(result);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // ENDPOINT 9: GET SINGLE CHILD
    // Returns a single child with their linked teachers.
    // GET /api/parentuser/{parentUserId}/children/{childId}
    // ══════════════════════════════════════════════════════════════════════════
    [HttpGet("{parentUserId:long}/children/{childId:long}")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetChild(
        [FromRoute] long parentUserId,
        [FromRoute] long childId)
    {
        var result = await _parentUserService.GetChildAsync(parentUserId, childId);
        return ToResponse(result);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // ENDPOINT 10: REMOVE CHILD
    // Soft-deactivates the child link. Preserves the record for audit.
    // DELETE /api/parentuser/{parentUserId}/children/{childId}
    // ══════════════════════════════════════════════════════════════════════════
    [HttpDelete("{parentUserId:long}/children/{childId:long}")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RemoveChild(
        [FromRoute] long parentUserId,
        [FromRoute] long childId)
    {
        var result = await _parentUserService.RemoveChildAsync(parentUserId, childId);
        return ToResponse(result);
    }
}