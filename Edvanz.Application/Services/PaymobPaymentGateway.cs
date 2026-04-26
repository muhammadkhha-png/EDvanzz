using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using Edvanz.Application.Dtos.Subscription;
using Edvanz.Application.IservicesContract;
using Edvanz.Application.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Edvanz.Infrastructure.Services;

/// <summary>
/// Real Paymob adapter for IPaymentGateway (REQ-SUB-017 / §6.4).
///
/// CURRENT STATUS (v1):
///   This class compiles and is wired through DI, but is only registered when
///   PaymobOptions.Enabled = true — which is FALSE in v1 (D-04). The HTTP-call
///   logic is implemented enough to be exercisable in a v2 sandbox.
///
/// PROTOCOL (Paymob hosted-iframe flow):
///   1. POST /api/auth/tokens — obtain auth token (cached briefly in process).
///   2. POST /api/ecommerce/orders — create order, get order id.
///   3. POST /api/acceptance/payment_keys — get payment_token.
///   4. Build iframe URL: {Host}/api/acceptance/iframes/{IframeId}?payment_token={token}.
///   5. Tutor pays in iframe → Paymob POSTs to our webhook → we verify HMAC.
///
/// HMAC VERIFICATION (REQ-SUB-NFR-008):
///   Paymob signs the callback payload with the merchant's HMAC secret using a
///   documented field-concatenation order. VerifyWebhookSignature implements that
///   contract — see code comment for the exact field list.
/// </summary>
public class PaymobPaymentGateway : IPaymentGateway
{
    // Paymob requires the user to assemble a specific subset of fields, in order,
    // before HMAC-SHA512. Order is taken from the Paymob integration docs.
    // Updating this list is a breaking change — keep in sync with Paymob version.
    private static readonly string[] HmacFieldOrder =
    {
        "amount_cents",
        "created_at",
        "currency",
        "error_occured",
        "has_parent_transaction",
        "id",
        "integration_id",
        "is_3d_secure",
        "is_auth",
        "is_capture",
        "is_refunded",
        "is_standalone_payment",
        "is_voided",
        "order.id",
        "owner",
        "pending",
        "source_data.pan",
        "source_data.sub_type",
        "source_data.type",
        "success"
    };

    private readonly HttpClient _http;
    private readonly PaymobOptions _options;
    private readonly ILogger<PaymobPaymentGateway> _logger;

    public PaymobPaymentGateway(
        HttpClient http,
        IOptions<PaymobOptions> options,
        ILogger<PaymobPaymentGateway> logger)
    {
        _http = http;
        _options = options.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<PaymentGatewaySessionResult> CreateSessionAsync(
        long pendingPaymentId, decimal amountEGP, string description)
    {
        try
        {
            // Step 1: auth token
            string authToken = await GetAuthTokenAsync();

            // Step 2: order
            long paymobOrderId = await CreateOrderAsync(authToken, pendingPaymentId, amountEGP);

            // Step 3: payment key
            string paymentToken = await CreatePaymentKeyAsync(
                authToken, paymobOrderId, amountEGP, description);

            // Step 4: iframe URL
            string iframeUrl =
                $"{_http.BaseAddress?.ToString().TrimEnd('/')}" +
                $"/api/acceptance/iframes/{_options.IframeId}" +
                $"?payment_token={paymentToken}";

            return new PaymentGatewaySessionResult
            {
                Success = true,
                IframeUrl = iframeUrl,
                SessionId = paymobOrderId.ToString(),
                ErrorCode = null
            };
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex,
                "Paymob CreateSession failed for pending payment {PendingId}", pendingPaymentId);
            return new PaymentGatewaySessionResult
            {
                Success = false,
                ErrorCode = "paymob-unreachable"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Paymob CreateSession unexpected failure for pending payment {PendingId}",
                pendingPaymentId);
            return new PaymentGatewaySessionResult
            {
                Success = false,
                ErrorCode = "paymob-error"
            };
        }
    }

    /// <inheritdoc />
    public bool VerifyWebhookSignature(string rawPayload, string providedSignature)
    {
        if (string.IsNullOrWhiteSpace(rawPayload) || string.IsNullOrWhiteSpace(providedSignature))
            return false;

        if (string.IsNullOrEmpty(_options.WebhookHmacSecret))
        {
            _logger.LogError("Paymob WebhookHmacSecret is not configured");
            return false;
        }

        try
        {
            // Paymob's HMAC contract: extract HmacFieldOrder values from the JSON
            // payload, concatenate in order, hash with HMAC-SHA512 using the
            // merchant's secret, hex-encode, and compare to the provided signature.
            string concatenated = ExtractAndConcatenate(rawPayload, HmacFieldOrder);

            using var hmac = new HMACSHA512(Encoding.UTF8.GetBytes(_options.WebhookHmacSecret));
            byte[] hashBytes = hmac.ComputeHash(Encoding.UTF8.GetBytes(concatenated));
            string computed = Convert.ToHexString(hashBytes).ToLowerInvariant();

            // Constant-time comparison to avoid timing-based signature attacks.
            return CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(computed),
                Encoding.UTF8.GetBytes(providedSignature.ToLowerInvariant()));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Paymob HMAC verification threw");
            return false;
        }
    }

    // ════════════════════════════════════════════════
    // PRIVATE HELPERS
    // ════════════════════════════════════════════════

    private async Task<string> GetAuthTokenAsync()
    {
        var response = await _http.PostAsJsonAsync(
            "api/auth/tokens",
            new { api_key = _options.ApiKey });

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<PaymobAuthResponse>()
            ?? throw new InvalidOperationException("Paymob auth response was empty");

        return body.Token;
    }

    private async Task<long> CreateOrderAsync(
        string authToken, long pendingPaymentId, decimal amountEGP)
    {
        var response = await _http.PostAsJsonAsync(
            "api/ecommerce/orders",
            new
            {
                auth_token = authToken,
                delivery_needed = "false",
                amount_cents = ToCents(amountEGP),
                currency = "EGP",
                merchant_order_id = pendingPaymentId.ToString(),
                items = Array.Empty<object>()
            });

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<PaymobOrderResponse>()
            ?? throw new InvalidOperationException("Paymob order response was empty");

        return body.Id;
    }

    private async Task<string> CreatePaymentKeyAsync(
        string authToken, long paymobOrderId, decimal amountEGP, string description)
    {
        var response = await _http.PostAsJsonAsync(
            "api/acceptance/payment_keys",
            new
            {
                auth_token = authToken,
                amount_cents = ToCents(amountEGP),
                expiration = 3600,
                order_id = paymobOrderId,
                billing_data = BuildPlaceholderBillingData(),
                currency = "EGP",
                integration_id = _options.IntegrationId,
                lock_order_when_paid = "true"
            });

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<PaymobPaymentKeyResponse>()
            ?? throw new InvalidOperationException("Paymob payment-key response was empty");

        return body.Token;
    }

    /// <summary>
    /// Extracts each requested field path from the JSON payload and concatenates
    /// the values in order. Missing fields are emitted as empty strings — this is
    /// per Paymob's documented HMAC contract (the receiver MUST handle absent fields).
    /// </summary>
    private static string ExtractAndConcatenate(string rawPayload, string[] fieldOrder)
    {
        using var document = System.Text.Json.JsonDocument.Parse(rawPayload);
        // Paymob nests the payable record under "obj" in the callback payload.
        var root = document.RootElement.TryGetProperty("obj", out var obj) ? obj : document.RootElement;

        var builder = new StringBuilder();
        foreach (var path in fieldOrder)
        {
            builder.Append(GetByDottedPath(root, path));
        }

        return builder.ToString();
    }

    private static string GetByDottedPath(System.Text.Json.JsonElement root, string path)
    {
        var segments = path.Split('.');
        var current = root;

        foreach (var segment in segments)
        {
            if (current.ValueKind != System.Text.Json.JsonValueKind.Object ||
                !current.TryGetProperty(segment, out var next))
            {
                return string.Empty;
            }
            current = next;
        }

        return current.ValueKind switch
        {
            System.Text.Json.JsonValueKind.String => current.GetString() ?? string.Empty,
            System.Text.Json.JsonValueKind.Number => current.GetRawText(),
            System.Text.Json.JsonValueKind.True => "true",
            System.Text.Json.JsonValueKind.False => "false",
            System.Text.Json.JsonValueKind.Null => string.Empty,
            _ => current.GetRawText()
        };
    }

    private static long ToCents(decimal amountEGP) => (long)(amountEGP * 100m);

    private static object BuildPlaceholderBillingData() => new
    {
        // Paymob requires billing_data; the iframe collects real values from the tutor.
        // Placeholders satisfy the API's mandatory-field validation only.
        apartment = "NA",
        email = "NA@example.com",
        floor = "NA",
        first_name = "NA",
        street = "NA",
        building = "NA",
        phone_number = "+201000000000",
        shipping_method = "NA",
        postal_code = "NA",
        city = "NA",
        country = "EG",
        last_name = "NA",
        state = "NA"
    };

    // ════════════════════════════════════════════════
    // PRIVATE RESPONSE TYPES
    // ════════════════════════════════════════════════

    private sealed record PaymobAuthResponse(string Token);
    private sealed record PaymobOrderResponse(long Id);
    private sealed record PaymobPaymentKeyResponse(string Token);
}