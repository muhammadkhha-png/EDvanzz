using Edvanz.Domain.Enums;

namespace Edvanz.Application.Dtos.Subscription;

/// <summary>
/// Input DTO for POST /api/subscription/renew/initiate (FR-SUB-040).
/// </summary>
public class RenewInitiateRequest
{
    /// <summary>
    /// Tutor-selected method: VodafoneCash or InstaPay.
    /// SuperAdminManual is rejected at the service boundary.
    /// </summary>
    public PaymentMethod PaymentMethod { get; set; }

    /// <summary>
    /// Tutor-selected channel: Paymob (auto-confirm) or Manual (admin-approved).
    /// In v1 with PaymobOptions.Enabled = false, the service flips a Paymob request
    /// to Manual mode in the response (D-04 / FR-SUB-034).
    /// </summary>
    public PaymentChannel PaymentChannel { get; set; }
}