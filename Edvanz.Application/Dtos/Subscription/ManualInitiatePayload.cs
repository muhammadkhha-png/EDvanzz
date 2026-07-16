namespace Edvanz.Application.Dtos.Subscription;

/// <summary>
/// Discriminated-union response for POST /api/subscription/renew/initiate (Critique M-2).
/// The Mode/Paymob/Manual shape is a shipped wire contract and is KEPT — but since the
/// Paymob gateway removal (2026-07-17) Mode is always "manual" and <see cref="Paymob"/>
/// is always null.
/// </summary>
public class RenewInitiateResponse
{
    /// <summary>
    /// Always "manual" (historically also "paymob"). Frontend switches on this value.
    /// </summary>
    public string Mode { get; set; } = null!;

    /// <summary>
    /// Always null since the Paymob gateway removal — retained for wire-compat.
    /// </summary>
    public PaymobInitiatePayload? Paymob { get; set; }

    /// <summary>
    /// Populated when Mode = "manual". Null otherwise.
    /// </summary>
    public ManualInitiatePayload? Manual { get; set; }
}

/// <summary>
/// Legacy payload shape from the removed Paymob path — never populated; kept only so
/// the RenewInitiateResponse wire contract is unchanged.
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