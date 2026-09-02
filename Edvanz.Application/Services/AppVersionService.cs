using System.Net;
using Edvanz.Application.Dtos;
using Edvanz.Application.Dtos.App;
using Edvanz.Application.Options;
using Edvanz.Application.ServiceContract;
using Edvanz.Domain.Entities;
using Edvanz.Domain.Interfaces;
using Edvanz.Domain.Resources;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;

namespace Edvanz.Application.Services;

/// <summary>
/// DB-first, options-fallback resolution of the mobile-app version gate, plus the SuperAdmin edit path.
/// See <see cref="IAppVersionService"/>.
/// </summary>
public class AppVersionService : IAppVersionService
{
    private const string AndroidKey = "android";
    private const string IosKey = "ios";

    private readonly IUnitOfWork _unitOfWork;
    private readonly AppVersionOptions _options;
    private readonly IStringLocalizer<Messages> _localizer;

    public AppVersionService(
        IUnitOfWork unitOfWork,
        IOptionsSnapshot<AppVersionOptions> options,
        IStringLocalizer<Messages> localizer)
    {
        _unitOfWork = unitOfWork;
        // IOptionsSnapshot (scoped) re-reads config per request, so the fallback tracks a live
        // App Service settings change without a redeploy.
        _options = options.Value;
        _localizer = localizer;
    }

    /// <inheritdoc />
    public async Task<AppVersionPlatformDto> GetEffectivePlatformAsync(string platform)
    {
        var key = NormalizePlatform(platform);
        var row = await _unitOfWork.AppVersionConfigs.GetByPlatformAsync(key);
        return row is not null ? ToDto(row) : FromOptions(key);
    }

    /// <inheritdoc />
    public async Task<Result<AppVersionConfigDto>> GetEffectiveConfigAsync()
    {
        var dto = await BuildEffectiveConfigAsync();
        return Result<AppVersionConfigDto>.Success(dto, _localizer);
    }

    /// <inheritdoc />
    public async Task<Result<AppVersionConfigDto>> UpdateAsync(
        long adminUserId, UpdateAppVersionRequest request)
    {
        if (request is null || request.Android is null || request.Ios is null)
        {
            return Result<AppVersionConfigDto>.Failure(
                _localizer, "AppVersionInvalidRequest", HttpStatusCode.BadRequest);
        }

        // Validate both platforms before writing either — an invalid payload changes nothing.
        var error = Validate(request.Android) ?? Validate(request.Ios);
        if (error is not null)
        {
            return Result<AppVersionConfigDto>.Failure(_localizer, error, HttpStatusCode.BadRequest);
        }

        var now = DateTime.UtcNow;
        await _unitOfWork.AppVersionConfigs.UpsertAsync(ToEntity(AndroidKey, request.Android, adminUserId, now));
        await _unitOfWork.AppVersionConfigs.UpsertAsync(ToEntity(IosKey, request.Ios, adminUserId, now));
        await _unitOfWork.SaveChangesAsync();

        var dto = await BuildEffectiveConfigAsync();
        return Result<AppVersionConfigDto>.Success(dto, _localizer, "AppVersionUpdated");
    }

    // ════════════════════════════════════════════════
    // PRIVATE HELPERS
    // ════════════════════════════════════════════════

    /// <summary>Both platforms' effective config (DB row if present, else the options default).</summary>
    private async Task<AppVersionConfigDto> BuildEffectiveConfigAsync()
    {
        var rows = await _unitOfWork.AppVersionConfigs.GetAllAsync();
        var byPlatform = rows
            .GroupBy(r => (r.Platform ?? string.Empty).Trim().ToLowerInvariant())
            .ToDictionary(g => g.Key, g => g.First());

        return new AppVersionConfigDto
        {
            Android = byPlatform.TryGetValue(AndroidKey, out var a) ? ToDto(a) : FromOptions(AndroidKey),
            Ios = byPlatform.TryGetValue(IosKey, out var i) ? ToDto(i) : FromOptions(IosKey)
        };
    }

    /// <summary>Returns a resx key when the platform payload is invalid; null when it is valid.</summary>
    private static string? Validate(AppVersionPlatformDto p)
    {
        if (p.MinSupportedBuild < 0 || p.LatestBuild < 0 || p.LatestBuild < p.MinSupportedBuild)
            return "AppVersionBuildsInvalid";
        if (string.IsNullOrWhiteSpace(p.LatestVersion) || string.IsNullOrWhiteSpace(p.StoreUrl))
            return "AppVersionFieldsRequired";
        return null;
    }

    private static string NormalizePlatform(string? platform)
        => string.Equals(platform?.Trim(), IosKey, StringComparison.OrdinalIgnoreCase) ? IosKey : AndroidKey;

    private static AppVersionPlatformDto ToDto(AppVersionConfig row) => new()
    {
        MinSupportedBuild = row.MinSupportedBuild,
        LatestBuild = row.LatestBuild,
        LatestVersion = row.LatestVersion ?? string.Empty,
        StoreUrl = row.StoreUrl ?? string.Empty
    };

    private AppVersionPlatformDto FromOptions(string platformKey)
    {
        var o = platformKey == IosKey ? _options.iOS : _options.Android;
        return new AppVersionPlatformDto
        {
            MinSupportedBuild = o.MinSupportedBuild,
            LatestBuild = o.LatestBuild,
            LatestVersion = o.LatestVersion ?? string.Empty,
            StoreUrl = o.StoreUrl ?? string.Empty
        };
    }

    private static AppVersionConfig ToEntity(
        string platformKey, AppVersionPlatformDto p, long adminUserId, DateTime now) => new()
    {
        Platform = platformKey,
        MinSupportedBuild = p.MinSupportedBuild,
        LatestBuild = p.LatestBuild,
        LatestVersion = p.LatestVersion.Trim(),
        StoreUrl = p.StoreUrl.Trim(),
        UpdatedAt = now,
        UpdatedByUserId = adminUserId,
        // Consumed only when the upsert INSERTS a new row (an update keeps the existing CreateAt).
        CreateAt = now
    };
}
