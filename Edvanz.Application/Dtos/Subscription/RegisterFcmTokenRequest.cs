using Edvanz.Domain.Enums;

namespace Edvanz.Application.Dtos.Subscription;

/// <summary>
/// Input DTO for POST /api/notifications/register-fcm-token (FR-SUB-054).
/// Idempotent on (UserId, FcmToken).
/// </summary>
public class RegisterFcmTokenRequest
{
    /// <summary>
    /// The Firebase-generated FCM token. Long string (typically 150–250 chars).
    /// </summary>
    public string Token { get; set; } = null!;

    /// <summary>
    /// The platform this token was issued for: Android or iOS.
    /// </summary>
    public DevicePlatform Platform { get; set; }
}