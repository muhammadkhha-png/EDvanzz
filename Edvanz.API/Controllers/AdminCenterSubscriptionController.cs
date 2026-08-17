using Edvanz.API.Attributes;
using Edvanz.Application.Dtos.Center;
using Edvanz.Application.IservicesContract;
using Edvanz.Application.ServiceContract;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Edvanz.API.Controllers;

/// <summary>
/// SuperAdmin management of center subscriptions (quota packages): activate/override, approve/reject
/// center requests, and edit per-slot pricing. Mirrors AdminSubscriptionController.
/// </summary>
[Route("api/admin/center-subscriptions")]
[Authorize]
[ModulePermission(roles: new[] { "SuperAdmin" }, roleOnly: true)]
public class AdminCenterSubscriptionController : ApiBaseController
{
    private readonly IAdminCenterSubscriptionService _service;
    private readonly ICenterSubscriptionService _centerSubscriptionService;
    private readonly ICurrentUserService _currentUser;

    public AdminCenterSubscriptionController(
        IAdminCenterSubscriptionService service,
        ICenterSubscriptionService centerSubscriptionService,
        ICurrentUserService currentUser)
    {
        _service = service;
        _centerSubscriptionService = centerSubscriptionService;
        _currentUser = currentUser;
    }

    /// <summary>A center's current subscription entitlement + status + usage (for the admin panel).</summary>
    [HttpGet("{centerId:long}")]
    public async Task<IActionResult> GetForCenter([FromRoute] long centerId)
        => ToResponse(await _centerSubscriptionService.GetSubscriptionAsync(centerId));

    [HttpPost("activate")]
    public async Task<IActionResult> Activate([FromBody] ActivateCenterSubscriptionDto dto)
    {
        var adminUserId = _currentUser.UserId;
        if (adminUserId is null) return UserNotResolved();
        return ToResponse(await _service.ActivateAsync(adminUserId.Value, dto));
    }

    [HttpGet("requests")]
    public async Task<IActionResult> GetRequests()
        => ToResponse(await _service.GetPendingRequestsAsync());

    [HttpPost("requests/{requestId:long}/approve")]
    public async Task<IActionResult> Approve([FromRoute] long requestId, [FromBody] ApproveCenterSubscriptionRequestDto dto)
    {
        var adminUserId = _currentUser.UserId;
        if (adminUserId is null) return UserNotResolved();
        return ToResponse(await _service.ApproveRequestAsync(adminUserId.Value, requestId, dto));
    }

    [HttpPost("requests/{requestId:long}/reject")]
    public async Task<IActionResult> Reject([FromRoute] long requestId, [FromBody] RejectCenterSubscriptionRequestDto dto)
    {
        var adminUserId = _currentUser.UserId;
        if (adminUserId is null) return UserNotResolved();
        return ToResponse(await _service.RejectRequestAsync(adminUserId.Value, requestId, dto));
    }

    [HttpGet("pricing")]
    public async Task<IActionResult> GetPricing()
        => ToResponse(await _service.GetPricingAsync());

    [HttpPut("pricing")]
    public async Task<IActionResult> UpdatePricing([FromBody] CenterPricingDto dto)
    {
        var adminUserId = _currentUser.UserId;
        if (adminUserId is null) return UserNotResolved();
        return ToResponse(await _service.UpdatePricingAsync(adminUserId.Value, dto));
    }
}
