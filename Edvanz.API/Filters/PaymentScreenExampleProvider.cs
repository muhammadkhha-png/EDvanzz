using System.Text.Json.Nodes;

namespace Edvanz.API.Filters;

/// <summary>
/// Swagger request/response examples for the Payment "Screens" API (<c>api/v1/*</c>)
/// backing the frontend <c>payment.json</c> spec — <see cref="Controllers.PaymentScreensController"/>.
///
/// Documents the PaymentTracking "students by status" endpoint
/// (<c>GET /api/v1/payments/students</c>), showcasing the
/// <c>paid | partial | prorated | unpaid</c> variants and the per-student
/// <c>studentCode</c> / <c>sessionName</c> / <c>paidOn</c> fields as a selectable dropdown.
///
/// Lookup is by (HTTP method + normalized route). Routes not owned here return null,
/// so the composing <see cref="SwaggerExamplesFilter"/> leaves them untouched.
/// </summary>
public sealed class PaymentScreenExampleProvider : EndpointExampleProvider
{
    public override EndpointExampleSet? GetExamples(string method, string route)
    {
        // Screen: PaymentTracking (students by status)
        // GET /api/v1/payments/students?month=YYYY-MM&status=paid|partial|prorated|unpaid
        if (method == "GET" && route == "api/v1/payments/students")
            return StudentsByStatus();

        return null;
    }

    private EndpointExampleSet StudentsByStatus() => new()
    {
        // month + status + page + limit are [FromQuery] — no request body.
        // One named 200 example per status value → Swagger renders a dropdown.
        ResponseExamples = new Dictionary<string, IReadOnlyDictionary<string, JsonNode>>
        {
            ["200"] = new Dictionary<string, JsonNode>
            {
                ["paid — fully settled (All Paid)"] = Envelope(
                    status: "paid", totalCollected: 55000.00m, monthAmount: 55000.00m, totalUnpaid: 0m,
                    rows: new JsonArray
                    {
                        Row("223", "Essam Ayman", "paid", "Saturday 5pm - prep 3",
                            amountPerMonth: 500.00m, amountPaid: 500.00m, amountDue: 500.00m,
                            unpaidAmount: 0m, unpaidMonths: 0, paidOn: "2026-03-22T14:30:00Z"),
                        Row("224", "Mariam Sherif", "paid", "Monday 3pm - prep 2",
                            amountPerMonth: 400.00m, amountPaid: 400.00m, amountDue: 400.00m,
                            unpaidAmount: 0m, unpaidMonths: 0, paidOn: "2026-03-19T11:05:00Z")
                    }),

                ["partial — part paid (Part Paid)"] = Envelope(
                    status: "partial", totalCollected: 300.00m, monthAmount: 500.00m, totalUnpaid: 200.00m,
                    rows: new JsonArray
                    {
                        // Paid part of the month → paidOn = latest paying transaction; still owes the remainder.
                        Row("225", "Yousef Tarek", "partial", "Saturday 5pm - prep 3",
                            amountPerMonth: 500.00m, amountPaid: 300.00m, amountDue: 500.00m,
                            unpaidAmount: 200.00m, unpaidMonths: 1, paidOn: "2026-03-20T09:15:00Z")
                    }),

                ["prorated — reduced fee, unpaid"] = Envelope(
                    status: "prorated", totalCollected: 0m, monthAmount: 250.00m, totalUnpaid: 250.00m,
                    rows: new JsonArray
                    {
                        // No payment yet → paidOn null; sessionName falls back to the month's period session.
                        Row("226", "Laila Fouad", "prorated", "Saturday 5pm - prep 3",
                            amountPerMonth: 500.00m, amountPaid: 0m, amountDue: 250.00m,
                            unpaidAmount: 250.00m, unpaidMonths: 1, paidOn: null)
                    }),

                ["unpaid — nothing paid"] = Envelope(
                    status: "unpaid", totalCollected: 0m, monthAmount: 500.00m, totalUnpaid: 1500.00m,
                    rows: new JsonArray
                    {
                        Row("227", "Omar Adel", "unpaid", "Monday 3pm - prep 2",
                            amountPerMonth: 500.00m, amountPaid: 0m, amountDue: 500.00m,
                            unpaidAmount: 1500.00m, unpaidMonths: 3, paidOn: null)
                    })
            }
        },
        Responses = new Dictionary<string, JsonNode>
        {
            // Matches the exact literal returned by PaymentScreenService (not yet localized).
            ["422"] = FailureEnvelope("Invalid status; expected paid | partial | prorated | unpaid.")
        }
    };

    // ── builders: fresh instances every call (a JsonNode can have only one parent) ──

    private static JsonObject Envelope(
        string status, decimal totalCollected, decimal monthAmount, decimal totalUnpaid, JsonArray rows) =>
        SuccessEnvelope("Operation completed successfully.", new JsonObject
        {
            ["month"] = "2026-03",
            ["monthLabel"] = "March 2026",
            ["status"] = status,
            ["totalCollected"] = totalCollected,
            ["monthAmount"] = monthAmount,
            // Ambiguous top-level "default fee": no single value exists (per-session/per-student).
            // The per-row amountPerMonth is authoritative.
            ["amountPerMonth"] = 0m,
            ["totalUnpaidAmount"] = totalUnpaid,
            ["total"] = rows.Count,
            ["page"] = 1,
            ["limit"] = 20,
            ["students"] = rows
        });

    private static JsonObject Row(
        string code, string name, string status, string sessionName,
        decimal amountPerMonth, decimal amountPaid, decimal amountDue,
        decimal unpaidAmount, int unpaidMonths, string? paidOn) => new()
        {
            ["id"] = code,                 // illustrative — TeacherStudentId serialized as string
            ["name"] = name,
            ["avatarUrl"] = null,
            ["status"] = status,           // row status echoes the requested group status
            ["amountPerMonth"] = amountPerMonth,
            ["amountPaid"] = amountPaid,
            ["amountDue"] = amountDue,
            ["unpaidAmount"] = unpaidAmount,
            ["unpaidMonths"] = unpaidMonths,
            ["studentCode"] = code,
            ["sessionName"] = sessionName,
            ["paidOn"] = paidOn            // null on unpaid/prorated rows with no payment
        };
}