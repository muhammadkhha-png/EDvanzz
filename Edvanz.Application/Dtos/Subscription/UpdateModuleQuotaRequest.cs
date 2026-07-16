using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Edvanz.Application.Dtos.Subscription;

/// <summary>
/// Input DTO for PUT /api/admin/module-quotas/{moduleKey}.
/// Changes take effect immediately on this instance (the gate cache is invalidated) and
/// within 60 seconds on any other instance.
/// Unknown JSON fields are rejected (400) so a typo'd field never silently succeeds.
/// </summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public class UpdateModuleQuotaRequest
{
    /// <summary>The new free-tier limit: 0 (subscriber-only) to 10,000.</summary>
    [Required]
    public int FreeTierLimit { get; set; }

    /// <summary>Optional replacement for the row's note; omit/null to keep the existing note.</summary>
    public string? Description { get; set; }
}
