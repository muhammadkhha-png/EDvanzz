namespace Edvanz.Application.Options;

/// <summary>
/// Configuration for the PUBLIC parent portal (parent.edvanz.io — a PHP page that calls this API
/// server-to-server). Bound from the "ParentPortal" section, so every value can be changed
/// WITHOUT a redeploy: locally in appsettings.json, in production via App Service settings
/// (<c>ParentPortal__Enabled</c>, <c>ParentPortal__PortalKey</c>, …).
/// </summary>
public class ParentPortalOptions
{
    public const string Section = "ParentPortal";

    /// <summary>
    /// Platform-wide kill switch. When false EVERY parent-portal route short-circuits to
    /// <c>ParentPortalUnavailable</c> before any handler runs — no lookups, no writes. Default
    /// true; flip it in App Service settings to take the whole public surface down instantly.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Shared secret the PHP portal sends in the <c>X-Portal-Key</c> header on every call.
    /// NEVER committed: the real value is the App Service setting
    /// <c>ParentPortal__PortalKey</c>; appsettings.json only carries an empty placeholder.
    ///
    /// SECURITY: when this is empty the key filter rejects EVERY request (fail-closed). An
    /// unconfigured deployment therefore serves nothing rather than serving everything.
    /// </summary>
    public string PortalKey { get; set; } = string.Empty;

    /// <summary>
    /// Abuse cap: access requests one DEVICE may create per rolling hour. Above it the endpoint
    /// returns <c>ParentPortalTooManyRequests</c> (429) instead of writing another row.
    /// </summary>
    public int RequestsPerDevicePerHour { get; set; } = 10;

    /// <summary>
    /// Abuse cap: access requests aimed at one TEACHER per rolling hour — stops a single teacher's
    /// inbox from being flooded even when the attacker rotates device ids.
    /// </summary>
    public int RequestsPerTeacherPerHour { get; set; } = 50;
}
