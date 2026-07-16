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
    /// Tutor-selected channel. Manual (admin-approved) is the only live channel —
    /// the Paymob gateway path was removed 2026-07-17, so a request that still says
    /// Paymob gets the manual payload in the response (unchanged wire behavior:
    /// the disabled stub always fell through to manual).
    /// </summary>
    public PaymentChannel PaymentChannel { get; set; }
}