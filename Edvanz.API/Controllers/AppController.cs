using System.Globalization;
using Edvanz.Application.Dtos.App;
using Edvanz.Application.ServiceContract;
using Edvanz.Domain.Resources;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;

namespace Edvanz.API.Controllers;

/// <summary>
/// Public app-shell endpoints hit before authentication. Anonymous by design — the mobile client
/// calls <see cref="GetVersionStatus"/> on startup (no token yet) to decide whether to FORCE or
/// SUGGEST an update.
/// </summary>
[ApiController]
[Route("api/app")]
public class AppController : ControllerBase
{
    private readonly IAppVersionService _appVersionService;
    private readonly IStringLocalizer<Messages> _localizer;

    public AppController(
        IAppVersionService appVersionService,
        IStringLocalizer<Messages> localizer)
    {
        _appVersionService = appVersionService;
        _localizer = localizer;
    }

    /// <summary>
    /// Startup update gate. Compares the caller's build to the platform's EFFECTIVE thresholds
    /// (DB-first: a saved <c>AppVersionConfig</c> row wins, else the compile-time <c>AppVersionOptions</c>
    /// default) and returns whether the client must (<c>forced</c>), may (<c>optional</c>), or need not
    /// (<c>none</c>) update, plus the store link and a localized title/message (localized to the request
    /// culture via Accept-Language; empty for <c>none</c>).
    ///
    /// <para><b>Robustness (never throws):</b> an unknown/missing <paramref name="platform"/> falls back
    /// to Android; a missing/non-numeric <paramref name="build"/> is treated as build 0 (i.e. below any
    /// positive minimum → <c>forced</c>), so a malformed request errs toward prompting an update rather
    /// than letting an unknown build through. Real clients always send a valid platform + build.</para>
    /// </summary>
    /// <param name="platform">"android" | "ios" (case-insensitive); anything else → Android.</param>
    /// <param name="build">The client's integer build number; missing/non-numeric → 0.</param>
    [HttpGet("version-status")]
    [AllowAnonymous]
    public async Task<IActionResult> GetVersionStatus(
        [FromQuery] string? platform = null, [FromQuery] string? build = null)
    {
        // Effective per-platform config (DB row if present, else options fallback). The service
        // normalizes an unknown/missing platform to Android and never throws.
        AppVersionPlatformDto cfg = await _appVersionService.GetEffectivePlatformAsync(platform ?? string.Empty);

        // Parse the build defensively — a missing/garbage value becomes 0 (forces update) instead of a 400.
        int clientBuild =
            int.TryParse(build?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
                ? parsed
                : 0;

        string updateMode;
        if (clientBuild < cfg.MinSupportedBuild) updateMode = "forced";
        else if (clientBuild < cfg.LatestBuild) updateMode = "optional";
        else updateMode = "none";

        // Localized copy for the update dialog (empty when no update is offered). IStringLocalizer
        // resolves against the request culture set by the localization middleware (Accept-Language).
        string title = string.Empty;
        string message = string.Empty;
        if (updateMode == "forced")
        {
            title = _localizer["AppUpdateForcedTitle"];
            message = _localizer["AppUpdateForcedMessage"];
        }
        else if (updateMode == "optional")
        {
            title = _localizer["AppUpdateOptionalTitle"];
            message = _localizer["AppUpdateOptionalMessage"];
        }

        // Plain 200 JSON — property names are the exact wire contract the app matches (camelCase).
        return Ok(new
        {
            updateMode,
            latestVersion = cfg.LatestVersion ?? string.Empty,
            latestBuild = cfg.LatestBuild,
            minSupportedBuild = cfg.MinSupportedBuild,
            storeUrl = cfg.StoreUrl ?? string.Empty,
            title,
            message
        });
    }
}
