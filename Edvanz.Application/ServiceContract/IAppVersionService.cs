using Edvanz.Application.Dtos;
using Edvanz.Application.Dtos.App;

namespace Edvanz.Application.ServiceContract;

/// <summary>
/// Resolves and edits the mobile-app version gate. DB-FIRST, OPTIONS-FALLBACK: for a platform with a
/// saved <c>AppVersionConfig</c> row the DB values win; otherwise the compile-time
/// <c>AppVersionOptions</c> default (appsettings "AppVersion") is used. The SuperAdmin dashboard edits
/// the DB rows; the anonymous startup endpoint reads the effective per-platform values.
/// </summary>
public interface IAppVersionService
{
    /// <summary>
    /// Effective values for ONE platform ("android"/"ios"; unknown → android), used by the public
    /// <c>GET /api/app/version-status</c>. Never fails — falls back to the options default when no row
    /// exists. Returns a plain DTO (the public endpoint emits a bare 200, not a Result envelope).
    /// </summary>
    Task<AppVersionPlatformDto> GetEffectivePlatformAsync(string platform);

    /// <summary>Effective config for BOTH platforms (DB row if present, else options) — the admin GET.</summary>
    Task<Result<AppVersionConfigDto>> GetEffectiveConfigAsync();

    /// <summary>
    /// Upserts BOTH platform rows from the request (records <c>UpdatedAt</c>/<c>UpdatedByUserId</c>),
    /// then returns the refreshed effective config. Validates each platform: builds non-negative,
    /// <c>latestBuild &gt;= minSupportedBuild</c>, non-empty <c>latestVersion</c>/<c>storeUrl</c> — a
    /// violation returns a localized 400 failure (no rows written).
    /// </summary>
    Task<Result<AppVersionConfigDto>> UpdateAsync(long adminUserId, UpdateAppVersionRequest request);
}
