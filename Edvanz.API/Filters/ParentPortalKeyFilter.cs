using System.Security.Cryptography;
using System.Text;
using Edvanz.Application.Options;
using Edvanz.Domain.Constants;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;

namespace Edvanz.API.Filters;

/// <summary>
/// The gate in front of every PUBLIC parent-portal route. Those routes are
/// <c>[AllowAnonymous]</c> (no JWT exists — the caller is a PHP page acting for an unauthenticated
/// parent), so this filter is the ONLY thing standing between the internet and the endpoints.
///
/// It enforces, in order:
/// <list type="number">
///   <item>the platform kill switch <c>ParentPortal:Enabled</c> — off ⇒ every route answers
///         <c>ParentPortalUnavailable</c> (503) before any handler, lookup or write runs;</item>
///   <item>the shared secret <c>X-Portal-Key</c>, compared in CONSTANT TIME against
///         <c>ParentPortal:PortalKey</c>. Missing, blank or wrong ⇒ 401. FAIL-CLOSED: when the
///         server has no key configured EVERY request is rejected, so an unconfigured deployment
///         serves nothing rather than everything.</item>
/// </list>
///
/// The per-request device header is NOT checked here — only the actions that actually read data
/// require it, and they surface a localized, portal-renderable state instead of a bare 401.
/// </summary>
public sealed class ParentPortalKeyFilter : IAsyncActionFilter
{
    private readonly ParentPortalOptions _options;
    private readonly IStringLocalizer<Edvanz.Domain.Resources.Messages> _localizer;

    public ParentPortalKeyFilter(
        IOptions<ParentPortalOptions> options,
        IStringLocalizer<Edvanz.Domain.Resources.Messages> localizer)
    {
        _options = options.Value;
        _localizer = localizer;
    }

    /// <inheritdoc />
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        if (!_options.Enabled)
        {
            context.Result = Envelope(StatusCodes.Status503ServiceUnavailable, "ParentPortalUnavailable");
            return;
        }

        string supplied = context.HttpContext.Request.Headers[ParentPortalConstants.PortalKeyHeader].ToString();

        if (string.IsNullOrEmpty(_options.PortalKey) || !IsMatch(supplied, _options.PortalKey))
        {
            context.Result = Envelope(StatusCodes.Status401Unauthorized, "Unauthorized");
            return;
        }

        await next();
    }

    /// <summary>
    /// Length-independent, constant-time comparison. Hashing both sides first means the compare
    /// always runs over 32 bytes, so neither the key's length nor the position of the first
    /// differing byte is observable through timing.
    /// </summary>
    private static bool IsMatch(string supplied, string expected)
    {
        if (string.IsNullOrEmpty(supplied))
            return false;

        Span<byte> suppliedHash = stackalloc byte[32];
        Span<byte> expectedHash = stackalloc byte[32];
        SHA256.HashData(Encoding.UTF8.GetBytes(supplied), suppliedHash);
        SHA256.HashData(Encoding.UTF8.GetBytes(expected), expectedHash);

        return CryptographicOperations.FixedTimeEquals(suppliedHash, expectedHash);
    }

    /// <summary>Same <c>{ success, code, message }</c> envelope ApiBaseController produces, so the portal parses one shape everywhere.</summary>
    private ObjectResult Envelope(int statusCode, string messageKey) =>
        new(new
        {
            success = false,
            code = messageKey,
            message = _localizer[messageKey].ToString()
        })
        { StatusCode = statusCode };
}
