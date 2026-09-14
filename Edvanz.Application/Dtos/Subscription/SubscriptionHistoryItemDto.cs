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
    /// What the teacher paid for this period, in EGP — or <c>null</c> when NO AMOUNT WAS EVER
    /// RECORDED, which is the case for every super-admin activation that did not state one.
    ///
    /// Nullable on purpose. The stored column is <c>decimal NOT NULL</c> and a real payment is
    /// never zero, so a stored 0 means "nobody wrote a figure here" — and rendering that as
    /// "0 EGP" tells a teacher their subscription was free, or (when the activation briefly
    /// computed a price) tells them they paid a sum nobody agreed. A null cannot be formatted
    /// into a price by accident; a zero can. Same contract as
    /// <see cref="AdminExtendRequest.AmountPaidEGP"/>: null is "not stated", not zero.
    ///
    /// The JSON key is always present (it serializes as <c>null</c>, not omitted), so a client
    /// that reads it still finds it; only its type widened from number to number-or-null.
    /// </summary>
    public decimal? AmountPaidEGP { get; set; }

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