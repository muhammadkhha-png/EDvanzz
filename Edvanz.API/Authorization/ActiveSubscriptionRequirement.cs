using Microsoft.AspNetCore.Authorization;

namespace Edvanz.API.Authorization;

/// <summary>
/// Authorization requirement (§8.1) that gates every authenticated request behind
/// an Active or ExpiringSoon subscription. Endpoints decorated with
/// [AllowExpiredSubscription] are exempt.
///
/// Marker-only — all logic lives in ActiveSubscriptionHandler.
/// Registered as the global authorization policy in Program.cs:
///   options.FallbackPolicy = new AuthorizationPolicyBuilder()
///       .RequireAuthenticatedUser()
///       .AddRequirements(new ActiveSubscriptionRequirement())
///       .Build();
/// </summary>
public class ActiveSubscriptionRequirement : IAuthorizationRequirement
{
}