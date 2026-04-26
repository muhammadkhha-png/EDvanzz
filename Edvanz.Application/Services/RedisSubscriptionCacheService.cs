using System.Text.Json;
using Edvanz.Application.IservicesContract;
using Edvanz.Application.Options;
using Edvanz.Domain.Constants;
using Edvanz.Domain.Interfaces;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Edvanz.Infrastructure.Services;

/// <summary>
/// Redis-backed subscription-status cache (§8.7 / NFR-SUB-007).
///
/// Backs ActiveSubscriptionHandler (§8.2) so the policy check stays in
/// single-digit milliseconds even under high concurrency.
///
/// Implementation notes:
///   - Storage: IDistributedCache (StackExchange.Redis in prod, in-memory in dev
///     based on the Program.cs registration).
///   - Adaptive TTL (§8.6): when a projection's EndDate is within
///     SubscriptionCacheOptions.NearExpiryThreshold of UtcNow, we use the shorter
///     NearExpiryTtl. Minimizes the staleness window for a just-expired sub.
///   - EC-19 (Redis down): GetAsync swallows IDistributedCache exceptions and
///     returns null. The handler then performs a direct DB read and continues.
///     Performance degrades; correctness does not.
///   - JSON serialization of CurrentSubscriptionStatusProjection is small (~150
///     bytes / entry) so we don't bother with binary encoding.
/// </summary>
public class RedisSubscriptionCacheService : ISubscriptionCacheService
{
    // System.Text.Json options shared across the class — created once for hot-path reuse.
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly IDistributedCache _cache;
    private readonly SubscriptionCacheOptions _options;
    private readonly ILogger<RedisSubscriptionCacheService> _logger;

    public RedisSubscriptionCacheService(
        IDistributedCache cache,
        IOptions<SubscriptionCacheOptions> options,
        ILogger<RedisSubscriptionCacheService> logger)
    {
        _cache = cache;
        _options = options.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<CurrentSubscriptionStatusProjection?> GetAsync(long teacherId)
    {
        string key = BuildKey(teacherId);

        try
        {
            string? json = await _cache.GetStringAsync(key);
            if (string.IsNullOrEmpty(json))
                return null;

            return JsonSerializer.Deserialize<CurrentSubscriptionStatusProjection>(json, JsonOptions);
        }
        catch (Exception ex)
        {
            // EC-19: Redis unavailable. Caller falls back to DB — degrade gracefully.
            _logger.LogWarning(ex,
                "Subscription cache read failed for teacher {TeacherId} — falling back to DB",
                teacherId);
            return null;
        }
    }

    /// <inheritdoc />
    public async Task SetAsync(long teacherId, CurrentSubscriptionStatusProjection projection)
    {
        if (projection is null) return;

        string key = BuildKey(teacherId);
        string json = JsonSerializer.Serialize(projection, JsonOptions);

        var entryOptions = new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = ResolveTtl(projection.EndDate)
        };

        try
        {
            await _cache.SetStringAsync(key, json, entryOptions);
        }
        catch (Exception ex)
        {
            // Cache failures are non-fatal — log and move on. The handler will
            // simply re-query the DB on the next request.
            _logger.LogWarning(ex,
                "Subscription cache write failed for teacher {TeacherId}", teacherId);
        }
    }

    /// <inheritdoc />
    public async Task InvalidateAsync(long teacherId)
    {
        string key = BuildKey(teacherId);

        try
        {
            await _cache.RemoveAsync(key);
        }
        catch (Exception ex)
        {
            // Invalidation failure is the most dangerous failure mode here:
            // a stale entry could let an expired teacher through for up to 30 min.
            // Log loudly so ops sees it. The 30-min default TTL is the safety net.
            _logger.LogError(ex,
                "Subscription cache invalidation FAILED for teacher {TeacherId} — TTL is the safety net",
                teacherId);
        }
    }

    // ════════════════════════════════════════════════
    // PRIVATE HELPERS
    // ════════════════════════════════════════════════

    private static string BuildKey(long teacherId) =>
        string.Format(SubscriptionConstants.CacheKeyFormat, teacherId);

    /// <summary>
    /// Picks the adaptive TTL per §8.6: short TTL when EndDate is close to UtcNow,
    /// default TTL otherwise.
    /// </summary>
    private TimeSpan ResolveTtl(DateTime endDateUtc)
    {
        TimeSpan timeUntilExpiry = endDateUtc - DateTime.UtcNow;

        // If already expired or within the near-expiry threshold, use the short TTL.
        // Negative timeUntilExpiry (already past EndDate) is the most extreme case
        // of "near expiry" so the same short TTL applies.
        if (timeUntilExpiry <= _options.NearExpiryThreshold)
        {
            return _options.NearExpiryTtl;
        }

        return _options.DefaultTtl;
    }
}