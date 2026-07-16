using Edvanz.Domain.Entities;
using Edvanz.Domain.Interfaces;
using Edvanz.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Edvanz.Infrastructure.Repositories;

/// <summary>
/// Reads the ModuleQuota reference table. Caching is handled by the caller
/// (SubscriptionGateService) so this stays a thin, query-only repo consistent with the
/// UnitOfWork's parameterless-construction pattern.
/// </summary>
public class ModuleQuotaRepo : IModuleQuotaRepo
{
    private readonly EdvanzDbContext _context;

    public ModuleQuotaRepo(EdvanzDbContext context)
    {
        _context = context;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<string, int>> GetLimitsAsync()
    {
        return await _context.Set<ModuleQuota>()
            .AsNoTracking()
            .ToDictionaryAsync(q => q.ModuleKey, q => q.FreeTierLimit);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ModuleQuota>> GetAllAsync()
    {
        return await _context.Set<ModuleQuota>()
            .AsNoTracking()
            .OrderBy(q => q.ModuleKey)
            .ToListAsync();
    }

    /// <inheritdoc />
    public async Task<ModuleQuota?> GetByKeyAsync(string moduleKey)
    {
        // Tracked: the admin update path mutates FreeTierLimit/audit columns and lets
        // UnitOfWork save. Served by UX_ModuleQuotas_ModuleKey.
        return await _context.Set<ModuleQuota>()
            .FirstOrDefaultAsync(q => q.ModuleKey == moduleKey);
    }
}
