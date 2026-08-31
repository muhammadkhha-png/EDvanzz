using Edvanz.Domain.Entities.ShareProp;

namespace Edvanz.Domain.Entities;

/// <summary>
/// One tier of a center's DEFAULT prorated-payment template — mirrors <see cref="TeacherProratedTier"/>
/// (up to 3 tiers, each a day-range with a fraction rate). Belongs to a <see cref="CenterConfiguration"/>.
///
/// FK behavior and the decimal precision are configured ENTIRELY in Fluent API (EdvanzDbContext) — NO
/// [ForeignKey]/[Column] annotations here (BUG-4 / Center Fluent-only precedent).
/// </summary>
public class CenterProratedTier : BaseEntity
{
    /// <summary>Foreign key to the parent <see cref="CenterConfiguration"/>.</summary>
    public long CenterConfigurationId { get; set; }
    public CenterConfiguration CenterConfiguration { get; set; } = null!;

    /// <summary>Tier ordinal (1, 2, or 3). Determines evaluation order.</summary>
    public int TierNumber { get; set; }

    /// <summary>First day of the month this tier applies to (inclusive).</summary>
    public int ThresholdDayStart { get; set; }

    /// <summary>Last day of the month this tier applies to (inclusive).</summary>
    public int ThresholdDayEnd { get; set; }

    /// <summary>Fraction of the full amount to charge for this tier (1.0 = full). decimal(5,4) via Fluent.</summary>
    public decimal FractionRate { get; set; }
}
