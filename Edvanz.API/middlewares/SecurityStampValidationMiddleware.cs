using System.Security.Claims;
using Edvanz.API.Authorization;
using Edvanz.Application.IservicesContract;
using Edvanz.Domain.Constants;
using Edvanz.Domain.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Edvanz.API.Middleware;

/// <summary>
/// Per-request live-authorization enforcement (REQ-USR-013 / REQ-USR-027 /
/// REQ-USR-008 / BR-ADM-010).
///
/// PIPELINE POSITION:
///   After <c>UseAuthentication</c> (so <c>HttpContext.User</c> is populated)
///   and BEFORE <c>UseAuthorization</c> (so policy handlers see the snapshot).
///
/// PER-REQUEST FLOW:
///   1. Anonymous request → pass through. Unauthenticated endpoints handle
///      their own permission story; authorization will reject anonymous
///      access on protected endpoints downstream.
///   2. Authenticated request:
///       a. Read NameIdentifier and SecurityStamp claims from the JWT.
///          A missing SecurityStamp claim means the token predates this
///          architectural change → 401 with a "TokenInvalidated" reason so
///          the client re-logs-in.
///       b. Load the snapshot:
///            - First from <c>IUserAuthCacheService</c> (Redis hit, ~1ms).
///            - On miss, from the DB via <c>IUserRepo.GetUserAuthSnapshotAsync</c>
///              (then write-through to Redis with default TTL).
///       c. Compare the claim's stamp against <c>snapshot.SecurityStamp</c>:
///          mismatch → 401. The stamp moved server-side (revocation,
///          deactivation, password change) and this token is no longer valid.
///       d. Check <c>snapshot.IsActive</c>: false → 401. The user was
///          deactivated and no further requests should succeed under any
///          previously-issued token.
///       e. Deposit the snapshot on <c>HttpContext.Items</c> via
///          <see cref="AuthContextItems"/> for the rewired
///          <c>PermissionHandler</c> (Phase 5) and the existing
///          <c>ActiveSubscriptionHandler</c>.
///
/// FAILURE MODES:
///   - User row deleted while a token exists: <c>GetUserAuthSnapshotAsync</c>
///     returns null → 401. The cache is invalidated as a side-effect to avoid
///     a stale entry from a future cold read.
///   - Redis unreachable: <c>IUserAuthCacheService.GetAsync</c> returns null
///     (EC-19 swallow), we fall through to DB. Performance degrades, correctness
///     is preserved.
///
/// SUPERADMIN PATH:
///   SuperAdmins still get a snapshot built and validated — they have a
///   SecurityStamp, can be deactivated, and password-change rules apply to them.
///   We do NOT short-circuit on role here. The snapshot's empty Modules and
///   Permissions sets are correct for SuperAdmin and the downstream
///   <c>PermissionHandler</c> already short-circuits on role.
/// </summary>
public class SecurityStampValidationMiddleware
{
    private const string TokenInvalidatedMessage = "TokenInvalidated";

    private readonly RequestDelegate _next;
    private readonly ILogger<SecurityStampValidationMiddleware> _logger;

    public SecurityStampValidationMiddleware(
        RequestDelegate next,
        ILogger<SecurityStampValidationMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(
        HttpContext context,
        IUserAuthCacheService cache,
        IUnitOfWork unitOfWork)
    {
        // ── 1. Anonymous → pass through ──────────────────────────────────
        // No identity attached by UseAuthentication → nothing to validate.
        // Downstream authorization will reject this on protected endpoints.
        if (context.User?.Identity is not { IsAuthenticated: true })
        {
            await _next(context);
            return;
        }

        // ── 2. Parse identity claims ─────────────────────────────────────
        string? userIdClaim = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!long.TryParse(userIdClaim, out long userId))
        {
            // Authenticated principal without a parseable user id is malformed.
            // Reject rather than guessing.
            await RejectAsync(context, TokenInvalidatedMessage);
            return;
        }

        string? tokenStamp = context.User.FindFirst(AuthConstants.SecurityStampClaim)?.Value;
        if (string.IsNullOrWhiteSpace(tokenStamp))
        {
            // Token predates the architectural cutover and has no SecurityStamp.
            // Force the client to re-authenticate.
            _logger.LogInformation(
                "Rejecting token for user {UserId}: missing SecurityStamp claim", userId);
            await RejectAsync(context, TokenInvalidatedMessage);
            return;
        }

        // ── 3. Resolve snapshot (cache → DB on miss) ─────────────────────
        UserAuthSnapshot? snapshot = await cache.GetAsync(userId);

        if (snapshot is null)
        {
            snapshot = await unitOfWork.Users.GetUserAuthSnapshotAsync(userId);

            if (snapshot is null)
            {
                // User row no longer exists. Whatever token they hold is invalid.
                _logger.LogWarning(
                    "Rejecting token for user {UserId}: user not found", userId);
                await RejectAsync(context, TokenInvalidatedMessage);
                return;
            }

            // Write-through so subsequent requests hit Redis instead of the DB.
            await cache.SetAsync(userId, snapshot);
        }

        // ── 4. Compare stamps ────────────────────────────────────────────
        // Ordinal comparison: SecurityStamp values are server-generated GUID
        // strings; case variations are not expected and an exact-match policy
        // is the safer default.
        if (!string.Equals(snapshot.SecurityStamp, tokenStamp, StringComparison.Ordinal))
        {
            _logger.LogInformation(
                "Rejecting token for user {UserId}: SecurityStamp mismatch", userId);
            await RejectAsync(context, TokenInvalidatedMessage);
            return;
        }

        // ── 5. Active-status gate (REQ-USR-008 / REQ-USR-027) ────────────
        if (!snapshot.IsActive)
        {
            _logger.LogInformation(
                "Rejecting token for user {UserId}: account is inactive", userId);
            await RejectAsync(context, TokenInvalidatedMessage);
            return;
        }

        // ── 6. Hand-off to downstream handlers ───────────────────────────
        context.Items[AuthConstants.AuthSnapshotItemKey] = snapshot;

        await _next(context);
    }

    /// <summary>
    /// Writes a 401 response with a stable message body. Kept private so the
    /// rejection shape is consistent across all four rejection paths above.
    /// The client distinguishes 401-with-this-message from a routing 401 and
    /// triggers the re-authentication flow.
    /// </summary>
    private static async Task RejectAsync(HttpContext context, string message)
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsync(
            $"{{\"success\":false,\"message\":\"{message}\"}}");
    }
}