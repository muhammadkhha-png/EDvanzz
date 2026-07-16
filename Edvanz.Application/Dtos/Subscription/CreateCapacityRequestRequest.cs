using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Edvanz.Application.Dtos.Subscription;

/// <summary>
/// Input DTO for POST /api/subscription/capacity-requests — a teacher asking the super
/// admin to raise their StudentCapacity (the configuration limit that bounds the roster
/// AND drives the per-student subscription price). Increase-only: the value must exceed
/// the teacher's current capacity.
/// Unknown JSON fields are rejected (400) so a typo'd field never silently succeeds.
/// </summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public class CreateCapacityRequestRequest
{
    /// <summary>
    /// The capacity the teacher is asking for. Must be greater than the current
    /// StudentCapacity and at most SubscriptionConstants.MaxStudentCapacity (100,000).
    /// </summary>
    [Required]
    public int RequestedCapacity { get; set; }

    /// <summary>Optional free-text justification shown to the reviewing admin (max 500 chars).</summary>
    public string? Note { get; set; }
}
