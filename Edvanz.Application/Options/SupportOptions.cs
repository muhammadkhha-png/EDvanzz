namespace Edvanz.Application.Options;

/// <summary>
/// Team/support contact details surfaced to the app. Bound from the appsettings.json "Support"
/// section, so the number can be changed WITHOUT a code change (locally: appsettings.json; on
/// Azure App Service: application setting "Support__WhatsAppNumber").
///
/// Used by the subscription flow's "contact the team" WhatsApp button and returned in
/// GET /api/subscription/status so the client never hardcodes it.
/// </summary>
public class SupportOptions
{
    public const string Section = "Support";

    /// <summary>
    /// The team's WhatsApp number for subscription enquiries. Digits only or E.164 (e.g.
    /// "201000000000") — the client builds a wa.me link from it. Empty when not configured.
    /// </summary>
    public string WhatsAppNumber { get; set; } = string.Empty;
}
