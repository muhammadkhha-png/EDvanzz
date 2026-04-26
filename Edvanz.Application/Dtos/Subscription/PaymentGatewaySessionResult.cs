namespace Edvanz.Application.Dtos.Subscription;

/// <summary>
/// Result of IPaymentGateway.CreateSessionAsync (§6.4 step 3).
/// </summary>
public class PaymentGatewaySessionResult
{
    /// <summary>
    /// True when the gateway successfully created a hosted session.
    /// False for the v1 stub adapter (PaymobOptions.Enabled = false) — caller flips to manual mode.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Hosted iframe URL when Success = true. Null otherwise.
    /// </summary>
    public string? IframeUrl { get; set; }

    /// <summary>
    /// Provider session id when Success = true. Null otherwise.
    /// Stored on PendingSubscriptionPayment.PaymobSessionId for webhook correlation.
    /// </summary>
    public string? SessionId { get; set; }

    /// <summary>
    /// Diagnostic error code when Success = false (e.g. "not-configured"). Null on success.
    /// Never exposed to the tutor; logged for ops.
    /// </summary>
    public string? ErrorCode { get; set; }
}