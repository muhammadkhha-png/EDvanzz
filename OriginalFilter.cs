using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;
using System.Text.Json.Nodes;

namespace Edvanz.API.Filters;

/// <summary>
/// Injects request-body and response examples into Swagger UI for the Subscription
/// Management Module (§4.1 / §4.4 / §6.5). Mirrors the project's existing
/// IOperationFilter approach — see <see cref="AcceptLanguageHeaderFilter"/>.
///
/// Lookup is by (HTTP method + route template). Routes that aren't in the table
/// are left untouched, so this filter is safe to register globally and only
/// affects endpoints it knows about.
///
/// Phase 9 deliverable — covers every Subscription endpoint enumerated in
/// SubscriptionController, AdminSubscriptionController, and PaymobWebhookController.
///
/// REGISTRATION (Program.cs alongside AcceptLanguageHeaderFilter):
///   builder.Services.AddSwaggerGen(options =&gt;
///   {
///       options.OperationFilter&lt;AcceptLanguageHeaderFilter&gt;();
///       options.OperationFilter&lt;SwaggerExamplesFilter&gt;();
///   });
/// </summary>
public class SwaggerExamplesFilter : IOperationFilter
{
    /// <summary>
    /// Applies known examples to the operation. Routes are normalized (lowercased,
    /// leading slash stripped) before lookup so route casing variations don't break matching.
    /// </summary>
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        string method = context.ApiDescription.HttpMethod ?? string.Empty;
        string route = NormalizeRoute(context.ApiDescription.RelativePath);

        var examples = ResolveExamples(method, route);
        if (examples is null) return;

        ApplyRequestExample(operation, examples.RequestBody);
        ApplyResponseExamples(operation, examples.Responses);
    }

    // ════════════════════════════════════════════════
    // ROUTE NORMALIZATION
    // ════════════════════════════════════════════════

    private static string NormalizeRoute(string? relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath)) return string.Empty;

        // Strip query string and trailing slash, then lowercase. ASP.NET Core surfaces
        // route templates with placeholders like "api/subscription/renew/status/{pendingPaymentId}"
        // which we keep intact — placeholders are the lookup key.
        int queryIndex = relativePath.IndexOf('?');
        string path = queryIndex >= 0 ? relativePath[..queryIndex] : relativePath;

        path = path.TrimStart('/').TrimEnd('/').ToLowerInvariant();
        return path;
    }

    // ════════════════════════════════════════════════
    // EXAMPLE APPLICATION
    // ════════════════════════════════════════════════

    private static void ApplyRequestExample(OpenApiOperation operation, JsonNode? requestExample)
    {
        if (requestExample is null) return;
        if (operation.RequestBody?.Content is null) return;

        foreach (var mediaType in operation.RequestBody.Content.Values)
        {
            mediaType.Example = requestExample;
        }
    }

    private static void ApplyResponseExamples(
        OpenApiOperation operation,
        IReadOnlyDictionary<string, JsonNode>? responseExamples)
    {
        if (responseExamples is null || responseExamples.Count == 0) return;
        if (operation.Responses is null) return;

        foreach (var (statusCode, example) in responseExamples)
        {
            if (!operation.Responses.TryGetValue(statusCode, out var response)) continue;
            if (response?.Content is null) continue;

            foreach (var mediaType in response.Content.Values)
            {
                mediaType.Example = example;
            }
        }
    }

    // ════════════════════════════════════════════════
    // EXAMPLE TABLE
    // ════════════════════════════════════════════════

    private static SubscriptionExampleSet? ResolveExamples(string method, string route)
    {
        // ─────────── SUBSCRIPTION CONTROLLER (teacher-facing) ───────────

        if (method == "GET" && route == "api/subscription/current")
            return new SubscriptionExampleSet
            {
                Responses = new Dictionary<string, JsonNode>
                {
                    ["200"] = SuccessEnvelope(
                        "Operation completed successfully",
                        new JsonObject
                        {
                            ["id"] = 9001,
                            ["startDate"] = "2026-04-01T00:00:00Z",
                            ["endDate"] = "2026-05-01T00:00:00Z",
                            ["daysRemaining"] = 3,
                            ["status"] = "ExpiringSoon",
                            ["renewalAmountEGP"] = 200.00m
                        }),
                    ["404"] = FailureEnvelope("لا يوجد اشتراك نشط حالياً.") // NoActiveSubscription
                }
            };

        if (method == "GET" && route == "api/subscription/history")
            return new SubscriptionExampleSet
            {
                Responses = new Dictionary<string, JsonNode>
                {
                    ["200"] = SuccessEnvelope(
                        "Operation completed successfully",
                        new JsonObject
                        {
                            ["data"] = new JsonArray
                            {
                                new JsonObject
                                {
                                    ["id"] = 8990,
                                    ["startDate"] = "2026-03-01T00:00:00Z",
                                    ["endDate"] = "2026-04-01T00:00:00Z",
                                    ["amountPaidEGP"] = 200.00m,
                                    ["paymentMethod"] = "VodafoneCash",
                                    ["paymentChannel"] = "Manual",
                                    ["maskedTransactionReference"] = "VFC****4321",
                                    ["paymentConfirmedAt"] = "2026-03-01T08:14:23Z"
                                }
                            },
                            ["page"] = 1,
                            ["pageSize"] = 20,
                            ["totalCount"] = 1
                        })
                }
            };

        if (method == "POST" && route == "api/subscription/renew/initiate")
            return new SubscriptionExampleSet
            {
                RequestBody = new JsonObject
                {
                    ["paymentMethod"] = "VodafoneCash",
                    ["paymentChannel"] = "Manual"
                },
                Responses = new Dictionary<string, JsonNode>
                {
                    ["200"] = SuccessEnvelope(
                        "Subscription renewal flow initiated.",
                        new JsonObject
                        {
                            ["mode"] = "manual",
                            ["paymob"] = null,
                            ["manual"] = new JsonObject
                            {
                                ["pendingPaymentId"] = 5001,
                                ["payToNumber"] = "01000000000",
                                ["instructions"] = "Send the amount to the Vodafone Cash wallet shown above, then submit the transaction reference.",
                                ["amountEGP"] = 200.00m
                            }
                        }),
                    ["409"] = FailureEnvelope("لديك عملية دفع جارية بالفعل.") // PendingPaymentAlreadyInFlight
                }
            };

        if (method == "POST" && route == "api/subscription/renew/manual-submit")
            return new SubscriptionExampleSet
            {
                RequestBody = new JsonObject
                {
                    ["pendingPaymentId"] = 5001,
                    ["paymentPhoneNumber"] = "01012345678",
                    ["transactionReference"] = "VFC987654321"
                },
                Responses = new Dictionary<string, JsonNode>
                {
                    ["200"] = SuccessEnvelope(
                        "Manual payment submission recorded.",
                        new JsonObject
                        {
                            ["pendingPaymentId"] = 5001,
                            ["status"] = "AwaitingSuperAdminApproval",
                            ["initiatedAt"] = "2026-04-28T10:00:00Z",
                            ["resolvedAt"] = null,
                            ["rejectionReason"] = null
                        }),
                    ["404"] = FailureEnvelope("Pending payment not found.")
                }
            };

        if (method == "GET" && route == "api/subscription/renew/status/{pendingpaymentid}")
            return new SubscriptionExampleSet
            {
                Responses = new Dictionary<string, JsonNode>
                {
                    ["200"] = SuccessEnvelope(
                        "Operation completed successfully",
                        new JsonObject
                        {
                            ["pendingPaymentId"] = 5001,
                            ["status"] = "Confirmed",
                            ["initiatedAt"] = "2026-04-28T10:00:00Z",
                            ["resolvedAt"] = "2026-04-28T10:14:02Z",
                            ["rejectionReason"] = null
                        })
                }
            };

        // ─────────── ADMIN SUBSCRIPTION CONTROLLER ───────────

        if (method == "POST" && route == "api/admin/subscriptions/activate")
            return new SubscriptionExampleSet
            {
                RequestBody = new JsonObject
                {
                    ["teacherId"] = 42,
                    ["startDate"] = null,
                    ["endDate"] = null
                },
                Responses = new Dictionary<string, JsonNode>
                {
                    ["200"] = SuccessEnvelope(
                        "Subscription activated.",
                        new JsonObject
                        {
                            ["id"] = 9002,
                            ["startDate"] = "2026-04-28T10:00:00Z",
                            ["endDate"] = "2026-05-28T10:00:00Z",
                            ["daysRemaining"] = 30,
                            ["status"] = "Active",
                            ["renewalAmountEGP"] = 200.00m
                        })
                }
            };

        if (method == "POST" && route == "api/admin/subscriptions/extend")
            return new SubscriptionExampleSet
            {
                RequestBody = new JsonObject
                {
                    ["teacherId"] = 42,
                    ["extensionDays"] = 7
                },
                Responses = new Dictionary<string, JsonNode>
                {
                    ["200"] = SuccessEnvelope(
                        "Subscription extended.",
                        new JsonObject
                        {
                            ["id"] = 9001,
                            ["startDate"] = "2026-04-01T00:00:00Z",
                            ["endDate"] = "2026-05-08T00:00:00Z",
                            ["daysRemaining"] = 10,
                            ["status"] = "Active",
                            ["renewalAmountEGP"] = 200.00m
                        }),
                    ["400"] = FailureEnvelope("Extension days must be positive.")
                }
            };

        if (method == "PUT" && route == "api/admin/subscriptions/end-date")
            return new SubscriptionExampleSet
            {
                RequestBody = new JsonObject
                {
                    ["subscriptionId"] = 9001,
                    ["newEndDate"] = "2026-06-30T00:00:00Z"
                },
                Responses = new Dictionary<string, JsonNode>
                {
                    ["200"] = SuccessEnvelope(
                        "End date updated.",
                        new JsonObject
                        {
                            ["id"] = 9001,
                            ["startDate"] = "2026-04-01T00:00:00Z",
                            ["endDate"] = "2026-06-30T00:00:00Z",
                            ["daysRemaining"] = 63,
                            ["status"] = "Active",
                            ["renewalAmountEGP"] = 200.00m
                        })
                }
            };

        if (method == "GET" && route == "api/admin/subscriptions/pending")
            return new SubscriptionExampleSet
            {
                Responses = new Dictionary<string, JsonNode>
                {
                    ["200"] = SuccessEnvelope(
                        "Operation completed successfully",
                        new JsonObject
                        {
                            ["data"] = new JsonArray
                            {
                                new JsonObject
                                {
                                    ["pendingPaymentId"] = 5001,
                                    ["teacherId"] = 42,
                                    ["teacherName"] = "Ahmed Mostafa",
                                    ["paymentMethod"] = "VodafoneCash",
                                    ["paymentChannel"] = "Manual",
                                    ["amountEGP"] = 200.00m,
                                    ["paymentPhoneNumber"] = "01012345678",
                                    ["transactionReference"] = "VFC987654321",
                                    ["initiatedAt"] = "2026-04-28T10:00:00Z"
                                }
                            },
                            ["page"] = 1,
                            ["pageSize"] = 20,
                            ["totalCount"] = 1
                        })
                }
            };

        if (method == "POST" && route == "api/admin/subscriptions/pending/{pendingpaymentid}/approve")
            return new SubscriptionExampleSet
            {
                Responses = new Dictionary<string, JsonNode>
                {
                    ["200"] = SuccessEnvelope(
                        "Pending payment approved.",
                        new JsonObject
                        {
                            ["subscriptionId"] = 9003,
                            ["startDate"] = "2026-04-28T10:14:02Z",
                            ["endDate"] = "2026-05-28T10:14:02Z",
                            ["wasIdempotentReplay"] = false
                        }),
                    ["409"] = FailureEnvelope("A subscription was already created for this teacher within the last 24 hours.")
                }
            };

        if (method == "POST" && route == "api/admin/subscriptions/pending/{pendingpaymentid}/reject")
            return new SubscriptionExampleSet
            {
                RequestBody = new JsonObject
                {
                    ["rejectionReason"] = "Transaction reference not found in provider dashboard"
                },
                Responses = new Dictionary<string, JsonNode>
                {
                    ["200"] = SuccessEnvelope("Pending payment rejected.", JsonValue.Create(true)!),
                    ["409"] = FailureEnvelope("Pending payment is not awaiting approval.")
                }
            };

        if (method == "PUT" && route == "api/admin/subscriptions/packages/{packageid}/price")
            return new SubscriptionExampleSet
            {
                RequestBody = new JsonObject
                {
                    ["newMonthlyPriceEGP"] = 250.00m
                },
                Responses = new Dictionary<string, JsonNode>
                {
                    ["200"] = SuccessEnvelope("Package price updated.", JsonValue.Create(true)!),
                    ["400"] = FailureEnvelope("Price must be non-negative.")
                }
            };

        // ─────────── PAYMOB WEBHOOK ───────────

        if (method == "POST" && route == "api/webhooks/paymob")
            return new SubscriptionExampleSet
            {
                RequestBody = new JsonObject
                {
                    ["type"] = "TRANSACTION",
                    ["obj"] = new JsonObject
                    {
                        ["id"] = 123456789,
                        ["success"] = true,
                        ["order"] = new JsonObject
                        {
                            ["id"] = 987654321,
                            ["merchant_order_id"] = "5001"
                        }
                    }
                },
                Responses = new Dictionary<string, JsonNode>
                {
                    ["200"] = new JsonObject { ["success"] = true, ["message"] = "Acknowledged" },
                    ["401"] = new JsonObject { ["success"] = false, ["message"] = "Webhook signature invalid." }
                }
            };

        return null;
    }

    // ════════════════════════════════════════════════
    // ENVELOPE HELPERS — match ApiBaseController.ToResponse shape
    // ════════════════════════════════════════════════

    private static JsonObject SuccessEnvelope(string message, JsonNode data) => new()
    {
        ["success"] = true,
        ["message"] = message,
        ["data"] = data
    };

    private static JsonObject FailureEnvelope(string message) => new()
    {
        ["success"] = false,
        ["message"] = message
    };

    // ════════════════════════════════════════════════
    // INTERNAL TYPES
    // ════════════════════════════════════════════════

    /// <summary>
    /// Holds a single endpoint's example set: request body example (optional)
    /// and a response-code-keyed map of response examples.
    /// </summary>
    private sealed class SubscriptionExampleSet
    {
        public JsonNode? RequestBody { get; init; }
        public IReadOnlyDictionary<string, JsonNode>? Responses { get; init; }
    }
}
