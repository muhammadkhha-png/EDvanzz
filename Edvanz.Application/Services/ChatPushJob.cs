using Edvanz.Application.Dtos.Notifications;
using Edvanz.Application.IservicesContract;
using Edvanz.Domain.Enums;
using Edvanz.Domain.Interfaces;
using Microsoft.Extensions.Logging;
using System.Globalization;

namespace Edvanz.Application.Services;

/// <summary>
/// Per-message FCM push worker for the 1:1 direct-chat subsystem.
/// Implements <see cref="IChatPushJob"/>. Activated by Hangfire on the
/// "notifications" queue after <see cref="IChatPushDispatcher"/> enqueues it
/// immediately following a committed message write.
///
/// PATTERN: mirrors RenewalNotificationJob / PendingPaymentRejectedNotificationJob —
/// fan out to every active UserDeviceToken for the recipient, deactivate stale
/// tokens (EC-11), log per-device outcomes individually, and let unexpected
/// exceptions bubble so Hangfire retries (3 attempts, exponential back-off).
///
/// NO SaveChangesAsync: DeactivateTokenAsync uses ExecuteUpdateAsync (direct SQL),
/// so no EF-tracked changes are staged and no SaveChanges is required.
///
/// [Queue("notifications")] is declared on the INTERFACE method — Hangfire reads
/// the attribute at enqueue time; the implementation does not repeat it (CLAUDE.md §6.1).
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
                "(conversationId {ConversationId}) — skipping",
                recipientUserId, conversationId);
            return;
        }

        // Deep-link payload: Flutter parses this to route directly to the thread on tap.
        // Structured payload: Category = DirectMessage lets the client tell a
        // person-to-person message apart from an app-generated notification; the
        // adapter serializes Screen + Args into the deep-link JSON for tap routing.
        var payload = new PushPayload
        {
            Category = NotificationCategory.msg,
            Screen = "chat",
            Args = new Dictionary<string, string>
            {
                ["conversationId"] = conversationId.ToString(CultureInfo.InvariantCulture)
            }
        };
        string title = $"New message from {senderName}";

        foreach (var token in tokens)
        {
            var result = await _pushSender.SendAsync(
                token.FcmToken, title, messagePreview, payload);
            if (result.Success) continue;

            if (result.ShouldDeactivateToken)
            {
                // EC-11: Firebase reports the token is no longer registered
                // (app uninstalled or device reset). Deactivate so future jobs skip it.
                // ExecuteUpdateAsync is direct SQL — no SaveChanges needed.
                _logger.LogInformation(
                    "ChatPushJob: deactivating stale FCM token {TokenId} " +
                    "for recipient {RecipientUserId} (EC-11, errorCode {ErrorCode})",
                    token.Id, recipientUserId, result.ErrorCode);

                await _unitOfWork.UserDeviceTokensRepo.DeactivateTokenAsync(token.Id);
            }
            else
            {
                // Transient Firebase error — log at Warning.
                // Hangfire retries the whole job (3 attempts) so transient failures
                // are retried automatically. Per-token non-fatal failures are logged
                // only; they do not throw.
                _logger.LogWarning(
                    "ChatPushJob: push failed for token {TokenId}, " +
                    "recipient {RecipientUserId}, errorCode {ErrorCode}",
                    token.Id, recipientUserId, result.ErrorCode);
            }
        }
    }
}