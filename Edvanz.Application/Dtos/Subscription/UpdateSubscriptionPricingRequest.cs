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

    /// <summary>
    /// New flat monthly price for the Managerial plan. NULLABLE for wire-compat: older admin
    /// clients that send only the per-student rate must not silently zero the flat prices, so
    /// an omitted value means "leave unchanged". When sent, must be greater than zero.
    /// </summary>
    public decimal? ManagerialMonthlyPriceEGP { get; set; }

    /// <summary>
    /// New flat monthly price for the Managerial + Parents (ManagerialPlus) plan. Same
    /// omitted-means-unchanged contract as <see cref="ManagerialMonthlyPriceEGP"/>.
    /// </summary>
    public decimal? ManagerialPlusMonthlyPriceEGP { get; set; }
}
