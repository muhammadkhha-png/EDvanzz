using Edvanz.Application.Dtos.Notifications;
using Edvanz.Application.IservicesContract;
using Edvanz.Domain.Enums;
using Edvanz.Domain.Interfaces;
using Microsoft.Extensions.Localization;
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
/// LOCALIZATION (🔴-2): the notification TITLE is rendered under the RECIPIENT's
/// LanguagePreference via the RenderInCulture pattern (see StudentLinkNotifier).
/// The HTTP request culture belongs to the SENDER and is the wrong language for
/// the recipient's push. The BODY stays the raw message preview — it is the
/// literal message text and must not be localized.
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
    private readonly IStringLocalizer<Domain.Resources.Messages> _localizer;
    private readonly ILogger<ChatPushJob> _logger;

    public ChatPushJob(
        IUnitOfWork unitOfWork,
        IPushNotificationSender pushSender,
        IStringLocalizer<Domain.Resources.Messages> localizer,
        ILogger<ChatPushJob> logger)
    {
        _unitOfWork = unitOfWork;
        _pushSender = pushSender;
        _localizer = localizer;
        _logger = logger;
    }

    /// <inheritdoc />
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

        // 🔴-2: render the title under the RECIPIENT's culture, not the ambient HTTP
        // request culture (which belongs to the sender). Body stays the raw preview.
        string? recipientLanguage = await _unitOfWork.Users
            .GetUserLanguagePreferenceByUserIdAsync(recipientUserId);
        string title = RenderTitleInCulture(recipientLanguage, senderName);

        // Badge = total unread chat messages for the recipient across ALL conversations
        // (not just this one) — msg badge counts unread chats only, never bell-notification unreads.
        int unreadChatCount = await _unitOfWork.ChatRepo.GetTotalUnreadCountAsync(recipientUserId);

        // Deep-link payload: Flutter parses this to route directly to the thread on tap.
        var payload = new PushPayload
        {
            Category = NotificationCategory.msg,
            Screen = "chat",
            Badge = unreadChatCount,
            Args = new Dictionary<string, string>
            {
                ["conversationId"] = conversationId.ToString(CultureInfo.InvariantCulture)
            }
        };

        var tokenValues = new List<string>(tokens.Count);
        foreach (var t in tokens) tokenValues.Add(t.FcmToken);

        // One FCM call for every device this recipient has registered, instead of
        // N sequential SendAsync round-trips (all devices share the same badge/category).
        var results = await _pushSender.SendMulticastAsync(tokenValues, title, messagePreview, payload);

        for (int i = 0; i < tokens.Count; i++)
        {
            var result = results[i];
            if (result.Success) continue;

            if (result.ShouldDeactivateToken)
            {
                // EC-11: Firebase reports the token is no longer registered
                // (app uninstalled or device reset). Deactivate so future jobs skip it.
                // ExecuteUpdateAsync is direct SQL — no SaveChanges needed.
                _logger.LogInformation(
                    "ChatPushJob: deactivating stale FCM token {TokenId} " +
                    "for recipient {RecipientUserId} (EC-11, errorCode {ErrorCode})",
                    tokens[i].Id, recipientUserId, result.ErrorCode);

                await _unitOfWork.UserDeviceTokensRepo.DeactivateTokenAsync(tokens[i].Id);
            }
            else
            {
                // Transient Firebase error — log at Warning.
                // Hangfire retries the whole job (3 attempts) so transient failures
                // are retried automatically. Per-token non-fatal failures are logged only.
                _logger.LogWarning(
                    "ChatPushJob: push failed for token {TokenId}, " +
                    "recipient {RecipientUserId}, errorCode {ErrorCode}",
                    tokens[i].Id, recipientUserId, result.ErrorCode);
            }
        }
    }

    /// <summary>
    /// Renders <c>Chat_PushTitle</c> under the RECIPIENT's culture, then restores the
    /// ambient culture in a finally block. Mirrors StudentLinkNotifier.RenderInCulture:
    /// the HTTP request culture belongs to the message SENDER, which is the wrong
    /// language for the recipient's push. Null / unknown preference falls back to "en".
    /// </summary>
    private string RenderTitleInCulture(string? languagePreference, string senderName)
    {
        var originalUiCulture = CultureInfo.CurrentUICulture;
        var originalCulture = CultureInfo.CurrentCulture;
        try
        {
            var culture = new CultureInfo(
                languagePreference?.Trim().ToLowerInvariant() == "ar" ? "ar" : "en");
            CultureInfo.CurrentCulture = culture;
            CultureInfo.CurrentUICulture = culture;

            return _localizer["Chat_PushTitle", senderName];
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
            CultureInfo.CurrentUICulture = originalUiCulture;
        }
    }
}