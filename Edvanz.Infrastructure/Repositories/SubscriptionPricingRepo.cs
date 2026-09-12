using Edvanz.Domain.Entities;
using Edvanz.Domain.Interfaces;
using Edvanz.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Edvanz.Infrastructure.Repositories;

/// <summary>
/// EF Core implementation of ISubscriptionPricingRepo — thin, query-only access to the
/// single-row SubscriptionPricingSettings table (same construction pattern as
/// ModuleQuotaRepo). SaveChanges is never called here; the caller owns the commit.
/// </summary>
public class SubscriptionPricingRepo : ISubscriptionPricingRepo
{
    private readonly EdvanzDbContext _context;

    public SubscriptionPricingRepo(EdvanzDbContext context)
    {
        _context = context;
    }

    /// <inheritdoc />
    public async Task<decimal?> GetPricePerStudentAsync()
    {
        // Single seeded row (Id = 1); OrderBy keeps the read deterministic if the table
        // ever grows. Null when the seed migration has not been applied — callers fail closed.
        return await _context.Set<SubscriptionPricingSetting>()
            .AsNoTracking()
            .OrderBy(s => s.Id)
            .Select(s => (decimal?)s.PricePerStudentEGP)
            .FirstOrDefaultAsync();
    }

    /// <inheritdoc />
    public async Task<SubscriptionPricingSetting?> GetSettingAsync()
    {
        // Tracked: the admin update path mutates the row and lets UnitOfWork save.
        return await _context.Set<SubscriptionPricingSetting>()
            .OrderBy(s => s.Id)
            .FirstOrDefaultAsync();
    }

    /// <inheritdoc />
    public async Task<SubscriptionRates> GetRatesAsync()
    {
        var rates = await _context.Set<SubscriptionPricingSetting>()
            .AsNoTracking()
            .OrderBy(s => s.Id)
            .Select(s => new SubscriptionRates(
                s.PricePerStudentEGP, s.ManagerialMonthlyPriceEGP, s.ManagerialPlusMonthlyPriceEGP))
            .FirstOrDefaultAsync();

        // Zeros rather than null: the pricing helper already treats a zero rate as "cannot be
        // priced", so every caller gets the same fail-closed answer without a null check.
        return rates ?? new SubscriptionRates(0m, 0m, 0m);
    }
}
