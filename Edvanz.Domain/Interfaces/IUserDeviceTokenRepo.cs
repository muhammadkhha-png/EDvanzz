using Edvanz.Domain.Entities;

namespace Edvanz.Domain.Interfaces;

/// <summary>
/// Repository for FCM device tokens (§5.5 / D-06).
/// Decoupled from RefreshTokens so push reaches a logged-out tutor for
/// pre-expiry reminders.
///
/// Lifecycle owned by this repo:
///   - Upsert on (UserId, FcmToken) — register endpoint
///   - Read active tokens for a user — per-teacher reminder worker (§7.3)
///   - Deactivate a single token — Firebase reported it unregistered
/// </summary>
public interface IUserDeviceTokenRepo : IGenericRepo<UserDeviceToken, long>
{
    /// <summary>
    /// Looks up a device-token row by the unique (UserId, FcmToken) key.
    /// Returns null if no token is registered for that user/token combination.
    /// Used by the upsert path on POST /api/notifications/register-fcm-token.
    /// </summary>
    Task<UserDeviceToken?> GetByUserAndTokenAsync(long userId, string fcmToken);

    /// <summary>
    /// Returns the user's currently-active FCM tokens (IsActive = true).
    /// Per-teacher reminder worker (§7.3) sends a push to every returned token.
    /// EC-12: a user may have multiple devices; the worker fans out to all of them
    /// but inserts only one UserNotification row.
    /// </summary>
    Task<IReadOnlyList<UserDeviceToken>> GetActiveTokensForUserAsync(long userId);

    /// <summary>
    /// Inserts a new device-token row. Called when register-fcm-token sees no
    /// existing match for (UserId, FcmToken).
    /// SaveChanges is NOT called here — the caller owns the unit of work.
    /// </summary>
    Task InsertTokenAsync(UserDeviceToken token);

    /// <summary>
    /// Flips IsActive=false on a single token row by Id. Called when the push
    /// sender receives "registration-token-not-registered" from Firebase (EC-11).
    /// Uses ExecuteUpdateAsync — no entity load.
    /// </summary>
    Task DeactivateTokenAsync(long tokenId);
}