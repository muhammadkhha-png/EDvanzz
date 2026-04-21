using Edvanz.Domain.Entities.ShareProp;
using System.ComponentModel.DataAnnotations.Schema;

namespace Edvanz.Domain.Entities;

/// <summary>
/// Defines the student capacity tiers available during teacher configuration.
/// AAM-FR-04.1: 7 predefined tiers from "Up to 300" through "3000+".
/// Super-admin-managed lookup table — tiers can be adjusted without code deployment.
/// </summary>
public class StudentCapacityPackage : BaseEntity
{
    /// <summary>
    /// Display name shown to the teacher (e.g., "Up to 300", "300 to 500", "3000+").
    /// </summary>
    public string Name { get; set; } = null!;

    /// <summary>
    /// Lower bound of the tier (inclusive). Zero for the first tier.
    /// </summary>
    public int MinStudents { get; set; }

    /// <summary>
    /// Upper bound of the tier (inclusive). Null represents unlimited (the "3000+" tier).
    /// </summary>
    public int? MaxStudents { get; set; }

    /// <summary>
    /// Soft-delete flag. Inactive packages are hidden from selection but preserved for existing references.
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Controls the display order in the package selection UI. Lower values appear first.
    /// </summary>
    public int DisplayOrder { get; set; }

    // Navigation
    public ICollection<Teacher> Teachers { get; set; } = new List<Teacher>();
    // ──────────────────────────────────────────────────────────────
    // SUBSCRIPTION PRICING (added for Subscription Management Module)
    // ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Price of a 30-day subscription period for teachers assigned to this package, in EGP.
    /// REQ-SUB-016: Displayed on the renewal screen.
    /// BR-SUB-009: Snapshotted into PendingSubscriptionPayment.AmountEGP at renewal initiation.
    /// </summary>
    [Column(TypeName = "decimal(10,2)")]
    public decimal MonthlyPriceEGP { get; set; }

    /// <summary>
    /// UTC timestamp of the last price change on this package.
    /// </summary>
    public DateTime? PriceUpdatedAt { get; set; }

    /// <summary>
    /// Foreign key to the User (super admin) who last updated the price.
    /// Null until a price is explicitly set by super admin (post-seed).
    /// </summary>
    [ForeignKey(nameof(PriceUpdatedByUser))]
    public long? PriceUpdatedByUserId { get; set; }

    public User? PriceUpdatedByUser { get; set; }
}