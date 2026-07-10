using Edvanz.Application.Options;
using Edvanz.Application.ServiceContract;
using Edvanz.Domain.Constants;
using Edvanz.Domain.Entities;
using Edvanz.Domain.Enums;
using Edvanz.Domain.Helpers;
using Edvanz.Domain.Interfaces;
using Microsoft.Extensions.Options;

namespace Edvanz.Application.Services;

/// <summary>
/// Resolves subscription status (reusing the same source of truth as the auth layer:
/// current-subscription projection + <see cref="SubscriptionStatusCalculator"/>) and enforces the
/// per-module free-tier quotas stored in the ModuleQuota table.
///
/// The quota table is read through a short-lived process-wide cache (60s) so a quota-gated create
/// costs at most one extra query per minute, and runtime edits to a row take effect within that
/// window. If a module has no row (e.g. before the seed migration runs), it falls back to the
/// <see cref="FreeTierQuotaOptions"/> config for the original four modules, else fails open.
/// </summary>
public sealed class SubscriptionGateService : ISubscriptionGateService
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(60);
    private static readonly SemaphoreSlim CacheLock = new(1, 1);
    private static IReadOnlyDictionary<string, int>? _cachedLimits;
    private static DateTime _cachedAtUtc;

    private readonly IUnitOfWork _unitOfWork;
    private readonly IOptionsMonitor<FreeTierQuotaOptions> _fallback;

    public SubscriptionGateService(
        IUnitOfWork unitOfWork,
        IOptionsMonitor<FreeTierQuotaOptions> fallback)
    {
        _unitOfWork = unitOfWork;
        _fallback = fallback;
    }

    /// <inheritdoc />
    public async Task<bool> HasActiveSubscriptionAsync(long teacherId)
    {
        var projection = await _unitOfWork.Users.GetCurrentSubscriptionStatusAsync(teacherId);
        if (projection is null)
            return false;

        var subForStatus = new TeacherSubscription
        {
            StartDate = projection.StartDate,
            EndDate = projection.EndDate,
            IsCurrent = true
        };

        var status = SubscriptionStatusCalculator.Derive(subForStatus, DateTime.UtcNow);
        return status == SubscriptionStatus.Active || status == SubscriptionStatus.ExpiringSoon;
    }

    /// <inheritdoc />
    public async Task<bool> CanCreateAsync(long teacherId, string moduleKey, Func<Task<int>> currentCountFactory)
    {
        if (await HasActiveSubscriptionAsync(teacherId))
            return true;

        int limit = await GetLimitAsync(moduleKey);
        if (limit <= 0)
            return false; // subscriber-only module — no need to count

        int current = await currentCountFactory();
        return current < limit;
    }

    private async Task<int> GetLimitAsync(string moduleKey)
    {
        var limits = await GetLimitsCachedAsync();
        if (limits.TryGetValue(moduleKey, out var v))
            return v;

        // Row missing (e.g. seed migration not yet applied) — fall back to config for the original
        // four modules, otherwise fail open so an unseeded module is never accidentally blocked.
        var opt = _fallback.CurrentValue;
        return moduleKey switch
        {
            ModuleQuotaKeys.Students => opt.Students,
            ModuleQuotaKeys.Sessions => opt.Sessions,
            ModuleQuotaKeys.Assistants => opt.Assistants,
            ModuleQuotaKeys.Groups => opt.Groups,
            _ => int.MaxValue
        };
    }

    private async Task<IReadOnlyDictionary<string, int>> GetLimitsCachedAsync()
    {
        if (_cachedLimits is not null && DateTime.UtcNow - _cachedAtUtc < CacheTtl)
            return _cachedLimits;

        await CacheLock.WaitAsync();
        try
        {
            if (_cachedLimits is not null && DateTime.UtcNow - _cachedAtUtc < CacheTtl)
                return _cachedLimits;

            var limits = await _unitOfWork.ModuleQuotaRepo.GetLimitsAsync();
            _cachedLimits = limits;
            _cachedAtUtc = DateTime.UtcNow;
            return limits;
        }
        finally
        {
            CacheLock.Release();
        }
    }
}
