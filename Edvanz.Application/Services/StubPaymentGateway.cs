using Edvanz.Application.Dtos.Subscription;
using Edvanz.Application.IservicesContract;
using Microsoft.Extensions.Logging;

namespace Edvanz.Infrastructure.Services;

/// <summary>
/// v1 stub adapter for IPaymentGateway (D-04 / FR-SUB-034).
///
/// Always returns Success = false from CreateSessionAsync so the renewal flow
/// in SubscriptionService.InitiateRenewalAsync flips to manual mode.
/// VerifyWebhookSignature always returns false because no webhook should
/// reach the system while the gateway is stubbed off — the webhook controller
/// short-circuits with 503 (PaymobNotConfigured) before any signature work.
///
/// Registered when PaymobOptions.Enabled = false (the v1 default). The DI
/// registration in InfrastructureServiceExtensions selects between this and
/// PaymobPaymentGateway based on configuration.
///
/// This class is intentionally trivial — it exists so the Application layer
/// can depend on IPaymentGateway without conditional logic for "no gateway".
/// </summary>
public class StubPaymentGateway : IPaymentGateway
{
    private const string NotConfiguredErrorCode = "not-configured";

    private readonly ILogger<StubPaymentGateway> _logger;

    public StubPaymentGateway(ILogger<StubPaymentGateway> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public Task<PaymentGatewaySessionResult> CreateSessionAsync(
        long pendingPaymentId, decimal amountEGP, string description)
    {
        _logger.LogInformation(
            "PaymentGateway stub: pending {PendingId} amount {Amount} EGP — falling back to manual mode",
            pendingPaymentId, amountEGP);

        return Task.FromResult(new PaymentGatewaySessionResult
        {
            Success = false,
            IframeUrl = null,
            SessionId = null,
            ErrorCode = NotConfiguredErrorCode
        });
    }

    /// <inheritdoc />
    public bool VerifyWebhookSignature(string rawPayload, string providedSignature)
    {
        // No webhook should reach the system while the stub is registered.
        // If one does, fail closed.
        _logger.LogWarning(
            "PaymentGateway stub: VerifyWebhookSignature called while gateway is disabled — rejecting");

        return false;
    }
}