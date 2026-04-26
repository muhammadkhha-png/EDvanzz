using Edvanz.Domain.Entities;
using Edvanz.Domain.Enums;
using Edvanz.Domain.Interfaces;
using Edvanz.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Edvanz.Infrastructure.Repositories;

/// <summary>
/// EF Core implementation of ISubscriptionPaymentRepo.
/// Inherits GenericRepo&lt;PendingSubscriptionPayment, long&gt; for basic CRUD;
/// adds named methods for every domain-specific query.
/// </summary>
public class SubscriptionPaymentRepo
    : GenericRepo<PendingSubscriptionPayment, long>, ISubscriptionPaymentRepo
{
    public SubscriptionPaymentRepo(EdvanzDbContext context) : base(context)
    {
    }

    // ══════════════════════════════════════════════
    // SINGLE-ROW LOOKUPS
    // ══════════════════════════════════════════════

    /// <inheritdoc />
    public async Task<PendingSubscriptionPayment?> GetByIdAndTeacherAsync(
        long pendingPaymentId, long teacherId)
    {
        // Tracked: caller may transition Status / persist resolution metadata.
        return await _context.Set<PendingSubscriptionPayment>()
            .FirstOrDefaultAsync(p => p.Id == pendingPaymentId && p.TeacherId == teacherId);
    }

    /// <inheritdoc />
    public async Task<PendingSubscriptionPayment?> GetByIdForAdminAsync(long pendingPaymentId)
    {
        // Tracked: admin approve / reject flips Status and writes ResolvedByUserId.
        return await _context.Set<PendingSubscriptionPayment>()
            .FirstOrDefaultAsync(p => p.Id == pendingPaymentId);
    }

    /// <inheritdoc />
    public async Task<PendingSubscriptionPayment?> GetByPaymobSessionIdAsync(string paymobSessionId)
    {
        // Tracked: webhook handler updates Status on confirm/reject.
        // Indexed by IX_PendingSubscriptionPayments_PaymobSessionId.
        return await _context.Set<PendingSubscriptionPayment>()
            .FirstOrDefaultAsync(p => p.PaymobSessionId == paymobSessionId);
    }

    // ══════════════════════════════════════════════
    // LIST QUERIES
    // ══════════════════════════════════════════════

    /// <inheritdoc />
    public async Task<(IReadOnlyList<PendingSubscriptionPayment> Items, int TotalCount)>
        GetAdminQueuePagedAsync(int page, int pageSize)
    {
        IQueryable<PendingSubscriptionPayment> query = _context.Set<PendingSubscriptionPayment>()
            .AsNoTracking()
            .Where(p => p.Status == PendingPaymentStatus.AwaitingSuperAdminApproval)
            .OrderBy(p => p.InitiatedAt);

        int totalCount = await query.CountAsync();

        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items, totalCount);
    }

    /// <inheritdoc />
    public async Task<bool> HasInFlightPaymentAsync(long teacherId)
    {
        return await _context.Set<PendingSubscriptionPayment>()
            .AnyAsync(p => p.TeacherId == teacherId
                        && (p.Status == PendingPaymentStatus.Initiated
                         || p.Status == PendingPaymentStatus.AwaitingSuperAdminApproval));
    }

    // ══════════════════════════════════════════════
    // BULK OPERATIONS
    // ══════════════════════════════════════════════

    /// <inheritdoc />
    public async Task<int> ExpireStalePendingPaymentsAsync(DateTime cutoffUtc)
    {
        // ExecuteUpdateAsync emits a single SQL UPDATE — no entities loaded.
        // Idempotent: a second run on the same data is a no-op (Status filter excludes already-Expired).
        return await _context.Set<PendingSubscriptionPayment>()
            .Where(p => p.Status == PendingPaymentStatus.Initiated
                     && p.InitiatedAt < cutoffUtc)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(p => p.Status, PendingPaymentStatus.Expired)
                .SetProperty(p => p.ResolvedAt, DateTime.UtcNow));
    }
    /// <inheritdoc />
    public void UpdatePending(PendingSubscriptionPayment pending)
    {
        _context.Entry(pending).State = EntityState.Modified;
    }

    /// <inheritdoc />
    public void DetachPending(PendingSubscriptionPayment pending)
    {
        _context.Entry(pending).State = EntityState.Detached;
    }
}