using Edvanz.Domain.Entities.ShareProp;
using System.ComponentModel.DataAnnotations;

namespace Edvanz.Domain.Entities;

/// <summary>
/// Runtime-editable mobile-app version gate, one row per <see cref="Platform"/> ("android" / "ios").
/// Read by the anonymous <c>GET /api/app/version-status</c> startup check and edited from the
/// SuperAdmin dashboard (<c>/api/admin/app-version</c>).
///
/// DB-FIRST, OPTIONS-FALLBACK: an ABSENT row for a platform means "use the compile-time
/// <c>AppVersionOptions</c> default" (the appsettings.json "AppVersion" section). So the table starts
/// empty and the gate stays dormant until an admin saves values — no seed rows in the migration.
///
/// Tiny config table: no soft-delete, no navigation. <see cref="UpdatedByUserId"/> is a plain audit
/// column (NO FK / navigation — CLAUDE.md §4.1).
/// </summary>
public class AppVersionConfig : BaseEntity
{
    /// <summary>Platform key (unique): "android" or "ios". Stored lowercase.</summary>
    [MaxLength(16)]
    public string Platform { get; set; } = null!;

    /// <summary>Lowest build still allowed to run; a build below this is FORCED to update.</summary>
    public int MinSupportedBuild { get; set; }

    /// <summary>Newest build in the store; a build at/above <see cref="MinSupportedBuild"/> but below this is OFFERED an optional update.</summary>
    public int LatestBuild { get; set; }

    /// <summary>Human-readable latest version name (e.g. "2.3.4") shown in the update prompt.</summary>
    [MaxLength(32)]
    public string LatestVersion { get; set; } = null!;

    /// <summary>Deep link to the platform's store listing (the update button target).</summary>
    [MaxLength(512)]
    public string StoreUrl { get; set; } = null!;

    /// <summary>When the row was last saved via the admin endpoint (UTC). Null = never edited.</summary>
    public DateTime? UpdatedAt { get; set; }

    /// <summary>User id of the super admin who last saved the row. Plain audit column — no FK.</summary>
    public long? UpdatedByUserId { get; set; }
}
