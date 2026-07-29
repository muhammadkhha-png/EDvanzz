namespace Edvanz.Application.Dtos.Subscription;

/// <summary>
/// Per-token outcome from IPushNotificationSender.SendMulticastAsync.
/// Mirrors PushNotificationSendResult but scoped to one token within a batch, so
/// the caller can deactivate exactly the tokens Firebase reported as stale (EC-11)
/// without re-querying which one failed.
/// Ordering: the returned list is index-aligned with the input token list — result[i]
/// corresponds to the token at fcmTokens[i].
/// </summary>
public sealed class TokenSendResult
{
    /// <summary>The FCM token this result applies to.</summary>
    public required string FcmToken { get; init; }

    /// <summary>True when Firebase accepted this token's message for delivery.</summary>
    public bool Success { get; init; }

    /// <summary>True when Firebase reported this token unregistered (EC-11).</summary>
    public bool ShouldDeactivateToken { get; init; }

    /// <summary>Diagnostic error code from Firebase. Null on success.</summary>
    public string? ErrorCode { get; init; }
}