using Edvanz.Domain.Entities;
using Edvanz.Domain.Enums;
using Edvanz.Domain.Interfaces;
using Edvanz.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Edvanz.Infrastructure.Repositories;

/// <summary>
/// EF Core implementation of <see cref="ISubscriptionRequestRepo"/>.
/// Inherits GenericRepo&lt;SubscriptionRequest, long&gt; for basic CRUD; adds named methods for
/// every domain-specific query (CapacityIncreaseRequestRepo pattern).
/// </summary>
public class SubscriptionRequestRepo
    : GenericRepo<SubscriptionRequest, long>, ISubscriptionRequestRepo
{
    public SubscriptionRequestRepo(EdvanzDbContext context) : base(context)
    {
    }

    /// <inheritdoc />
    public async Task<SubscriptionRequest?> GetByIdAndTeacherAsync(long requestId, long teacherId)
    {
        // Tracked: the teacher-side cancel flow transitions Status.
        return await _context.Set<SubscriptionRequest>()
            .FirstOrDefaultAsync(r => r.Id == requestId && r.TeacherId == teacherId);
    }

    /// <inheritdoc />
    public async Task<SubscriptionRequest?> GetByIdForAdminAsync(long requestId)
    {
        // Tracked: admin approve / reject flips Status and writes ResolvedByUserId.
        return await _context.Set<SubscriptionRequest>()
            .FirstOrDefaultAsync(r => r.Id == requestId);
    }

    /// <inheritdoc />
    public async Task<bool> HasPendingRequestAsync(long teacherId)
    {
        return await _context.Set<SubscriptionRequest>()
            .AnyAsync(r => r.TeacherId == teacherId
                        && r.Status == SubscriptionRequestStatus.Pending);
    }

    /// <inheritdoc />
    public async Task<(IReadOnlyList<SubscriptionRequest> Items, int TotalCount)>
        GetAdminQueuePagedAsync(int page, int pageSize)
    {
        // FIFO (oldest first) — served in submission order, like the capacity/pending queues.
        IQueryable<SubscriptionRequest> query = _context.Set<SubscriptionRequest>()
            .AsNoTracking()
            .Where(r => r.Status == SubscriptionRequestStatus.Pending)
            .OrderBy(r => r.RequestedAt);

        int totalCount = await query.CountAsync();

        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items, totalCount);
    }

    /// <inheritdoc />
    public async Task<(IReadOnlyList<SubscriptionRequest> Items, int TotalCount)>
        GetByTeacherPagedAsync(long teacherId, int page, int pageSize)
    {
        IQueryable<SubscriptionRequest> query = _context.Set<SubscriptionRequest>()
            .AsNoTracking()
            .Where(r => r.TeacherId == teacherId)
            .OrderByDescending(r => r.RequestedAt);

        int totalCount = await query.CountAsync();

        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items, totalCount);
    }

    /// <inheritdoc />
    public void UpdateRequest(SubscriptionRequest request)
    {
        _context.Entry(request).State = EntityState.Modified;
    }
}
