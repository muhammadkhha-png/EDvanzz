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
    /// THIS IS THE NUMBER THE FULL PLAN IS PRICED ON (BR-SUB-009), so it is applied BEFORE the
    /// activation snapshots its amount — setting it afterwards would record the old price against
    /// the new plan.
    /// </summary>
    public int? LinkedStudentCapacity { get; set; }
}