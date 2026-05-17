using Edvanz.Domain.Constants;
using Edvanz.Domain.Interfaces;
using Microsoft.AspNetCore.Http;

namespace Edvanz.API.Authorization;

/// <summary>
/// Typed accessor for the per-request <see cref="UserAuthSnapshot"/> that
/// <c>SecurityStampValidationMiddleware</c> deposits on <c>HttpContext.Items</c>
/// for downstream authorization handlers.
///
/// Centralising this avoids string-key typos at the two call sites and makes
/// the data flow explicit:
///   - <c>SecurityStampValidationMiddleware</c> calls <see cref="Set"/>.
///   - <c>PermissionHandler</c> calls <see cref="Get"/> (Phase 5).
///
/// The snapshot is set BEFORE authorization runs, so a null return value from
/// <see cref="Get"/> indicates one of:
///   - Anonymous request (middleware short-circuited on no NameIdentifier claim).
///   - SuperAdmin (middleware short-circuited on role match).
///   - Middleware not registered (configuration error — verify Program.cs).
/// Handlers must treat null as "no live data — fall back to claims" rather
/// than failing the request.
/// </summary>
public static class AuthContextItems
{
    /// <summary>
    /// Stores the resolved snapshot for downstream handlers in this request.
    /// </summary>
    public static void Set(HttpContext context, UserAuthSnapshot snapshot)
    {
        context.Items[AuthConstants.AuthSnapshotItemKey] = snapshot;
    }

    /// <summary>
    /// Retrieves the snapshot resolved earlier in the pipeline.
    /// Returns null when no snapshot was set (anonymous, SuperAdmin,
    /// or middleware not registered).
    /// </summary>
    public static UserAuthSnapshot? Get(HttpContext context)
    {
        return context.Items.TryGetValue(AuthConstants.AuthSnapshotItemKey, out var raw)
            ? raw as UserAuthSnapshot
            : null;
    }
}