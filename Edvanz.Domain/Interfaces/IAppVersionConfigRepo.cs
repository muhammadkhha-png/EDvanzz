using Edvanz.Domain.Entities;

namespace Edvanz.Domain.Interfaces;

/// <summary>
/// Access to the <see cref="AppVersionConfig"/> table — the runtime-editable per-platform mobile-app
/// version gate. An absent platform row means "fall back to <c>AppVersionOptions</c>", so callers must
/// tolerate a null/missing row (never insert a default here).
/// </summary>
public interface IAppVersionConfigRepo
{
    /// <summary>All platform rows, AsNoTracking, ordered by <c>Platform</c>. Feeds the admin read + the
    /// public effective-config lookup (0, 1, or 2 rows).</summary>
    Task<IReadOnlyList<AppVersionConfig>> GetAllAsync();

    /// <summary>AsNoTracking lookup of a single platform row (case-insensitive key), or null when the
    /// platform has never been saved (→ caller uses the options fallback).</summary>
    Task<AppVersionConfig?> GetByPlatformAsync(string platform);

    /// <summary>
    /// Insert-or-update the row for <paramref name="incoming"/>.Platform: if a row already exists it is
    /// mutated in place (tracked), otherwise the entity is added. Does NOT call SaveChanges — the caller
    /// owns the commit boundary (CLAUDE.md §5.2). Match is case-insensitive on <c>Platform</c>.
    /// </summary>
    Task UpsertAsync(AppVersionConfig incoming);
}
