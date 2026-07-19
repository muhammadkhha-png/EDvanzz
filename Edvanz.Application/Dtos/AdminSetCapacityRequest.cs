using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Edvanz.Application.Dtos.Subscription;

/// <summary>
/// Input DTO for PUT /api/admin/subscriptions/teachers/{teacherId}/capacity — a super
/// admin raising a teacher's StudentCapacity directly, WITHOUT a prior teacher-initiated
/// request. Increase-only (product decision): the value must exceed the teacher's current
/// capacity; decreases are intentionally not supported here.
///
/// Same side effects as approving a capacity request: capacity applies immediately, an
/// Approved CapacityIncreaseRequest audit row is written, the teacher is notified, and the
/// new price applies from the NEXT renewal (BR-SUB-009). TeacherId comes from the route.
/// Unknown JSON fields are rejected (400) so a typo'd field never silently succeeds.
/// </summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public class AdminSetCapacityRequest
{
    /// <summary>
    /// The capacity to raise the teacher to. Must be greater than the teacher's current
    /// StudentCapacity and at most SubscriptionConstants.MaxStudentCapacity.
    /// </summary>
    [Required]
    public int NewCapacity { get; set; }

    /// <summary>Optional admin note stored on the audit row (max 500 chars).</summary>
    public string? Note { get; set; }
}
