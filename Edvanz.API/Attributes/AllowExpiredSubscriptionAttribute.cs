namespace Edvanz.API.Attributes;

/// <summary>
/// Marker attribute (FR-SUB-024 / REQ-SUB-014) that whitelists an endpoint from
/// the active-subscription policy. Apply to:
///   - Subscription endpoints (so an expired tutor can renew)
///   - Auth endpoints (login, refresh token, change password)
///   - Notifications endpoints (so an expired tutor can see why they're locked out)
///   - Webhook endpoints (Paymob callback)
///
/// The ActiveSubscriptionHandler reads this attribute via endpoint metadata and
/// short-circuits the requirement check when present. No other side effects.
///
/// Usage:
///   [HttpPost("renew/initiate")]
///   [AllowExpiredSubscription]
///   public Task&lt;IActionResult&gt; Initiate(...) { ... }
///
/// Class-level application is also supported and applies to every action in the controller.
/// </summary>
[AttributeUsage(
    AttributeTargets.Class | AttributeTargets.Method,
    AllowMultiple = false,
    Inherited = true)]
public sealed class AllowExpiredSubscriptionAttribute : Attribute
{
}