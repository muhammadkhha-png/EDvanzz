using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Edvanz.Application.Dtos.Subscription;

/// <summary>
/// Input DTO for POST /api/admin/subscriptions/requests/{id}/reject.
/// Unknown JSON fields are rejected (400).
/// </summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public class RejectSubscriptionRequestRequest
{
    /// <summary>Why the request was rejected (max 500 chars) — delivered to the teacher.</summary>
    [Required]
    public string RejectionReason { get; set; } = null!;
}
