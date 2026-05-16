using Edvanz.Domain.Interfaces;

namespace Edvanz.Application.IservicesContract;

/// <summary>
/// Redis-backed cache for the per-user authorization snapshot
/// (REQ-USR-013 / REQ-USR-027 / REQ-USR-008 / BR-ADM-010).
///
/// Backs <c>SecurityStampValidationMiddleware</c> so per-request authorization
/// resolves in single-digit milliseconds even under high concurrency.
///
/// Implemented over <c>IDistributedCache</c> — StackExchange.Redis in prod
/// (Memurai locally), in-memory in dev when no connection is configured.
/// EC-19: when Redis is unavailable, the middleware falls back to a direct
/// DB read so correctness is preserved; only performance degrades.
///
/// INVALIDATION DISCIPLINE:
///   This service does NOT manage when to invalidate. Every state-change site
///   (assistant update, status toggle, password change, module grant/revoke)
///   must call <see cref="InvalidateAsync"/> synchronously after the DB
///   commit succeeds — the same pattern used by <c>ISubscriptionCacheService</c>.
/// </summary>
public interface IUserAuthCacheService
{
    /// <summary>
    /// Returns the cached snapshot or null on miss.
    /// Swallows <c>IDistributedCache</c> exceptions and returns null so the
    /// caller can transparently fall back to the database (EC-19).
    /// </summary>
    /// <param name="userId">The user's primary key — matches
    /// <c>User.Id</c> and the JWT <c>NameIdentifier</c> claim.</param>
    Task<UserAuthSnapshot?> GetAsync(long userId);

    /// <summary>
    /// Writes the snapshot with the configured default TTL.
    /// Failures are logged and swallowed — the next request will simply
    /// re-read from the database and re-attempt the write.
    /// </summary>
    Task SetAsync(long userId, UserAuthSnapshot snapshot);

    /// <summary>
    /// Drops the cache entry for a user. Called synchronously by every
    /// service that mutates a user's effective access state — immediately
    /// after the DB commit succeeds.
    ///
    /// Failures are logged at Error level: a stale entry could let a
    /// revoked assistant retain a permission for up to the TTL window.
    /// The TTL is the safety net for these failures.
    /// </summary>
    Task InvalidateAsync(long userId);
}