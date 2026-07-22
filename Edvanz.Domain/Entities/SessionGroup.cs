using Edvanz.Domain.Entities.ShareProp;
using System.ComponentModel.DataAnnotations.Schema;

namespace Edvanz.Domain.Entities;

/// <summary>
/// Represents a named container that groups one or more sessions under a common label.
/// REQ-SES-024: Tutors can create Session Groups (e.g., "Prep Year 1", "Secondary Year 3").
/// REQ-SES-025: Group name supports both Arabic and English.
/// REQ-SES-NFR-004: Scoped to the individual tutor account.
/// BR-SES-005: A group with no sessions is retained until explicitly deleted.
/// </summary>
public class SessionGroup : BaseEntity
{
    /// <summary>
    /// Foreign key to the owning Teacher. All groups are scoped to this teacher.
    /// REQ-SES-NFR-004: Not visible or accessible by other tutor accounts.
    /// </summary>
    [ForeignKey(nameof(Teacher))]
    public long TeacherId { get; set; }
    public Teacher Teacher { get; set; } = null!;

    /// <summary>
    /// Display name of the group. Manually entered by the tutor.
    /// REQ-SES-025: Supports both Arabic and English input.
    /// REQ-SES-031: Renamable at any time.
    /// </summary>
    public string GroupName { get; set; } = null!;

    /// <summary>
    /// Optional free-text description of the group, entered by the tutor on the
    /// create-group screen. REQ-SES-025: supports Arabic and English. Nullable —
    /// groups created before this field existed (and groups saved without one) have null.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Navigation property: sessions belonging to this group.
    /// REQ-SES-027: Group box expands to show sessions within it.
    /// </summary>
    public ICollection<Session> Sessions { get; set; } = new List<Session>();
}