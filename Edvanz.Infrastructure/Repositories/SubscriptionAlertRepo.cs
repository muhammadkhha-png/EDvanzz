using Edvanz.Domain.Entities;
using Edvanz.Domain.Interfaces;
using Edvanz.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Edvanz.Infrastructure.Repositories;

/// <summary>
/// EF Core implementation of ISubscriptionAlertRepo.
/// Inherits GenericRepo&lt;SubscriptionAlert, long&gt; for basic CRUD.
/// </summary>
public class SubscriptionAlertRepo
    : GenericRepo<SubscriptionAlert, long>, ISubscriptionAlertRepo
{
    public SubscriptionAlertRepo(EdvanzDbContext context) : base(context)
    {
    }

    /// <inheritdoc />
    public async Task<bool> HasAlertBeenSentAsync(
        long teacherId, DateTime subscriptionEndDate, int alertDay)
    {
        // SubscriptionEndDate column is stored as `date` (no time component) — the
        // worker passes EndDate.Date, so we compare on the date portion directly.
        DateTime endDateOnly = subscriptionEndDate.Date;

        return await _context.Set<SubscriptionAlert>()
            .AsNoTracking()
            .AnyAsync(a => a.TeacherId == teacherId
                        && a.SubscriptionEndDate == endDateOnly
                        && a.AlertDay == alertDay);
    }

    /// <inheritdoc />
    public async Task InsertAlertAsync(SubscriptionAlert alert)
    {
        await _context.Set<SubscriptionAlert>().AddAsync(alert);
    }
}