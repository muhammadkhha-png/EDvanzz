using Edvanz.Domain.Entities.ShareProp;
using System.ComponentModel.DataAnnotations.Schema;

namespace Edvanz.Domain.Entities;

/// <summary>
/// Represents one tier in a teacher's prorated payment configuration.
/// AAM-FR-04.4: Up to 3 tiers, each with a day threshold range and a fraction/deduction rate.
/// REQ-PAY-021/023: Default tiers are days 1-10 (100%), 11-20 (66.67%), 21-31 (33.33%).
/// Tiers are only active when TeacherConfiguration.IsProratedPaymentEnabled is true.
/// </summary>
public class TeacherProratedTier : BaseEntity
{
    /// <summary>
    /// Foreign key to the parent TeacherConfiguration.
    /// </summary>
    [ForeignKey(nameof(TeacherConfiguration))]
    public long TeacherConfigurationId { get; set; }
    public TeacherConfiguration TeacherConfiguration { get; set; } = null!;

    /// <summary>
    /// Tier ordinal (1, 2, or 3). Determines evaluation order.
    /// </summary>
    public int TierNumber { get; set; }

    /// <summary>
    /// First day of the month this tier applies to (inclusive).
    /// REQ-PAY-021 defaults: Tier 1 = 1, Tier 2 = 11, Tier 3 = 21.
    /// </summary>
    public int ThresholdDayStart { get; set; }

    /// <summary>
    /// Last day of the month this tier applies to (inclusive).
    /// REQ-PAY-021 defaults: Tier 1 = 10, Tier 2 = 20, Tier 3 = 31.
    /// </summary>
    public int ThresholdDayEnd { get; set; }

    /// <summary>
    /// Fraction of the full payment amount to charge for this tier.
    /// Stored as a decimal: 1.0 = full, 0.6667 = two-thirds, 0.3333 = one-third.
    /// REQ-PAY-023: Fully configurable by the teacher.
    /// </summary>
    [Column(TypeName = "decimal(5,4)")]
    public decimal FractionRate { get; set; }
}