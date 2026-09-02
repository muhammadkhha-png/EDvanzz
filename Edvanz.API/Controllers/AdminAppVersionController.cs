using Edvanz.API.Attributes;
using Edvanz.Application.Dtos.App;
using Edvanz.Application.IservicesContract;
using Edvanz.Application.ServiceContract;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Net;

namespace Edvanz.API.Controllers;

/// <summary>
/// SuperAdmin administration of the mobile-app version gate (the AppVersionConfig table read by the
/// anonymous <c>GET /api/app/version-status</c> startup check).
///
/// DB-FIRST, OPTIONS-FALLBACK: a saved row per platform wins; an unsaved platform falls back to the
/// compile-time <c>AppVersionOptions</c> default. Saving here takes effect immediately (no redeploy).
///
/// AUTHORIZATION:
///   Class-level [Authorize]; every action [ModulePermission(roles: ["SuperAdmin"], roleOnly: true)]
///   (mirrors AdminModuleQuotaController / AdminTutorModuleController). The editor's user id is read
///   ONLY from the JWT (never the body/route — CLAUDE.md §3.3).
/// </summary>
[Route("api/admin/app-version")]
[Authorize]
public class AdminAppVersionController : ApiBaseController
{
    private readonly IAppVersionService _appVersionService;
    private readonly ICurrentUserService _currentUser;

    public AdminAppVersionController(
        IAppVersionService appVersionService,
        ICurrentUserService currentUser)
    {
        _appVersionService = appVersionService;
        _currentUser = currentUser;
    }

    // ══════════════════════════════════════════════════════════════════════════
    // ENDPOINT 1: READ EFFECTIVE CONFIG (BOTH PLATFORMS)
    // ══════════════════════════════════════════════════════════════════════════
    //
    // GET /api/admin/app-version
    //   → { android:{minSupportedBuild,latestBuild,latestVersion,storeUrl}, ios:{...} }
    //   (DB row if present, else the AppVersionOptions default) — under the standard Result envelope.
    //
    // ══════════════════════════════════════════════════════════════════════════
    [HttpGet]
    [ModulePermission(roles: new[] { "SuperAdmin" }, roleOnly: true)]
    [ProducesResponseType(typeof(Edvanz.Application.Dtos.Result<AppVersionConfigDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAppVersion()
    {
        long? adminUserId = _currentUser.UserId;
        if (adminUserId is null) return AdminNotResolved();

        var result = await _appVersionService.GetEffectiveConfigAsync();
        return ToResponse(result);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // ENDPOINT 2: UPSERT BOTH PLATFORMS
    // ══════════════════════════════════════════════════════════════════════════
    //
    // PUT /api/admin/app-version
    //   { android:{minSupportedBuild,latestBuild,latestVersion,storeUrl}, ios:{...} }
    //   → upserts both rows (records who/when), returns the refreshed effective config.
    //   400 when builds are negative, latestBuild < minSupportedBuild, or a version/storeUrl is empty.
    //
    // ══════════════════════════════════════════════════════════════════════════
    [HttpPut]
    [ModulePermission(roles: new[] { "SuperAdmin" }, roleOnly: true)]
    [ProducesResponseType(typeof(Edvanz.Application.Dtos.Result<AppVersionConfigDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpdateAppVersion([FromBody] UpdateAppVersionRequest request)
    {
        long? adminUserId = _currentUser.UserId;
        if (adminUserId is null) return AdminNotResolved();

        var result = await _appVersionService.UpdateAsync(adminUserId.Value, request);
        return ToResponse(result);
    }

    // ════════════════════════════════════════════════
    // PRIVATE HELPERS
    // ════════════════════════════════════════════════

    /// <summary>Standardized response when the calling admin's user id cannot be read from claims.</summary>
    private IActionResult AdminNotResolved()
    {
        return new ObjectResult(new { success = false, message = "Admin user not resolved" })
        {
            StatusCode = (int)HttpStatusCode.Unauthorized
        };
    }
}
