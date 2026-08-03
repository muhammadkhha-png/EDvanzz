using System.Security.Claims;
using Edvanz.Domain.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Edvanz.API.Middleware;

/// <summary>
/// Idle-session enforcement via a sliding refresh-token deadline.
///
/// On every AUTHENTICATED request this pushes the caller's refresh-token idle deadline
/// (<see cref="Edvanz.Domain.Entities.RefreshToken.ExpiryDate"/>) forward to
/// <c>now + Jwt:  </c>. A session therefore stays alive as long as the
/// app keeps calling endpoints, and expires after that many minutes with NO authenticated
/// requests — at which point <c>AuthService.Refresh</c> rejects the now past-dated token
/// and the user must log in again.
///
/// PIPELINE POSITION: after <c>UseAuthentication</c> (so <c>HttpContext.User</c> is
/// populated) and after <c>SecurityStampValidationMiddleware</c> (so invalidated tokens
/// are already rejected and never extend a session), before <c>UseAuthorization</c>.
///
/// NOTES:
///  - Anonymous requests pass through untouched. In particular <c>/api/Auth/refresh</c>
///    is <c>[AllowAnonymous]</c>, so a token refresh does NOT by itself count as activity —
///    only real authenticated calls keep the session alive (idle logout is frontend-proof).
///  - Best-effort: any failure is logged and swallowed; it can never break a request.
///  - Throttled via <see cref="IMemoryCache"/> to at most one DB write per user per
///    <see cref="SlideThrottle"/>, so this stays off the hot path.
/// </summary>
public sealed class SessionActivitySlidingMiddleware
{
    /// <summary>At most one idle-deadline write per user per this window (keeps it cheap).</summary>
    private static readonly TimeSpan SlideThrottle = TimeSpan.FromMinutes(5);

    /// <summary>Fallback idle window if the config key is missing (mirrors appsettings default).</summary>
    private const int DefaultIdleMinutes = 1440;

    private readonly RequestDelegate _next;
    private readonly ILogger<SessionActivitySlidingMiddleware> _logger;

    public SessionActivitySlidingMiddleware(
        RequestDelegate next,
        ILogger<SessionActivitySlidingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(
        HttpContext context,
        IUnitOfWork unitOfWork,
        IMemoryCache cache,
        IConfiguration configuration)
    {
        if (context.User?.Identity is { IsAuthenticated: true } &&
            long.TryParse(context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out long userId))
        {
            await SlideThrottledAsync(userId, unitOfWork, cache, configuration);
        }

        await _next(context);
    }

    private async Task SlideThrottledAsync(
        long userId, IUnitOfWork unitOfWork, IMemoryCache cache, IConfiguration configuration)
    {
        string throttleKey = $"session-slide:{userId}";
        if (cache.TryGetValue(throttleKey, out _))
            return; // slid within the throttle window — skip the DB write

        try
        {
            int idleMinutes = configuration.GetValue<int>("Jwt:RefreshTokenMinutes", DefaultIdleMinutes);
            DateTime now = DateTime.UtcNow;
            await unitOfWork.RefreshTokenRepo.SlideActiveExpiryAsync(userId, now.AddMinutes(idleMinutes), now);

            // Mark throttled only after a successful write, so a transient failure retries next request.
            cache.Set(throttleKey, true, SlideThrottle);
        }
        catch (Exception ex)
        {
            // A failed idle-slide must never fail the request.
            _logger.LogWarning(ex, "Session idle-slide failed for user {UserId}", userId);
        }
    }
}
