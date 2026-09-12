using System.ComponentModel.DataAnnotations;

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

    /// <summary>
    /// What the teacher paid for these days, if anything. OPTIONAL and nullable on purpose: null
    /// means "not stated", which is the honest record for a goodwill extension and is NOT the same
    /// as zero. Money totals count only stated amounts, so omitting it never invents revenue.
    /// Older admin clients that do not send it keep working unchanged.
    /// </summary>
    public decimal? AmountPaidEGP { get; set; }

    /// <summary>
    /// Why the days were granted, in the admin's own words. "Paid for September" and "compensation
    /// for the outage" are indistinguishable in the dates alone, and the difference is the whole
    /// reason anyone looks this up later.
    /// </summary>
    [MaxLength(500)]
    public string? Note { get; set; }
}