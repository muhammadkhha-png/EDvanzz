using Edvanz.Domain.Entities.ShareProp;

namespace Edvanz.Domain.Entities;

/// <summary>
/// Single-row settings table holding the per-student monthly subscription rate.
/// The renewal price is Teacher.StudentCapacity × <see cref="PricePerStudentEGP"/>
/// ("1 student = 2.5 EGP/month"), computed at renewal INITIATION and snapshotted onto
/// PendingSubscriptionPayment.AmountEGP (BR-SUB-009) — later rate/capacity changes never
/// alter an in-flight payment.
///
/// Seeded with Id = 1 @ 2.50 EGP via HasData; edited only through
/// PUT /api/admin/subscriptions/pricing. FK behavior is configured entirely in Fluent
/// API (BUG-4 rule).
/// </summary>
public class SubscriptionPricingSetting : BaseEntity
{
    /// <summary>Monthly price per student, in EGP (decimal(10,2) via Fluent). Must be &gt; 0 to allow renewals.</summary>
    public decimal PricePerStudentEGP { get; set; }

    /// <summary>
    /// Flat monthly price for a MANAGERIAL subscription, in EGP (decimal(10,2) via Fluent). A
    /// managerial plan has no per-student component, so its fee is this single value regardless
    /// of roster size. Seeded @ 500.00; edited through the admin pricing endpoint. Must be &gt; 0.
    /// </summary>
    public decimal ManagerialMonthlyPriceEGP { get; set; }

    /// <summary>
    /// Flat monthly price for a MANAGERIAL + PARENTS (<see cref="Enums.SubscriptionPlanType.ManagerialPlus"/>)
    /// subscription, in EGP (decimal(10,2) via Fluent). Like Managerial it has no per-student
    /// component. Seeded @ 650.00; edited through the admin pricing endpoint. Must be &gt; 0.
    /// </summary>
    public decimal ManagerialPlusMonthlyPriceEGP { get; set; }

    /// <summary>When the rate was last changed via the admin endpoint (UTC). Null = never edited since seeding.</summary>
    public DateTime? UpdatedAt { get; set; }

    /// <summary>The super admin who last changed the rate.</summary>
    public long? UpdatedByUserId { get; set; }

    public User? UpdatedByUser { get; set; }
}
