namespace Edvanz.Application.Options;

/// <summary>
/// Configuration for the Redis-backed per-user auth snapshot cache
/// (REQ-USR-013 / REQ-USR-027 / BR-ADM-010).
///
/// Bound from <c>appsettings.json</c> section <c>"UserAuthCache"</c>.
///
/// CACHE STRATEGY:
///   - <see cref="DefaultTtl"/> is the safety-net TTL. The primary correctness
///     mechanism is synchronous invalidation on every state-change commit —
///     the TTL only matters if Redis missed an invalidation call (e.g.,
///     transient outage). Keep it short enough that worst-case staleness is
///     acceptable but long enough to keep hit rate high.
///   - This cache has no near-expiry adaptive TTL (unlike subscription cache)
///     because user-auth state has no equivalent of an EndDate.
/// </summary>
public class UserAuthCacheOptions
{
    public const string Section = "UserAuthCache";

    /// <summary>
    /// Default TTL for a cached snapshot. Default 15 minutes.
    ///
    /// Sizing rationale: write-side invalidation is the correctness mechanism;
    /// TTL is purely defensive. 15 min keeps the safety-net window short
    /// (a tutor's revocation will land within 15 min even if invalidation
    /// silently failed) without thrashing the DB for users who are idle.
    /// </summary>
    public TimeSpan DefaultTtl { get; set; } = TimeSpan.FromMinutes(15);
}