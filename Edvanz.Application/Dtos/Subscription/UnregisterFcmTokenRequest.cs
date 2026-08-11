namespace Edvanz.Application.Dtos.Subscription;

/// <summary>
/// Input DTO for DELETE /api/notifications/fcm-token (companion to
/// RegisterFcmTokenRequest / FR-SUB-054). Removes/deactivates a single device
/// token â€” called by the client on logout, or whenever it knows a token is no
/// longer valid for this session.
/// </summary>
public class UnregisterFcmTokenRequest
{
    /// <summary>
    /// The Firebase-generated FCM token to deactivate for the calling user.
    /// </summary>
    public string Token { get; set; } = null!;
}