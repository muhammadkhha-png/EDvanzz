namespace Edvanz.Application.Options;

/// <summary>
/// Per-module creation quotas for UNSUBSCRIBED (or expired) teachers. Bound from the
/// appsettings.json section "FreeTierQuotas", so the limits can be tuned WITHOUT a code change:
///   - locally: edit appsettings.json
///   - on Azure App Service: set application settings "FreeTierQuotas__Students" etc. in the
///     portal (a save restarts the app and the new values take effect — no redeploy, no DDL).
///
/// A value of 0 means the feature is subscriber-only (any create is blocked for the free tier).
/// Attendance and payments are intentionally NOT represented here — they are free for everyone.
/// Defaults below apply when the section (or a key) is absent.
/// </summary>
public class FreeTierQuotaOptions
{
    public const string Section = "FreeTierQuotas";

    /// <summary>Max students an unsubscribed teacher may create. Default 1.</summary>
    public int Students { get; set; } = 1;

    /// <summary>Max sessions an unsubscribed teacher may create. Default 1.</summary>
    public int Sessions { get; set; } = 1;

    /// <summary>Max assistants an unsubscribed teacher may create. Default 0 (subscriber-only).</summary>
    public int Assistants { get; set; } = 0;

    /// <summary>Max session groups an unsubscribed teacher may create. Default 0 (subscriber-only).</summary>
    public int Groups { get; set; } = 0;
}
