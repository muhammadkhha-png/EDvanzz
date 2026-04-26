namespace Edvanz.Application.Dtos.Subscription;

/// <summary>
/// Discriminated-union response for POST /api/subscription/renew/initiate (Critique M-2).
/// Exactly one of <see cref="Paymob"/> or <see cref="Manual"/> is non-null,
/// determined by <see cref="Mode"/>.
/// </summary>
public class RenewInitiateResponse
{
    /// <summary>
    /// "paymob" or "manual". Frontend switches on this value.
    /// </summary>
    public string Mode { get; set; } = null!;

    /// <summary>
    /// Populated when Mode = "paymob". Null otherwise.
    /// </summary>
    public PaymobInitiatePayload? Paymob { get; set; }

    /// <summary>
    /// Populated when Mode = "manual". Null otherwise.
    /// </summary>
    public ManualInitiatePayload? Manual { get; set; }
}

/// <summary>
/// Payload returned when a Paymob session was successfully created.
/// </summary>
public class PaymobInitiatePayload
{
    public long PendingPaymentId { get; set; }

    /// <summary>
    /// Hosted iframe URL the Flutter app loads in a webview.
    /// </summary>
    public string IframeUrl { get; set; } = null!;

    /// <summary>
    /// Provider session id; correlated with the webhook callback.
    /// </summary>
    public string SessionId { get; set; } = null!;
}

/// <summary>
/// Payload returned when the manual flow is selected (or when Paymob is stubbed off in v1).
/// </summary>
public class ManualInitiatePayload
{
    public long PendingPaymentId { get; set; }

    /// <summary>
    /// External account/phone number the tutor pays to (Vodafone Cash wallet or InstaPay handle).
    /// </summary>
    public string PayToNumber { get; set; } = null!;

    /// <summary>
    /// Localized step-by-step instructions for completing the external payment.
    /// </summary>
    public string Instructions { get; set; } = null!;

    /// <summary>
    /// Amount the tutor must pay, in EGP — captured at initiation per BR-SUB-009.
    /// </summary>
    public decimal AmountEGP { get; set; }
}