using Edvanz.Application.Dtos.Subscription;

namespace Edvanz.Application.IservicesContract;

/// <summary>
/// Abstraction over the payment provider (Paymob in production, stub in v1).
/// FR-SUB-034 / D-04: ships behind PaymobOptions.Enabled. When disabled, the
/// stub adapter is registered and always returns Success = false so the
/// SubscriptionService flips the response to manual mode.
///
/// Lives in IservicesContract (existing folder convention used by the project
/// for cross-cutting infrastructure abstractions like IEncryptionService and
/// ITokenService).
/// </summary>
public interface IPaymentGateway
{
    /// <summary>
    /// Creates a hosted-payment session (Paymob iframe) and returns the iframe URL
    /// + provider session id. The session id is persisted on PendingSubscriptionPayment
    /// so the subsequent webhook callback can correlate.
    ///
    /// Returns Success = false for the v1 stub (Enabled = false). Caller flips
    /// to manual mode and presents external-pay instructions.
    /// </summary>
    Task<PaymentGatewaySessionResult> CreateSessionAsync(
        long pendingPaymentId, decimal amountEGP, string description);

    /// <summary>
    /// Validates an incoming webhook's HMAC signature (REQ-SUB-NFR-008).
    /// Returns false on signature mismatch — the webhook controller short-circuits with 401.
    /// </summary>
    bool VerifyWebhookSignature(string rawPayload, string providedSignature);
}