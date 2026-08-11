using Edvanz.Application.Dtos;
using Edvanz.Application.Dtos.Subscription;

namespace Edvanz.Application.ServiceContract;

/// <summary>
/// Bell-icon inbox operations (§4.2 / FR-SUB-050…054).
/// Push-notification records only — WhatsApp messages are NOT in this inbox (D-05).
///
/// All endpoints backed by this service are whitelisted via [AllowExpiredSubscription]
/// so an expired tutor can still see why their subscription expired.
/// </summary>
public interface INotificationHistoryService
{
    /// <summary>
    /// Paginated bell-icon feed (FR-SUB-050).
    /// </summary>
    Task<Result<PaginatedResponse<List<NotificationDto>>>> GetPagedAsync(
        long userId, int page, int pageSize);

    /// <summary>
    /// Unread badge counter (FR-SUB-052).
    /// </summary>
    Task<Result<int>> GetUnreadCountAsync(long userId);

    /// <summary>
    /// Marks a single notification as read (FR-SUB-053).
    /// Idempotent — re-marking an already-read row is a no-op.
    /// Enforces user ownership: a user cannot mark another user's notification.
    /// </summary>
    Task<Result<bool>> MarkReadAsync(long userId, long notificationId);

    /// <summary>
    /// Bulk mark-all-read (FR-SUB-053). Returns the number of rows transitioned.
    /// </summary>
    Task<Result<int>> MarkAllReadAsync(long userId);

    /// <summary>
    /// Upserts the user's FCM device token (FR-SUB-054).
    /// Idempotent on (UserId, FcmToken) — refreshes LastSeenAt and IsActive
    /// when the token already exists.
    /// </summary>
    Task<Result<bool>> RegisterFcmTokenAsync(
        long userId, RegisterFcmTokenRequest request);
    /// <summary>
    /// Deactivates a single FCM device token for the calling user (companion to
    /// RegisterFcmTokenAsync). Called on logout, or whenever the client knows a
    /// token is no longer valid for this session.
    /// Idempotent — a token that doesn't exist, or is already inactive, still
    /// returns success.
    /// </summary>
    Task<Result<bool>> UnregisterFcmTokenAsync(
        long userId, UnregisterFcmTokenRequest request);
}