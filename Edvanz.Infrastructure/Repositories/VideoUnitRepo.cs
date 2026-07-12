using Edvanz.Domain.Entities;
using Edvanz.Domain.Interfaces;
using Edvanz.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Edvanz.Infrastructure.Repositories;

/// <summary>
/// Extended repository for <see cref="VideoUnit"/> (Track C / G-UNIT). Same
/// query-pattern conventions as <see cref="VideoAssetRepo"/> — paged
/// projections, tenant-scoped, aggregates computed in one round trip.
/// </summary>
public class VideoUnitRepo : GenericRepo<VideoUnit, long>, IVideoUnitRepo
{
    public VideoUnitRepo(EdvanzDbContext context) : base(context)
    {
    }

    /// <inheritdoc />
    public async Task AddUnitAsync(VideoUnit unit)
    {
        await _context.VideoUnits.AddAsync(unit);
    }

    /// <inheritdoc />
    public async Task<VideoUnit?> GetUnitByIdAndTeacherAsync(long unitId, long teacherId)
    {
        // Tracked: caller may mutate for update or soft-delete.
        return await _context.VideoUnits
            .FirstOrDefaultAsync(u => u.Id == unitId && u.TeacherId == teacherId);
    }

    /// <inheritdoc />
    public async Task SoftDeleteUnitAsync(VideoUnit unit, DateTime deletedAtUtc)
    {
        unit.DeletedAt = deletedAtUtc;
        _context.Entry(unit).State = EntityState.Modified;
        await Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task<int> DetachVideosFromUnitAsync(long unitId)
    {
        // Single SQL DELETE against the M:N join table — makes the unit's
        // videos loose immediately rather than waiting for a hard DB delete
        // (this is a soft-delete of the unit, so the FK's NoAction never fires).
        return await _context.VideoAssetUnits
            .Where(au => au.UnitId == unitId)
            .ExecuteDeleteAsync();
    }

    /// <inheritdoc />
    public async Task<(IReadOnlyList<TeacherVideoUnitListRow> Items, int TotalCount)>
        GetTeacherUnitsPagedAsync(long teacherId, string? search, int page, int pageSize)
    {
        var query = _context.VideoUnits
            .Where(u => u.TeacherId == teacherId);

        if (!string.IsNullOrWhiteSpace(search))
        {
            string pattern = $"%{search.Trim()}%";
            query = query.Where(u => EF.Functions.Like(u.Title, pattern));
        }

        int totalCount = await query.CountAsync();

        // Rolled-up child aggregates via correlated subqueries — one round
        // trip, no per-unit N+1. SeenStudentCount mirrors the definition used
        // by the top-level teacher video list (distinct students with any
        // VideoAnalytics row across the unit's videos).
        var rows = await query
            .OrderByDescending(u => u.CreateAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(u => new TeacherVideoUnitListRow
            {
                Id = u.Id,
                Title = u.Title,
                Description = u.Description,
                VideoCount = _context.VideoAssetUnits.Count(au => au.UnitId == u.Id),
                SeenStudentCount = _context.VideoAnalytics
                    .Count(a => _context.VideoAssetUnits
                        .Any(au => au.UnitId == u.Id && au.VideoAssetId == a.VideoAssetId)),
                UnseenStudentCount = _context.VideoScopes
                    .Count(s => _context.VideoAssetUnits.Any(au => au.UnitId == u.Id && au.VideoAssetId == s.VideoAssetId))
                    - _context.VideoAnalytics
                        .Count(a => _context.VideoAssetUnits
                            .Any(au => au.UnitId == u.Id && au.VideoAssetId == a.VideoAssetId)),
                CreatedAt = u.CreateAt,
            })
            .AsNoTracking()
            .ToListAsync();

        foreach (var row in rows)
        {
            row.UnseenStudentCount = Math.Max(0, row.UnseenStudentCount);
        }

        return (rows, totalCount);
    }

    /// <inheritdoc />
    public async Task<(IReadOnlyList<TeacherVideoListRow> Items, int TotalCount)>
        GetVideosInUnitPagedAsync(long unitId, long teacherId, int page, int pageSize)
    {
        var query = _context.VideoAssets
            .Where(v => v.TeacherId == teacherId
                && _context.VideoAssetUnits.Any(au => au.UnitId == unitId && au.VideoAssetId == v.Id));

        int totalCount = await query.CountAsync();

        var rows = await query
            .OrderByDescending(v => v.CreateAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(v => new TeacherVideoListRow
            {
                Id = v.Id,
                Title = v.Title,
                SourceType = v.SourceType,
                DurationSeconds = v.DurationSeconds,
                StudentsInScope = _context.VideoScopes.Count(s => s.VideoAssetId == v.Id),
                TotalOpens = _context.VideoAnalytics
                    .Where(a => a.VideoAssetId == v.Id)
                    .Sum(a => (int?)a.OpenCount) ?? 0,
                SeenStudentCount = _context.VideoAnalytics.Count(a => a.VideoAssetId == v.Id),
                CreatedAt = v.CreateAt,
            })
            .AsNoTracking()
            .ToListAsync();

        foreach (var row in rows)
        {
            row.UnseenStudentCount = Math.Max(0, row.StudentsInScope - row.SeenStudentCount);
        }

        return (rows, totalCount);
    }

    // ══════════════════════════════════════════════════════════════════════
    // UNIT SCOPE — collection-level Target Scope (final decision)
    // ══════════════════════════════════════════════════════════════════════

    /// <inheritdoc />
    public async Task<VideoUnit?> GetUnitWithScopesAsync(long unitId, long teacherId)
    {
        return await _context.VideoUnits
            .Include(u => u.Scopes)
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == unitId && u.TeacherId == teacherId);
    }

    /// <inheritdoc />
    public async Task AddUnitScopesAsync(IEnumerable<VideoUnitScope> scopes)
    {
        await _context.VideoUnitScopes.AddRangeAsync(scopes);
    }

    /// <inheritdoc />
    public async Task DeleteAllScopesForUnitAsync(long unitId)
    {
        await _context.VideoUnitScopes
            .Where(s => s.VideoUnitId == unitId)
            .ExecuteDeleteAsync();
    }

    /// <inheritdoc />
    public async Task<bool> DeleteUnitScopeByIdAndTeacherAsync(long scopeId, long teacherId)
    {
        var rowsAffected = await _context.VideoUnitScopes
            .Where(s => s.Id == scopeId && s.TeacherId == teacherId)
            .ExecuteDeleteAsync();

        return rowsAffected > 0;
    }

    /// <inheritdoc />
    public async Task<int> CountScopesForUnitAsync(long unitId)
    {
        return await _context.VideoUnitScopes
            .CountAsync(s => s.VideoUnitId == unitId);
    }
}
