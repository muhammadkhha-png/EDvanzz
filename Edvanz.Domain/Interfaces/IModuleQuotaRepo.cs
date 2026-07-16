using Edvanz.Domain.Entities;

namespace Edvanz.Domain.Interfaces;

/// <summary>
/// Access to the ModuleQuota reference table. Implementations may cache the limits map — the
/// values change rarely and are read on every quota-gated create for unsubscribed teachers.
/// </summary>
public interface IModuleQuotaRepo
{
    /// <summary>
    /// Returns every module's free-tier limit as a map of ModuleKey → limit. Cached briefly.
    /// </summary>
    Task<IReadOnlyDictionary<string, int>> GetLimitsAsync();

    /// <summary>
    /// All quota rows, AsNoTracking, ordered by ModuleKey. Feeds the admin list endpoint.
    /// </summary>
    Task<IReadOnlyList<ModuleQuota>> GetAllAsync();

    /// <summary>
    /// Tracked lookup by unique ModuleKey (case per UX_ModuleQuotas_ModuleKey collation)
    /// for the admin update path. Null when no row exists — the endpoint 404s rather than
    /// inserting (keys are code-defined in ModuleQuotaKeys and seeded by migration).
    /// </summary>
    Task<ModuleQuota?> GetByKeyAsync(string moduleKey);
}
