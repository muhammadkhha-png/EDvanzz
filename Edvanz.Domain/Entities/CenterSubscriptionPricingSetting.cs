using Edvanz.Domain.Entities.ShareProp;

namespace Edvanz.Domain.Entities;

/// <summary>
/// Single-row settings table holding the per-slot monthly rates that price a center subscription.
/// A center subscription's fee = FullTeacherSlots × <see cref="FullTeacherSlotPriceEGP"/> +
/// ManagerialTeacherSlots × <see cref="ManagerialTeacherSlotPriceEGP"/>, computed at request/
/// activation time and snapshotted onto <see cref="CenterSubscriptionRequest.ComputedAmountEGP"/> /
/// <see cref="CenterSubscription.AmountPaidEGP"/> — later rate changes never alter an existing row.
///
/// Seeded with Id = 1 via HasData; edited only through the admin center-pricing endpoint. Mirrors
/// <see cref="SubscriptionPricingSetting"/>. FK behavior is configured entirely in Fluent API (BUG-4).
/// </summary>
public class CenterSubscriptionPricingSetting : BaseEntity
{
    /// <summary>Monthly price per FULL teacher slot, in EGP (decimal(10,2) via Fluent). Must be &gt; 0.</summary>
    public decimal FullTeacherSlotPriceEGP { get; set; }

    /// <summary>Monthly price per MANAGERIAL teacher slot, in EGP (decimal(10,2) via Fluent). Must be &gt; 0.</summary>
    public decimal ManagerialTeacherSlotPriceEGP { get; set; }

    /// <summary>Monthly price per MANAGERIAL + PARENTS (ManagerialPlus) teacher slot, in EGP (decimal(10,2) via Fluent). Must be &gt; 0.</summary>
    public decimal ManagerialPlusTeacherSlotPriceEGP { get; set; }

    /// <summary>When the rates were last changed via the admin endpoint (UTC). Null = never edited since seeding.</summary>
    public DateTime? UpdatedAt { get; set; }

    /// <summary>The super admin who last changed the rates.</summary>
    public long? UpdatedByUserId { get; set; }
    public User? UpdatedByUser { get; set; }
}
