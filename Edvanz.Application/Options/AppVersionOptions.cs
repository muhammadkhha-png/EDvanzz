namespace Edvanz.Application.Options;

/// <summary>
/// Mobile-app version gate, bound from the appsettings.json "AppVersion" section so the
/// force/optional-update thresholds change WITHOUT a redeploy (locally: appsettings.json; on Azure
/// App Service: application settings "AppVersion__Android__MinSupportedBuild" etc.).
///
/// Consumed by the anonymous GET /api/app/version-status endpoint the app hits on startup. Defaults
/// are seeded to the CURRENT shipped build so the gate is DORMANT (updateMode == "none") until an
/// operator raises <see cref="AppVersionPlatformOptions.MinSupportedBuild"/> / LatestBuild.
/// </summary>
public class AppVersionOptions
{
    public const string Section = "AppVersion";

    /// <summary>Android build thresholds + store link.</summary>
    public AppVersionPlatformOptions Android { get; set; } = new();

    /// <summary>iOS build thresholds + store link.</summary>
    public AppVersionPlatformOptions iOS { get; set; } = new();
}

/// <summary>Per-platform version thresholds surfaced to the app's startup update gate.</summary>
public class AppVersionPlatformOptions
{
    /// <summary>
    /// Lowest build still allowed to run. A client whose build is BELOW this is FORCED to update
    /// (updateMode == "forced"). Seeded to the current build so nothing is forced until raised.
    /// </summary>
    public int MinSupportedBuild { get; set; }

    /// <summary>
    /// The newest build available in the store. A client at/above <see cref="MinSupportedBuild"/>
    /// but BELOW this is OFFERED an optional update (updateMode == "optional").
    /// </summary>
    public int LatestBuild { get; set; }

    /// <summary>Human-readable latest version name (e.g. "2.3.4") shown in the update prompt.</summary>
    public string LatestVersion { get; set; } = string.Empty;

    /// <summary>Deep link to the platform's store listing (the update button target).</summary>
    public string StoreUrl { get; set; } = string.Empty;
}
