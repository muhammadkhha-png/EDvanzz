namespace Edvanz.Application.Dtos.App;

// ══════════════════════════════════════════════════════════════════════════
// App-version gate DTOs (SuperAdmin admin surface: /api/admin/app-version).
// PascalCase C# → camelCase JSON. The Angular admin screen matches these names
// verbatim: { android:{minSupportedBuild,latestBuild,latestVersion,storeUrl}, ios:{...} }.
// ══════════════════════════════════════════════════════════════════════════

/// <summary>One platform's effective version-gate values (DB row if present, else the options default).</summary>
public class AppVersionPlatformDto
{
    /// <summary>Lowest build still allowed to run (below → forced update).</summary>
    public int MinSupportedBuild { get; set; }

    /// <summary>Newest build in the store (at/above min but below → optional update).</summary>
    public int LatestBuild { get; set; }

    /// <summary>Human-readable latest version name, e.g. "2.3.4".</summary>
    public string LatestVersion { get; set; } = string.Empty;

    /// <summary>Store listing deep link (update button target).</summary>
    public string StoreUrl { get; set; } = string.Empty;
}

/// <summary>Both platforms' effective version-gate config — the admin GET response and PUT result.</summary>
public class AppVersionConfigDto
{
    public AppVersionPlatformDto Android { get; set; } = new();

    /// <summary>Serializes as "ios" (camelCase of Ios) — the exact wire key the admin screen expects.</summary>
    public AppVersionPlatformDto Ios { get; set; } = new();
}

/// <summary>Admin PUT body — the SAME shape as <see cref="AppVersionConfigDto"/>; both platforms upserted.</summary>
public class UpdateAppVersionRequest
{
    public AppVersionPlatformDto? Android { get; set; }
    public AppVersionPlatformDto? Ios { get; set; }
}
