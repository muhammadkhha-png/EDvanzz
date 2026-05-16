namespace Edvanz.Domain.Constants;

/// <summary>
/// Constants for the live permission &amp; module revocation enforcement system
/// (REQ-USR-013 / REQ-USR-027 / REQ-USR-008 / BR-ADM-010).
///
/// Centralises Redis key formats, claim names, and HttpContext.Items keys
/// so they are compile-time checked and single-sourced across:
///   - <c>RedisUserAuthCacheService</c> (key format)
///   - <c>SecurityStampValidationMiddleware</c> (claim names, items key)
///   - <c>PermissionHandler</c> (items key)
///   - <c>TokenService</c> (claim names)
/// </summary>
public static class AuthConstants
{
    // ══════════════════════════════════════════════
    // REDIS KEY FORMAT
    // ══════════════════════════════════════════════

    /// <summary>
    /// Format string for the per-user auth snapshot cache key.
    /// Use <c>string.Format(UserAuthCacheKeyFormat, userId)</c>.
    /// Combined with the <c>"edvanz:"</c> instance prefix configured in
    /// <c>InfrastructureServiceExtensions</c>, the full Redis key becomes:
    /// <c>"edvanz:auth:user:{userId}"</c>.
    /// </summary>
    public const string UserAuthCacheKeyFormat = "auth:user:{0}";

    // ══════════════════════════════════════════════
    // JWT CLAIM NAMES
    // ══════════════════════════════════════════════

    /// <summary>
    /// JWT claim carrying the current <c>User.SecurityStamp</c> at the moment
    /// of token issuance. The validation middleware compares this value
    /// against the live snapshot's stamp — a mismatch rejects the token.
    /// </summary>
    public const string SecurityStampClaim = "SecurityStamp";

    // ══════════════════════════════════════════════
    // HttpContext.Items KEYS
    // ══════════════════════════════════════════════

    /// <summary>
    /// Key under which <c>SecurityStampValidationMiddleware</c> deposits the
    /// resolved <see cref="Interfaces.UserAuthSnapshot"/> for downstream
    /// authorization handlers in the same request.
    /// </summary>
    public const string AuthSnapshotItemKey = "Edvanz.AuthSnapshot";
}