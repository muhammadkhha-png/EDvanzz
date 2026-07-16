using Edvanz.Domain.Entities.ShareProp;
using System.ComponentModel.DataAnnotations;

namespace Edvanz.Domain.Entities;

/// <summary>
/// Reference/config table holding the FREE-TIER creation quota for each module, keyed by a stable
/// <see cref="ModuleKey"/> (see <c>ModuleQuotaKeys</c>). An unsubscribed (or expired) teacher may
/// create up to <see cref="FreeTierLimit"/> items in that module; a subscription lifts the cap.
///
/// This is editable at runtime: change a row's <see cref="FreeTierLimit"/> and the new limit takes
/// effect within the cache window — no redeploy, no code change. A value of 0 means the module is
/// subscriber-only for the free tier. Seeded via migration (HasData).
/// </summary>
public class ModuleQuota : BaseEntity
{
    /// <summary>Stable module identifier (unique). Matches a constant in <c>ModuleQuotaKeys</c>.</summary>
    [MaxLength(64)]
    public string ModuleKey { get; set; } = null!;

    /// <summary>Max items an unsubscribed teacher may create in this module. 0 = subscriber-only.</summary>
    public int FreeTierLimit { get; set; }

    /// <summary>Optional human-readable note for whoever edits the row.</summary>
    [MaxLength(256)]
    public string? Description { get; set; }

    /// <summary>When the limit was last changed via the admin endpoint (UTC). Null = never edited since seeding.</summary>
    public DateTime? UpdatedAt { get; set; }

    /// <summary>User id of the super admin who last changed the limit. Plain audit column — no FK.</summary>
    public long? UpdatedByUserId { get; set; }
}
