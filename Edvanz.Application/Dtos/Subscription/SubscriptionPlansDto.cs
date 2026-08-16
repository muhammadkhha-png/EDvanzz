namespace Edvanz.Application.Dtos.Subscription;

/// <summary>
/// Output DTO for GET /api/subscription/pricing (teacher-facing) — the live prices the app uses
/// to show the fee before a subscription request. The server always recomputes the authoritative
/// amount when the request is created, so these are for display only.
/// </summary>
public class SubscriptionPlansDto
{
    /// <summary>Monthly price per student for a Full plan, in EGP (fee = students × this).</summary>
    public decimal PerStudentMonthlyEGP { get; set; }

    /// <summary>Flat monthly price for a Managerial plan, in EGP (independent of student count).</summary>
    public decimal ManagerialMonthlyEGP { get; set; }
}
