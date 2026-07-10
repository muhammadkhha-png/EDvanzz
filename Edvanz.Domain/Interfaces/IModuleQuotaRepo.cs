namespace Edvanz.Domain.Interfaces;

/// <summary>
/// Read access to the ModuleQuota reference table. Implementations may cache — the values change
/// rarely and are read on every quota-gated create for unsubscribed teachers.
/// </summary>
public interface IModuleQuotaRepo
{
    /// <summary>
    /// Returns every module's free-tier limit as a map of ModuleKey → limit. Cached briefly.
    /// </summary>
    Task<IReadOnlyDictionary<string, int>> GetLimitsAsync();
}
