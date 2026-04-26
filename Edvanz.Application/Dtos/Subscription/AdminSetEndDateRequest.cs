namespace Edvanz.Application.Dtos.Subscription;

/// <summary>
/// Input DTO for PUT /api/admin/subscriptions/{id}/end-date (FR-SUB-062 / REQ-ADM-015).
/// Overrides the EndDate on a specific TeacherSubscription row.
/// </summary>
public class AdminSetEndDateRequest
{
    /// <summary>
    /// The TeacherSubscription row whose EndDate to override.
    /// </summary>
    public long SubscriptionId { get; set; }

    /// <summary>
    /// New EndDate (UTC). Must be strictly greater than StartDate; validated at the service boundary.
    /// </summary>
    public DateTime NewEndDate { get; set; }
}