using System.Text.Json;
using Edvanz.Domain.Resources;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Localization;

namespace Edvanz.API.Authorization;

/// <summary>
/// Custom <see cref="IAuthorizationMiddlewareResultHandler"/> that intercepts a Forbidden result
/// caused ONLY by <see cref="ActiveSubscriptionHandler"/> (i.e. an authenticated teacher/assistant
/// with an expired or missing subscription) and returns a clear, localized envelope:
///   403 { "success": false, "message": "You need an active subscription ...", "subscriptionRequired": true }
///
/// This replaces the framework's bare, body-less 403 so a newly registered (unsubscribed) tutor
/// gets an actionable "please subscribe" message on EVERY gated action instead of a raw Forbidden.
///
/// All other authorization outcomes (unauthenticated 401, other Forbidden reasons, success) are
/// delegated unchanged to the framework's default handler. Envelope shape matches ApiBaseController.
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
        // Only intervene when the request was blocked SPECIFICALLY because of the subscription
        // requirement — and the caller is authenticated (so this is a "needs to subscribe" case,
        // not an unauthenticated 401).
        var blockedForSubscription =
            authorizeResult.Forbidden &&
            context.User?.Identity?.IsAuthenticated == true &&
            authorizeResult.AuthorizationFailure?.FailureReasons
                .Any(r => r.Message == ActiveSubscriptionHandler.SubscriptionRequiredReason) == true;

        if (blockedForSubscription)
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            context.Response.ContentType = "application/json; charset=utf-8";

            var payload = JsonSerializer.Serialize(new
            {
                success = false,
                message = _localizer["SubscriptionRequired"].Value,
                subscriptionRequired = true
            });

            await context.Response.WriteAsync(payload);
            return;
        }

        await Default.HandleAsync(next, context, policy, authorizeResult);
    }
}
