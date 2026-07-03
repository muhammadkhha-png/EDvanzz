using System.Threading.Tasks;

namespace Edvanz.Application.ServiceContract;

/// <summary>
/// Idempotency guard for the money-movement endpoints (api/v1). Backed by the shared distributed
/// cache (Redis in prod, in-memory in dev). Opt-in per request via a client-supplied idempotency
/// key: when a key is supplied and a result was already stored for (scope, teacherId, key), the
/// caller returns that stored result instead of re-executing the money mutation — preventing a
/// double-charge on retries / double-taps.
///
/// A null/blank key disables the guard for that request (identical to the current best-effort path).
/// Cache failures fail OPEN (treated as "first time"), consistent with the codebase's cache posture
/// (EC-19) — correctness of the money path still rests on same-day dedup + per-item isolation +
/// RowVersion concurrency; this is an additional protective layer, not the sole guarantee.
/// </summary>
public interface IIdempotencyService
{
    /// <summary>
    /// Returns the previously-stored result JSON for (scope, teacherId, key) if this request was
    /// already processed (a replay), else null (first time — proceed, then call
    /// <see cref="StoreResultAsync"/>). Always returns null when the key is null/blank.
    /// </summary>
    Task<string?> GetStoredResultAsync(string scope, long teacherId, string? idempotencyKey);

    /// <summary>
    /// Stores the result JSON for (scope, teacherId, key) with a bounded TTL. No-op on a null/blank key.
    /// </summary>
    Task StoreResultAsync(string scope, long teacherId, string? idempotencyKey, string resultJson);
}
