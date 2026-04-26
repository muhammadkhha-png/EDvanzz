namespace Edvanz.Application.Dtos.Subscription;

/// <summary>
/// Input DTO for POST /api/admin/subscriptions/extend (FR-SUB-061 / REQ-ADM-016).
/// Adds <see cref="ExtensionDays"/> to the current subscription's EndDate.
/// </summary>
public class AdminExtendRequest
{
    public long TeacherId { get; set; }

    /// <summary>
    /// Number of days to add to the current subscription's EndDate. Must be positive.
    /// Validated at the service boundary.
    /// </summary>
    public int ExtensionDays { get; set; }
}