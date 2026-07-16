using Edvanz.Domain.Entities;

namespace Edvanz.Domain.Interfaces;

/// <summary>
/// Access to the single-row SubscriptionPricingSettings table — the per-student monthly
/// rate that drives renewal pricing (Teacher.StudentCapacity × rate, BR-SUB-009 snapshot
/// semantics). All query logic is encapsulated here; the Application layer never builds
/// raw expression predicates.
/// </summary>
public interface ISubscriptionPricingRepo
{
    /// <summary>
    /// Hot-path read of the per-student rate (AsNoTracking). Null when the settings row
    /// is missing (e.g., seed migration not yet applied) — callers must fail closed with
    /// PerStudentRateNotConfigured rather than assume a default.
    /// </summary>
    Task<decimal?> GetPricePerStudentAsync();

    /// <summary>
    /// Tracked load of the settings row for the admin update path. Null when the seed
    /// row is missing (defensive — the row is HasData-seeded with Id = 1).
    /// </summary>
    Task<SubscriptionPricingSetting?> GetSettingAsync();
}
