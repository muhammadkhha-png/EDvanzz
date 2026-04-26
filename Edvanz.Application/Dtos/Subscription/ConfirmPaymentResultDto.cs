namespace Edvanz.Application.Dtos.Subscription;

/// <summary>
/// Output DTO from ISubscriptionService.ConfirmPaymentAsync (FR-SUB-037).
/// Returned to the Paymob webhook (HTTP 200 body) and the admin approval endpoint.
/// </summary>
public class ConfirmPaymentResultDto
{
    /// <summary>
    /// The new TeacherSubscription row id created by the confirmation.
    /// </summary>
    public long SubscriptionId { get; set; }

    /// <summary>
    /// New StartDate (UTC). Equals the previous current sub's EndDate for early renewals,
    /// or PaymentConfirmedAt for first-of-sequence (BR-SUB-013 / §6.2).
    /// </summary>
    public DateTime StartDate { get; set; }

    /// <summary>
    /// New EndDate (UTC). Always StartDate + 30 days (D-01).
    /// </summary>
    public DateTime EndDate { get; set; }

    /// <summary>
    /// True when the confirmation was a no-op because the pending row was already
    /// in Status = Confirmed (idempotent re-delivery — EC-03).
    /// </summary>
    public bool WasIdempotentReplay { get; set; }
}