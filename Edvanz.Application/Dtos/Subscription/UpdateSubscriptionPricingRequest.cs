using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Edvanz.Application.Dtos.Subscription;

/// <summary>
/// Input DTO for PUT /api/admin/subscriptions/pricing.
/// BR-SUB-009: changing the rate never alters in-flight pending payments — their amount
/// was snapshotted at initiation.
/// Unknown JSON fields are rejected (400) so a typo'd field never silently succeeds.
/// </summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public class UpdateSubscriptionPricingRequest
{
    /// <summary>The new per-student monthly rate in EGP. Must be greater than zero.</summary>
    [Required]
    public decimal PricePerStudentEGP { get; set; }
}
