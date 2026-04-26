using Edvanz.Domain.Enums;

namespace Edvanz.Application.Dtos.Subscription;

/// <summary>
/// One row of the paginated payment history (REQ-SUB-022 / FR-SUB-039).
/// Phone number and reference are masked for tutor display per BR-SUB-011.
/// </summary>
public class SubscriptionHistoryItemDto
{
    public long Id { get; set; }

    /// <summary>
    /// Subscription period start (UTC).
    /// </summary>
    public DateTime StartDate { get; set; }

    /// <summary>
    /// Subscription period end (UTC).
    /// </summary>
    public DateTime EndDate { get; set; }

    /// <summary>
    /// Amount paid in EGP. Zero for super-admin manual activations.
    /// </summary>
    public decimal AmountPaidEGP { get; set; }

    public PaymentMethod PaymentMethod { get; set; }

    public PaymentChannel PaymentChannel { get; set; }

    /// <summary>
    /// Masked transaction reference (e.g. "PMB****1234"). Null when no reference exists
    /// (super-admin override). The unmasked value lives encrypted in EncryptedPaymentDetails
    /// and is never exposed through this DTO.
    /// </summary>
    public string? MaskedTransactionReference { get; set; }

    /// <summary>
    /// UTC timestamp of payment confirmation (the single value captured at §6.3 step 1).
    /// </summary>
    public DateTime PaymentConfirmedAt { get; set; }
}