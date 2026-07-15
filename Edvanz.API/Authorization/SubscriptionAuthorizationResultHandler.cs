using System.Text.Json;
using Edvanz.Application.Security;
using Edvanz.Domain.Resources;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Localization;

namespace Edvanz.API.Authorization;

/// <summary>
/// Custom <see cref="IAuthorizationMiddlewareResultHandler"/> that intercepts Forbidden results
/// caused by known, user-actionable reasons and returns a clear, localized envelope instead of
/// the framework's bare, body-less 403:
///
///   - <see cref="ActiveSubscriptionHandler.SubscriptionRequiredReason"/> → "please subscribe"
///   - <see cref="PermissionHandler.ModuleNotAssignedReason"/> → "module not assigned to your account"
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
        bool authenticated = context.User?.Identity?.IsAuthenticated == true;

        if (authorizeResult.Forbidden && authenticated)
        {
            var reasons = authorizeResult.AuthorizationFailure?.FailureReasons;

            bool blockedForSubscription = reasons?
                .Any(r => r.Message == ActiveSubscriptionHandler.SubscriptionRequiredReason) == true;

            if (blockedForSubscription)
            {
                await WriteEnvelopeAsync(context, "SubscriptionRequired", "subscriptionRequired");
                return;
            }

            bool blockedForMissingModule = reasons?
                .Any(r => r.Message == PermissionHandler.ModuleNotAssignedReason) == true;

            if (blockedForMissingModule)
            {
                await WriteEnvelopeAsync(context, "ModuleNotAssigned", "moduleAccessDenied");
                return;
            }
        }

        await Default.HandleAsync(next, context, policy, authorizeResult);
    }

    /// <summary>
    /// Writes the standard 403 envelope for a known, user-actionable Forbidden reason:
    /// <c>{ success: false, message: &lt;localized&gt;, [flagKey]: true }</c>.
    /// </summary>
    private async Task WriteEnvelopeAsync(HttpContext context, string messageKey, string flagKey)
    {
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
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