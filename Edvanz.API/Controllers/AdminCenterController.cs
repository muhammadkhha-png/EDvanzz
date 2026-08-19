using Edvanz.API.Attributes;
using Edvanz.Application.Dtos.Center;
using Edvanz.Application.IservicesContract;
using Edvanz.Application.ServiceContract;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Edvanz.API.Controllers;

/// <summary>
/// SuperAdmin-only provisioning of Center accounts (create the login + Center row + code, list,
/// read, deactivate). Center subscriptions (quota packages) are on the admin center-subscription
/// controller.
/// </summary>
[Route("api/admin/centers")]
[Authorize]
[ModulePermission(roles: new[] { "SuperAdmin" }, roleOnly: true)]
public class AdminCenterController : ApiBaseController
{
    private readonly IAdminCenterService _adminCenterService;
    private readonly ICenterService _centerService;
    private readonly ITeacherIndependenceService _independenceService;
    private readonly ICurrentUserService _currentUser;

    public AdminCenterController(
        IAdminCenterService adminCenterService,
        ICenterService centerService,
        ITeacherIndependenceService independenceService,
        ICurrentUserService currentUser)
    {
        _adminCenterService = adminCenterService;
        _centerService = centerService;
        _independenceService = independenceService;
        _currentUser = currentUser;
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateCenterDto dto)
    {
        var adminUserId = _currentUser.UserId;
        if (adminUserId is null) return UserNotResolved();
        return ToResponse(await _adminCenterService.CreateCenterAsync(adminUserId.Value, dto));
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
        => ToResponse(await _adminCenterService.GetCentersAsync());

    [HttpGet("{centerId:long}")]
    public async Task<IActionResult> GetById([FromRoute] long centerId)
        => ToResponse(await _adminCenterService.GetCenterByIdAsync(centerId));

    /// <summary>The center's teachers (for the admin center-detail page).</summary>
    [HttpGet("{centerId:long}/teachers")]
    public async Task<IActionResult> GetTeachers([FromRoute] long centerId)
        => ToResponse(await _centerService.GetTeachersAsync(centerId));

    [HttpPost("{centerId:long}/deactivate")]
    public async Task<IActionResult> Deactivate([FromRoute] long centerId)
    {
        var adminUserId = _currentUser.UserId;
        if (adminUserId is null) return UserNotResolved();
        return ToResponse(await _adminCenterService.DeactivateCenterAsync(adminUserId.Value, centerId));
    }

    [HttpPost("{centerId:long}/activate")]
    public async Task<IActionResult> Reactivate([FromRoute] long centerId)
    {
        var adminUserId = _currentUser.UserId;
        if (adminUserId is null) return UserNotResolved();
        return ToResponse(await _adminCenterService.ReactivateCenterAsync(adminUserId.Value, centerId));
    }

    // ── Teacher independence request queue (a center-teacher asking to leave their center) ──

    /// <summary>The queue of Pending teacher-independence requests, oldest first.</summary>
    [HttpGet("independence-requests")]
    public async Task<IActionResult> GetIndependenceRequests()
        => ToResponse(await _independenceService.GetPendingRequestsAsync());

    /// <summary>Approve = detach the teacher from the center (they become a standalone teacher).</summary>
    [HttpPost("independence-requests/{requestId:long}/approve")]
    public async Task<IActionResult> ApproveIndependence([FromRoute] long requestId)
    {
        var adminUserId = _currentUser.UserId;
        if (adminUserId is null) return UserNotResolved();
        return ToResponse(await _independenceService.ApproveAsync(adminUserId.Value, requestId));
    }

    [HttpPost("independence-requests/{requestId:long}/reject")]
    public async Task<IActionResult> RejectIndependence([FromRoute] long requestId, [FromBody] RejectIndependenceRequestDto dto)
    {
        var adminUserId = _currentUser.UserId;
        if (adminUserId is null) return UserNotResolved();
        return ToResponse(await _independenceService.RejectAsync(adminUserId.Value, requestId, dto));
    }
}
