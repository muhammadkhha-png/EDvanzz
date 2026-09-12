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

    /// <summary>
    /// Hot-path read (AsNoTracking) of ALL THREE monthly rates in one round trip — what
    /// <c>SubscriptionPricing.MonthlyValueEGP</c> needs to price any plan. Pricing a plan used to
    /// take a different read per branch, so the caller had to know which plan it was holding
    /// before it could ask for a price; the admin console, which prices every active subscription
    /// at once, would have issued one settings read per teacher. Zeros when the seed row is
    /// missing, which the pricing helper renders as "cannot be priced".
    /// </summary>
    Task<SubscriptionRates> GetRatesAsync();
}

/// <summary>The three monthly rates a subscription can be priced from.</summary>
/// <param name="PerStudentEGP">Full plan: charged per linked student-app account.</param>
/// <param name="ManagerialMonthlyEGP">Managerial plan: flat.</param>
/// <param name="ManagerialPlusMonthlyEGP">Managerial + Parents plan: flat.</param>
public sealed record SubscriptionRates(
    decimal PerStudentEGP,
    decimal ManagerialMonthlyEGP,
    decimal ManagerialPlusMonthlyEGP);
