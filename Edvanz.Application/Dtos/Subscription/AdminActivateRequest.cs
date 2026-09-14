namespace Edvanz.Application.Dtos.Subscription;

/// <summary>
/// Input DTO for POST /api/admin/subscriptions/activate (FR-SUB-060 / REQ-ADM-012).
/// Bypasses payment — inserts a new IsCurrent = true row with PaymentChannel = SuperAdminOverride.
/// </summary>
public class AdminActivateRequest
{
    /// <summary>
    /// The teacher to activate.
    /// </summary>
    public long TeacherId { get; set; }

    /// <summary>
    /// Optional explicit start date (UTC). Null defaults to UtcNow at activation time.
    /// EC-21: a future-dated StartDate produces a row that the policy handler treats
    /// as not-yet-active until UtcNow >= StartDate.
    /// </summary>
    public DateTime? StartDate { get; set; }

    /// <summary>
    /// Optional explicit end date (UTC). Null defaults to StartDate + 30 days (D-01).
    /// </summary>
    public DateTime? EndDate { get; set; }

    /// <summary>
    /// How many students this teacher may hold on the account. NULL LEAVES IT UNCHANGED — an
    /// admin client that does not send it must never silently reset a limit to zero.
    ///
    /// Set here rather than only on a separate screen because the limits and the subscription are
    /// one decision: an admin agreeing a plan is agreeing the numbers it covers, and making them
    /// two screens means activating at the OLD limit and fixing it afterwards.
    /// </summary>
    public int? StudentCapacity { get; set; }

    /// <summary>
    /// How many of those students may have their own app account. NULL LEAVES IT UNCHANGED.
    ///
    /// THIS IS THE NUMBER THE FULL PLAN IS PRICED ON (BR-SUB-009), so it is applied in the SAME
    /// transaction as the subscription row — a plan that activated at a limit the teacher does not
    /// actually have would be agreed for one thing and enforce another.
    /// </summary>
    public int? LinkedStudentCapacity { get; set; }

    /// <summary>
    /// What the teacher paid for this period, if anything. OPTIONAL and nullable on purpose: null
    /// means "not stated", which is the honest record for a free or goodwill activation and is NOT
    /// the same as zero. Payment for an admin activation is arranged outside the app, so nothing
    /// here can know it happened — only the admin doing it can. Omitting it stores 0, which every
    /// teacher-facing surface renders as "no amount recorded" rather than as a price of zero.
    /// Older admin clients that do not send it keep working unchanged.
    ///
    /// DO NOT compute this from the price list. It was tried: the activation priced the period
    /// through the renewal calculator and stored the result, so an admin granting a free month
    /// showed the teacher, in their own subscription history, money they had never paid.
    /// </summary>
    public decimal? AmountPaidEGP { get; set; }
}