using Edvanz.Application.IservicesContract;
using Edvanz.Application.Options;
using Edvanz.Application.ServiceContract;
using Edvanz.Domain.Constants;
using Edvanz.Domain.Interfaces;
using Edvanz.Domain.Resources;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net;

namespace Edvanz.API.Controllers;

/// <summary>
/// Paymob webhook callback (§6.5 / REQ-SUB-NFR-008).
///
/// AUTHORIZATION:
///   [AllowAnonymous] — Paymob's servers don't carry a user token. Trust is
///   established by HMAC-SHA512 signature verification against the merchant's
///   webhook secret. Unsigned / mis-signed requests get 401.
///
/// IDEMPOTENCY:
///   Paymob retries on non-2xx. Our ConfirmPaymentAsync is idempotent on
///   Status = Confirmed (EC-03), so re-deliveries are safe.
///
/// RESPONSE CODES:
///   - 200: payment confirmed (or idempotent replay)
///   - 401: HMAC signature invalid
///   - 404: PaymobSessionId not found in our PendingSubscriptionPayments
///   - 503: PaymobOptions.Enabled = false (stub mode — should never receive callbacks)
///
/// IMPORTANT: We always return 200 for any case where Paymob shouldn't retry —
/// including for terminal states (Rejected/Expired) and unknown sessions. The
/// alternative (404) makes Paymob keep retrying, which spams logs.
/// </summary>
[Route("api/webhooks/paymob")]
[ApiController]
[AllowAnonymous]
public class PaymobWebhookController : ControllerBase
{
    private const string SignatureHeader = "hmac";
    private const string SignatureQueryKey = "hmac";

    private readonly ISubscriptionService _subscriptionService;
    private readonly IPaymentGateway _paymentGateway;
    private readonly IUnitOfWork _unitOfWork;
    private readonly PaymobOptions _paymobOptions;
    private readonly IStringLocalizer<Messages> _localizer;
    private readonly ILogger<PaymobWebhookController> _logger;

    public PaymobWebhookController(
        ISubscriptionService subscriptionService,
        IPaymentGateway paymentGateway,
        IUnitOfWork unitOfWork,
        IOptions<PaymobOptions> paymobOptions,
        IStringLocalizer<Messages> localizer,
        ILogger<PaymobWebhookController> logger)
    {
        _subscriptionService = subscriptionService;
        _paymentGateway = paymentGateway;
        _unitOfWork = unitOfWork;
        _paymobOptions = paymobOptions.Value;
        _localizer = localizer;
        _logger = logger;
    }

    // ══════════════════════════════════════════════════════════════════════════
    // ENDPOINT: PAYMOB CALLBACK (§6.5 / REQ-SUB-NFR-008)
    // ══════════════════════════════════════════════════════════════════════════
    //
    // FLOW:
    //   1. Read raw body (HMAC verification needs the bytes Paymob signed).
    //   2. Read HMAC from "hmac" query string OR header (Paymob varies by integration).
    //   3. Verify signature via IPaymentGateway.VerifyWebhookSignature.
    //   4. Parse the obj.order.merchant_order_id (we set it = pendingPaymentId on initiate).
    //      Or parse obj.order.id and look up by PaymobSessionId.
    //   5. If success = true, call ConfirmPaymentAsync. If success = false, ignore
    //      (failed transactions don't transition our pending row — it expires via job).
    //
    // SAMPLE: POST /api/webhooks/paymob?hmac=abc123…
    //   (Paymob JSON body, see https://docs.paymob.com)
    //
    // ══════════════════════════════════════════════════════════════════════════
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Callback()
    {
        // ── 0. Refuse callbacks when stub mode is in effect ──
        if (!_paymobOptions.Enabled)
        {
            _logger.LogWarning("Paymob webhook received while gateway is disabled — returning 503");
            return StatusCode((int)HttpStatusCode.ServiceUnavailable, new
            {
                success = false,
                message = _localizer[SubscriptionConstants.Messages.PaymobNotConfigured].Value
            });
        }

        // ── 1. Read the raw body (must happen BEFORE model binding consumes it) ──
        Request.EnableBuffering();
        Request.Body.Position = 0;
        using var reader = new StreamReader(Request.Body, leaveOpen: true);
        string rawPayload = await reader.ReadToEndAsync();
        Request.Body.Position = 0;

        if (string.IsNullOrWhiteSpace(rawPayload))
        {
            _logger.LogWarning("Paymob webhook received empty body");
            return Unauthorized(new { success = false, message = _localizer[SubscriptionConstants.Messages.WebhookSignatureInvalid].Value });
        }

        // ── 2. Read HMAC (Paymob sends as query string in their docs; some integrations
        //      use a header — we accept either) ──
        string? providedSignature = Request.Query[SignatureQueryKey].FirstOrDefault()
                                    ?? Request.Headers[SignatureHeader].FirstOrDefault();

        if (string.IsNullOrWhiteSpace(providedSignature))
        {
            _logger.LogWarning("Paymob webhook missing HMAC signature");
            return Unauthorized(new { success = false, message = _localizer[SubscriptionConstants.Messages.WebhookSignatureInvalid].Value });
        }

        // ── 3. Verify signature ──
        if (!_paymentGateway.VerifyWebhookSignature(rawPayload, providedSignature))
        {
            _logger.LogWarning("Paymob webhook HMAC verification FAILED");
            return Unauthorized(new { success = false, message = _localizer[SubscriptionConstants.Messages.WebhookSignatureInvalid].Value });
        }

        // ── 4. Parse the payload to extract pending-payment id and success flag ──
        PaymobCallback? parsed = ParsePayload(rawPayload);
        if (parsed is null)
        {
            _logger.LogWarning("Paymob webhook payload could not be parsed");
            // Return 200 so Paymob doesn't retry forever on a malformed payload.
            return Ok(new { success = false, message = "Payload not parseable" });
        }

        // ── 5. Resolve the pending payment ──
        long? pendingPaymentId = await ResolvePendingPaymentIdAsync(parsed);
        if (pendingPaymentId is null)
        {
            _logger.LogWarning(
                "Paymob webhook references unknown session {SessionId} / merchantOrderId {MoId}",
                parsed.OrderId, parsed.MerchantOrderId);
            // Return 200 — retrying won't help; the row simply isn't ours.
            return Ok(new { success = false, message = "Pending payment not found" });
        }

        // ── 6. Branch on success flag ──
        if (!parsed.IsSuccess)
        {
            // Failed transaction. The PendingPaymentExpiryJob will move the row to
            // Expired after the 24h cutoff (EC-18). No DB write here.
            _logger.LogInformation(
                "Paymob webhook reported FAILURE for pending {PendingId} — leaving in Initiated",
                pendingPaymentId);
            return Ok(new { success = true, message = "Acknowledged (failure)" });
        }

        // ── 7. Confirm via the shared pipeline (§6.3) ──
        var result = await _subscriptionService.ConfirmPaymentAsync(
            pendingPaymentId.Value,
            resolvedByUserId: null); // null = webhook path

        if (!result.IsSuccess)
        {
            _logger.LogError(
                "Paymob webhook confirm failed for pending {PendingId}: {Message}",
                pendingPaymentId, result.Message);
            // Return 500 so Paymob retries — confirm pipeline failures are usually transient
            // (concurrency conflicts already exhausted retries inside the service).
            return StatusCode((int)HttpStatusCode.InternalServerError, new
            {
                success = false,
                message = result.Message
            });
        }

        return Ok(new { success = true, data = result.Data, message = result.Message });
    }

    // ════════════════════════════════════════════════
    // PRIVATE HELPERS
    // ════════════════════════════════════════════════

    private static PaymobCallback? ParsePayload(string rawPayload)
    {
        try
        {
            using var document = System.Text.Json.JsonDocument.Parse(rawPayload);
            // Paymob nests the transaction under "obj".
            var root = document.RootElement.TryGetProperty("obj", out var obj) ? obj : document.RootElement;

            bool isSuccess = root.TryGetProperty("success", out var s) && s.ValueKind == System.Text.Json.JsonValueKind.True;

            string? orderId = null;
            string? merchantOrderId = null;
            if (root.TryGetProperty("order", out var order))
            {
                if (order.TryGetProperty("id", out var idEl))
                    orderId = idEl.ValueKind == System.Text.Json.JsonValueKind.Number
                        ? idEl.GetRawText()
                        : idEl.GetString();

                if (order.TryGetProperty("merchant_order_id", out var moEl))
                    merchantOrderId = moEl.ValueKind == System.Text.Json.JsonValueKind.Number
                        ? moEl.GetRawText()
                        : moEl.GetString();
            }

            return new PaymobCallback(isSuccess, orderId, merchantOrderId);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Resolves the pending-payment id from the callback. Two paths:
    ///   (a) merchant_order_id was set to our pending-payment id at initiate time.
    ///       This is the fast path — parse to long and we're done.
    ///   (b) order.id (Paymob's internal id) is what we stored as PaymobSessionId
    ///       on the pending row. Fall back to a repo lookup.
    /// </summary>
    private async Task<long?> ResolvePendingPaymentIdAsync(PaymobCallback parsed)
    {
        if (long.TryParse(parsed.MerchantOrderId, out var merchantOrderAsLong))
        {
            return merchantOrderAsLong;
        }

        if (string.IsNullOrWhiteSpace(parsed.OrderId)) return null;

        var pending = await _unitOfWork.SubscriptionPaymentsRepo
            .GetByPaymobSessionIdAsync(parsed.OrderId);

        return pending?.Id;
    }

    private sealed record PaymobCallback(bool IsSuccess, string? OrderId, string? MerchantOrderId);
}