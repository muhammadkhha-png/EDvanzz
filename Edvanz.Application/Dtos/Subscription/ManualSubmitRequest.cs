namespace Edvanz.Application.Dtos.Subscription;

/// <summary>
/// Input DTO for POST /api/subscription/renew/manual-submit (FR-SUB-033).
/// Submitted details are encrypted via IEncryptionService before persistence (REQ-SUB-NFR-004).
/// </summary>
public class ManualSubmitRequest
{
    /// <summary>
    /// The PendingSubscriptionPayment row created by the prior /initiate call.
    /// Must be in Status = Initiated and owned by the calling teacher (tenant guard).
    /// </summary>
    public long PendingPaymentId { get; set; }

    /// <summary>
    /// The phone number from which the tutor sent the payment (Vodafone Cash sender / InstaPay handle).
    /// Stored encrypted; masked in payment history per BR-SUB-011.
    /// </summary>
    public string PaymentPhoneNumber { get; set; } = null!;

    /// <summary>
    /// The external transaction reference returned by the provider.
    /// Required so super admin can verify against the provider dashboard before approving.
    /// </summary>
    public string TransactionReference { get; set; } = null!;
}