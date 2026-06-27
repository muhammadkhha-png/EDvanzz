using Edvanz.Application.IservicesContract;
using Edvanz.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace Edvanz.Application.Services;

/// <summary>
/// Per-message FCM push worker for the 1:1 direct-chat subsystem.
/// Implements <see cref="IChatPushJob"/>. Enqueued by
/// <see cref="IChatPushDispatcher"/> (Infrastructure) immediately after a
/// message is committed; never called directly by the service layer.
///
/// PATTERN: mirrors RenewalNotificationJob / PendingPaymentRejectedNotificationJob
/// exactly — fan out to every active UserDeviceToken, deactivate stale tokens
/// (EC-11), log per-device outcomes, let transient failures bubble so Hangfire
/// retries (3 attempts, exponential back-off).
///
/// NO SaveChanges needed: DeactivateTokenAsync uses ExecuteUpdateAsync (direct
/// SQL) and does not stage EF tracked changes.
/// </summary>
public class ChatPushJob : IChatPushJob
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPushNotificationSender _pushSender;
    private readonly ILogger<ChatPushJob> _logger;

    public ChatPushJob(
        IUnitOfWork unitOfWork,
        IPushNotificationSender pushSender,
        ILogger<ChatPushJob> logger)
    {
        _unitOfWork = unitOfWork;
        _pushSender = pushSender;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task SendAsync(
        long conversationId,
        string senderName,
        string messagePreview,
        long recipientUserId)
    {
        var tokens = await _unitOfWork.UserDeviceTokensRepo
            .GetActiveTokensForUserAsync(recipientUserId);

        if (tokens.Count == 0)
        {
            _logger.LogDebug(
                "ChatPushJob: no active FCM tokens for recipient {RecipientUserId} " +
                "(conversationId {ConversationId}) — skipping push",
                recipientUserId, conversationId);
            return;
        }

        // Deep-link JSON payload: Flutter routes to the conversation thread on tap.
        string deepLink = $"{{\"screen\":\"chat\",\"conversationId\":{conversationId}}}";
        string title = $"New message from {senderName}";

        foreach (var token in tokens)
        {
            var result = await _pushSender.SendAsync(
                token.FcmToken, title, messagePreview, deepLink);

            if (result.Success) continue;

            if (result.ShouldDeactivateToken)
            {
                // EC-11: Firebase reports the token is no longer registered
                // (app uninstalled / device reset). Deactivate so future jobs skip it.
                // ExecuteUpdateAsync — no SaveChanges needed.
                _logger.LogInformation(
                    "ChatPushJob: deactivating stale FCM token {TokenId} " +
                    "for recipient {RecipientUserId} (EC-11, code {ErrorCode})",
                    token.Id, recipientUserId, result.ErrorCode);

                await _unitOfWork.UserDeviceTokensRepo.DeactivateTokenAsync(token.Id);
            }
            else
            {
                // Transient Firebase error — log at Warning; Hangfire retries the whole job.
                _logger.LogWarning(
                    "ChatPushJob: push failed for token {TokenId}, " +
                    "recipient {RecipientUserId}, error {ErrorCode}",
                    token.Id, recipientUserId, result.ErrorCode);
            }
        }
    }
}