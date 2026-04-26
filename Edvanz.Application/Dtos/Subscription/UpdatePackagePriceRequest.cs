namespace Edvanz.Application.Dtos.Subscription;

/// <summary>
/// Body DTO for PUT /api/admin/subscriptions/packages/{id}/price.
/// </summary>
public class UpdatePackagePriceRequest
{
    /// <summary>
    /// New monthly price in EGP. Must be ≥ 0; service-side validated.
    /// </summary>
    public decimal NewMonthlyPriceEGP { get; set; }
}