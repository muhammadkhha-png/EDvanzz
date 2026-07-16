using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Edvanz.Application.Dtos.Subscription;

/// <summary>
/// Input DTO for POST /api/admin/subscriptions/capacity-requests/{id}/reject.
/// Unknown JSON fields are rejected (400) so a typo'd field never silently succeeds.
/// </summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public class RejectCapacityRequestRequest
{
    /// <summary>Why the request was rejected (max 500 chars) — delivered to the teacher in the rejection notification.</summary>
    [Required]
    public string RejectionReason { get; set; } = null!;
}
