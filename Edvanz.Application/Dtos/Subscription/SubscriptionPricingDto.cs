namespace Edvanz.Application.Dtos.Subscription;

/// <summary>
/// Output DTO for GET/PUT /api/admin/subscriptions/pricing — the per-student monthly
/// rate driving renewal pricing (Teacher.StudentCapacity × rate).
/// </summary>
public class SubscriptionPricingDto
{
    /// <summary>Monthly price per student, in EGP.</summary>
    public decimal PricePerStudentEGP { get; set; }

    /// <summary>When the rate was last changed; null if never edited since seeding.</summary>
    public DateTime? UpdatedAt { get; set; }

    /// <summary>User id of the super admin who last changed the rate; null if never edited.</summary>
    public long? UpdatedByUserId { get; set; }
}
