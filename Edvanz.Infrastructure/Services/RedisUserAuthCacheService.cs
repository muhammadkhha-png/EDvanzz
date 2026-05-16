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
/// Redis-backed per-user auth snapshot cache
/// (REQ-USR-013 / REQ-USR-027 / REQ-USR-008 / BR-ADM-010).
///
/// Backs <c>SecurityStampValidationMiddleware</c> so per-request authorization
/// stays in single-digit milliseconds even under high concurrency.
///
/// Implementation notes:
///   - Storage: <c>IDistributedCache</c> (StackExchange.Redis pointed at
///     Memurai locally; in-memory fallback in dev when <c>Redis:Connection</c>
///     is empty).
///   - EC-19: <c>GetAsync</c> swallows <c>IDistributedCache</c> exceptions and
///     returns null so the middleware falls back to a direct DB read.
///     Correctness is preserved — only performance degrades.
///   - JSON serialization of <see cref="UserAuthSnapshot"/> is small
///     (~500 bytes per entry for a typical assistant with 10 permissions)
///     so we don't bother with binary encoding.
///
/// This class mirrors <c>RedisSubscriptionCacheService</c> deliberately —
/// the two caches are siblings, not a hierarchy, but they share enough
/// operational characteristics that divergence in behavior would be a
/// maintenance hazard.
/// </summary>
public class RedisUserAuthCacheService : IUserAuthCacheService
{
    // Single shared JsonSerializerOptions instance — System.Text.Json caches
    // metadata against the options instance, so reusing this saves allocations
    // and reflection on every serialize/deserialize call.
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly IDistributedCache _cache;
    private readonly UserAuthCacheOptions _options;
    private readonly ILogger<RedisUserAuthCacheService> _logger;

    public RedisUserAuthCacheService(
        IDistributedCache cache,
        IOptions<UserAuthCacheOptions> options,
        ILogger<RedisUserAuthCacheService> logger)
    {
        _cache = cache;
        _options = options.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<UserAuthSnapshot?> GetAsync(long userId)
    {
        string key = BuildKey(userId);

        try
        {
            string? json = await _cache.GetStringAsync(key);
            if (string.IsNullOrEmpty(json))
                return null;

            return JsonSerializer.Deserialize<UserAuthSnapshot>(json, JsonOptions);
        }
        catch (Exception ex)
        {
            // EC-19: Redis unavailable. Caller falls back to DB — degrade gracefully.
            // Log at Warning (not Error) because the path is self-healing.
            _logger.LogWarning(ex,
                "User auth cache read failed for user {UserId} — falling back to DB",
                userId);
            return null;
        }
    }

    /// <inheritdoc />
    public async Task SetAsync(long userId, UserAuthSnapshot snapshot)
    {
        if (snapshot is null) return;

        string key = BuildKey(userId);
        string json = JsonSerializer.Serialize(snapshot, JsonOptions);

        var entryOptions = new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = _options.DefaultTtl
        };

        try
        {
            await _cache.SetStringAsync(key, json, entryOptions);
        }
        catch (Exception ex)
        {
            // Cache write failure is non-fatal — the next request simply re-reads
            // from the database and re-attempts the write. Log at Warning so an
            // operator can detect sustained outages but not flood the logs on
            // single transient failures.
            _logger.LogWarning(ex,
                "User auth cache write failed for user {UserId}", userId);
        }
    }

    /// <inheritdoc />
    public async Task InvalidateAsync(long userId)
    {
        string key = BuildKey(userId);

        try
        {
            await _cache.RemoveAsync(key);
        }
        catch (Exception ex)
        {
            // Invalidation failure is the most dangerous failure mode here:
            // a stale entry could let a revoked assistant retain a permission
            // for up to the TTL window (UserAuthCacheOptions.DefaultTtl).
            //
            // Log at Error so ops sees it. The TTL is the safety net.
            _logger.LogError(ex,
                "User auth cache invalidation FAILED for user {UserId} — TTL is the safety net",
                userId);
        }
    }

    // ════════════════════════════════════════════════
    // PRIVATE HELPERS
    // ════════════════════════════════════════════════

    /// <summary>
    /// Builds the Redis key for a given user. Format defined in
    /// <see cref="AuthConstants.UserAuthCacheKeyFormat"/>. Combined with the
    /// <c>"edvanz:"</c> instance prefix configured in
    /// <c>InfrastructureServiceExtensions</c>, the full Redis key becomes:
    /// <c>"edvanz:auth:user:{userId}"</c>.
    /// </summary>
    private static string BuildKey(long userId) =>
        string.Format(AuthConstants.UserAuthCacheKeyFormat, userId);
}