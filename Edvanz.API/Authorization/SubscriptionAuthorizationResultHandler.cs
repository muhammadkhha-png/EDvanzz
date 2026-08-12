using System.Text.Json;
using Edvanz.Application.Security;
using Edvanz.Domain.Resources;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Localization;

namespace Edvanz.API.Authorization;

/// <summary>
/// Custom <see cref="IAuthorizationMiddlewareResultHandler"/> that intercepts two categories of
/// framework outcome that otherwise reach the client as a bare, body-less response, and returns a
/// clear, localized envelope instead:
///
///   Forbidden (403), for known, user-actionable reasons:
///     - <see cref="ActiveSubscriptionHandler.SubscriptionRequiredReason"/> → "please subscribe"
///     - <see cref="PermissionHandler.ModuleNotAssignedReason"/> → "module not assigned to your account"
///
///   Challenged (401), i.e. the request carried no valid authenticated principal at all when it hit
///   the global FallbackPolicy's RequireAuthenticatedUser() — distinct from
///   SecurityStampValidationMiddleware's 401s, which cover a token that WAS present but is stale/
///   invalid/revoked. This branch covers "no token / unparseable token" specifically.
///
/// All other authorization outcomes (other Forbidden reasons, success) are delegated unchanged to
/// the framework's default handler. Envelope shape matches ApiBaseController.
/// </summary>
public sealed class SubscriptionAuthorizationResultHandler : IAuthorizationMiddlewareResultHandler
{
    private static readonly AuthorizationMiddlewareResultHandler Default = new();

    private readonly IStringLocalizer<Messages> _localizer;

    public SubscriptionAuthorizationResultHandler(IStringLocalizer<Messages> localizer)
    {
        _localizer = localizer;
    }

    public async Task HandleAsync(
        RequestDelegate next,
        HttpContext context,
        AuthorizationPolicy policy,
        PolicyAuthorizationResult authorizeResult)
    {
        bool authenticated = context.User?.Identity?.IsAuthenticated == true;

        if (authorizeResult.Forbidden && authenticated)
        {
            var reasons = authorizeResult.AuthorizationFailure?.FailureReasons;

            bool blockedForSubscription = reasons?
                .Any(r => r.Message == ActiveSubscriptionHandler.SubscriptionRequiredReason) == true;

            if (blockedForSubscription)
            {
                await WriteEnvelopeAsync(context, "SubscriptionRequired", "subscriptionRequired", StatusCodes.Status403Forbidden);
                return;
            }

            bool blockedForMissingModule = reasons?
                .Any(r => r.Message == PermissionHandler.ModuleNotAssignedReason) == true;

            if (blockedForMissingModule)
            {
                await WriteEnvelopeAsync(context, "ModuleNotAssigned", "moduleAccessDenied", StatusCodes.Status403Forbidden);
                return;
            }
        }

        // Unauthenticated: no principal reached us at all (missing/malformed Bearer token). A
        // present-but-invalid token is instead caught earlier and 401'd by
        // SecurityStampValidationMiddleware, which never lets an authenticated-but-stale principal
        // reach here Forbidden — so this branch is specifically the "you never logged in" case.
        if (authorizeResult.Challenged && !authenticated)
        {
            await WriteEnvelopeAsync(context, "AuthenticationRequired", "authenticationRequired", StatusCodes.Status401Unauthorized);
            return;
        }

        await Default.HandleAsync(next, context, policy, authorizeResult);
    }

    /// <summary>
    /// Writes the standard envelope for a known, user-actionable authorization outcome:
    /// <c>{ success: false, message: &lt;localized&gt;, [flagKey]: true }</c>.
    /// </summary>
    private async Task WriteEnvelopeAsync(HttpContext context, string messageKey, string flagKey, int statusCode)
    {
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/json; charset=utf-8";

        var payload = JsonSerializer.Serialize(new Dictionary<string, object>
        {
            ["success"] = false,
            ["message"] = _localizer[messageKey].Value,
            [flagKey] = true
        });

        await context.Response.WriteAsync(payload);
    }
}