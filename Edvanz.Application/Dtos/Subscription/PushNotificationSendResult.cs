namespace Edvanz.Application.Dtos.Subscription;

/// <summary>
/// Result of IPushNotificationSender.SendAsync.
/// </summary>
public class PushNotificationSendResult
{
    /// <summary>
    /// True when the message was accepted by Firebase for delivery.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// True when Firebase reported "registration-token-not-registered" (EC-11).
    /// Caller flips UserDeviceToken.IsActive = false via IUserDeviceTokenRepo.DeactivateTokenAsync.
    /// </summary>
    public bool ShouldDeactivateToken { get; set; }

    /// <summary>
    /// Diagnostic error code from Firebase. Null on success. Logged for ops.
    /// </summary>
    public string? ErrorCode { get; set; }
}