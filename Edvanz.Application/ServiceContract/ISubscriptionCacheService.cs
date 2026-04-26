using Edvanz.Domain.Interfaces;

namespace Edvanz.Application.IservicesContract;

/// <summary>
/// Redis-backed cache for the per-teacher current-subscription projection (§8.7 / NFR-SUB-007).
/// Backs the ActiveSubscriptionHandler (§8.2) so the policy check stays at single-digit
/// milliseconds even under high concurrency.
///
/// Implemented over IDistributedCache (StackExchange.Redis in prod; in-memory in dev).
/// EC-19: when Redis is unavailable, the handler falls back to a direct DB read.
/// </summary>
public interface ISubscriptionCacheService
{
    /// <summary>
    /// Returns the cached projection or null on miss.
    /// SemaphoreSlim-keyed lock prevents thundering-herd DB queries for the same teacher.
    /// </summary>
    Task<CurrentSubscriptionStatusProjection?> GetAsync(long teacherId);

    /// <summary>
    /// Writes the projection with the configured TTL. Adaptive TTL: when EndDate
    /// is within 10 minutes of UtcNow, TTL is reduced to 1 minute (§8.6) to
    /// minimize the window where a just-expired subscription could appear active.
    /// </summary>
    Task SetAsync(long teacherId, CurrentSubscriptionStatusProjection projection);

    /// <summary>
    /// Drops the cache entry for a teacher. Called synchronously by SubscriptionService
    /// and AdminSubscriptionService immediately after commit (§6.3 step 10).
    /// </summary>
    Task InvalidateAsync(long teacherId);
}