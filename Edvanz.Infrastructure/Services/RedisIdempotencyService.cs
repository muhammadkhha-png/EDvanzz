using System;
using System.Threading.Tasks;
using Edvanz.Application.ServiceContract;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;

namespace Edvanz.Infrastructure.Services;

/// <summary>
/// Distributed-cache-backed <see cref="IIdempotencyService"/> for the api/v1 money endpoints.
/// Mirrors <see cref="RedisUserAuthCacheService"/>: uses the shared <see cref="IDistributedCache"/>
/// (StackExchange.Redis in prod, in-memory in dev) and fails OPEN on cache errors so a Redis outage
/// degrades to the current best-effort behavior rather than blocking collections.
///
/// Key format: <c>idem:{scope}:{teacherId}:{key}</c> (plus the "edvanz:" instance prefix). The stored
/// value is the serialized response DTO; a replay returns it verbatim (idempotent 200). TTL 24h — long
/// enough to absorb realistic client retries, short enough not to accumulate.
/// </summary>
public class RedisIdempotencyService : IIdempotencyService
{
    private static readonly TimeSpan Ttl = TimeSpan.FromHours(24);

    private readonly IDistributedCache _cache;
    private readonly ILogger<RedisIdempotencyService> _logger;

    public RedisIdempotencyService(IDistributedCache cache, ILogger<RedisIdempotencyService> logger)
    {
        _cache = cache;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<string?> GetStoredResultAsync(string scope, long teacherId, string? idempotencyKey)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey)) return null;
        try
        {
            return await _cache.GetStringAsync(BuildKey(scope, teacherId, idempotencyKey));
        }
        catch (Exception ex)
        {
            // Fail open: treat as first-time. The money path's own guards (same-day dedup,
            // per-item isolation, RowVersion) still hold; only the replay guard degrades.
            _logger.LogWarning(ex,
                "Idempotency read failed for {Scope}/{TeacherId} — proceeding without replay guard",
                scope, teacherId);
            return null;
        }
    }

    /// <inheritdoc />
    public async Task StoreResultAsync(string scope, long teacherId, string? idempotencyKey, string resultJson)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey)) return;
        try
        {
            await _cache.SetStringAsync(
                BuildKey(scope, teacherId, idempotencyKey),
                resultJson,
                new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = Ttl });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Idempotency write failed for {Scope}/{TeacherId}", scope, teacherId);
        }
    }

    private static string BuildKey(string scope, long teacherId, string idempotencyKey) =>
        $"idem:{scope}:{teacherId}:{idempotencyKey}";
}
