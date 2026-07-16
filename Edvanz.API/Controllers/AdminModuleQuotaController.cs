using Edvanz.API.Attributes;
using Edvanz.Application.Dtos.Subscription;
using Edvanz.Application.IservicesContract;
using Edvanz.Application.ServiceContract;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Net;

namespace Edvanz.API.Controllers;

/// <summary>
/// Free-tier module-quota administration (the ModuleQuotas limits table).
///
/// An unsubscribed/expired teacher may create up to FreeTierLimit items per module
/// (0 = subscriber-only); an active subscription lifts the caps. These endpoints let
/// the super admin tune those limits at runtime: changes apply immediately on this
/// instance (the SubscriptionGateService cache is invalidated) and within 60 seconds
/// on any other instance.
///
/// AUTHORIZATION:
///   Class-level [Authorize]; every action [ModulePermission(roles: ["SuperAdmin"], roleOnly: true)]
///   (mirrors AdminSubscriptionController / AdminTutorModuleController).
/// </summary>
[Route("api/admin/module-quotas")]
[Authorize]
public class AdminModuleQuotaController : ApiBaseController
{
    private readonly IAdminSubscriptionService _adminService;
    private readonly ICurrentUserService _currentUser;

    public AdminModuleQuotaController(
        IAdminSubscriptionService adminService,
        ICurrentUserService currentUser)
    {
        _adminService = adminService;
        _currentUser = currentUser;
    }

    // ══════════════════════════════════════════════════════════════════════════
    // ENDPOINT 1: LIST MODULE QUOTAS
    // ══════════════════════════════════════════════════════════════════════════
    //
    // WHAT IT DOES:
    //   Returns every module's free-tier creation limit, ordered by ModuleKey.
    //
    // TABLES READ: ModuleQuotas
    //
    // SAMPLE: GET /api/admin/module-quotas
    //
    // ══════════════════════════════════════════════════════════════════════════
    [HttpGet]
    [ModulePermission(roles: new[] { "SuperAdmin" }, roleOnly: true)]
    [ProducesResponseType(typeof(Edvanz.Application.Dtos.Result<System.Collections.Generic.List<Edvanz.Application.Dtos.Subscription.ModuleQuotaDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetModuleQuotas()
    {
        long? adminUserId = _currentUser.UserId;
        if (adminUserId is null) return AdminNotResolved();

        var result = await _adminService.GetModuleQuotasAsync();
        return ToResponse(result);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // ENDPOINT 2: UPDATE ONE MODULE'S LIMIT
    // ══════════════════════════════════════════════════════════════════════════
    //
    // WHAT IT DOES:
    //   Sets a module's FreeTierLimit (0–10,000; 0 = subscriber-only) and optional
    //   description; records who/when. 404 for a key with no seeded row (keys are
    //   code-defined in ModuleQuotaKeys — e.g. Students, Sessions, Videos, Exams,
    //   OnlineExams). Takes effect immediately (gate cache invalidated).
    //
    // TABLES WRITTEN: ModuleQuotas
    //
    // SAMPLE: PUT /api/admin/module-quotas/Sessions
    //   { "freeTierLimit": 2, "description": "Promo: two free sessions" }
    //
    // ══════════════════════════════════════════════════════════════════════════
    [HttpPut("{moduleKey}")]
    [ModulePermission(roles: new[] { "SuperAdmin" }, roleOnly: true)]
    [ProducesResponseType(typeof(Edvanz.Application.Dtos.Result<Edvanz.Application.Dtos.Subscription.ModuleQuotaDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateModuleQuota(
        [FromRoute] string moduleKey,
        [FromBody] UpdateModuleQuotaRequest request)
    {
        long? adminUserId = _currentUser.UserId;
        if (adminUserId is null) return AdminNotResolved();

        var result = await _adminService.UpdateModuleQuotaAsync(adminUserId.Value, moduleKey, request);
        return ToResponse(result);
    }

    // ════════════════════════════════════════════════
    // PRIVATE HELPERS
    // ════════════════════════════════════════════════

    /// <summary>
    /// Standardized response when the calling admin's user id cannot be read from
    /// claims (mirrors AdminSubscriptionController).
    /// </summary>
    private IActionResult AdminNotResolved()
    {
        return new ObjectResult(new { success = false, message = "Admin user not resolved" })
        {
            StatusCode = (int)HttpStatusCode.Unauthorized
        };
    }
}
