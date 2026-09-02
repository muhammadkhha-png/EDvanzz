using Edvanz.Domain.Entities;
using Edvanz.Domain.Interfaces;
using Edvanz.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Edvanz.Infrastructure.Repositories;

/// <summary>
/// Reads/writes the AppVersionConfig table (per-platform mobile update gate). Thin, query-only repo
/// consistent with the UnitOfWork's parameterless-construction pattern; commits are owned by the caller.
/// </summary>
public class AppVersionConfigRepo : IAppVersionConfigRepo
{
    private readonly EdvanzDbContext _context;

    public AppVersionConfigRepo(EdvanzDbContext context)
    {
        _context = context;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<AppVersionConfig>> GetAllAsync()
    {
        return await _context.Set<AppVersionConfig>()
            .AsNoTracking()
            .OrderBy(c => c.Platform)
            .ToListAsync();
    }

    /// <inheritdoc />
    public async Task<AppVersionConfig?> GetByPlatformAsync(string platform)
    {
        var key = (platform ?? string.Empty).Trim();
        // Served by UX_AppVersionConfigs_Platform. EF.Functions comparison stays case-insensitive under
        // the column's default (CI) collation, matching the "android"/"ios" keys.
        return await _context.Set<AppVersionConfig>()
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Platform == key);
    }

    /// <inheritdoc />
    public async Task UpsertAsync(AppVersionConfig incoming)
    {
        var key = (incoming.Platform ?? string.Empty).Trim();

        // Tracked lookup (NOT the AsNoTracking read above) so a mutate flows to the DB on the caller's save.
        var existing = await _context.Set<AppVersionConfig>()
            .FirstOrDefaultAsync(c => c.Platform == key);

        if (existing is null)
        {
            incoming.Platform = key;
            await _context.Set<AppVersionConfig>().AddAsync(incoming);
            return;
        }

        existing.MinSupportedBuild = incoming.MinSupportedBuild;
        existing.LatestBuild = incoming.LatestBuild;
        existing.LatestVersion = incoming.LatestVersion;
        existing.StoreUrl = incoming.StoreUrl;
        existing.UpdatedAt = incoming.UpdatedAt;
        existing.UpdatedByUserId = incoming.UpdatedByUserId;
    }
}
