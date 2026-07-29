using Edvanz.Application.Dtos.Notifications;
using Edvanz.Application.IservicesContract;
using Edvanz.Domain.Constants;
using Edvanz.Domain.Entities;
using Edvanz.Domain.Enums;
using Edvanz.Domain.Interfaces;
using Edvanz.Domain.Resources;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using System.Globalization;

namespace Edvanz.Application.Services;

/// <summary>
/// Pending-payment rejection notification worker. Triggered fire-and-forget by
/// AdminSubscriptionService.RejectPendingAsync.
///
/// CHANNELS (same channels as RenewalNotificationJob):
///   1. Push to every IsActive UserDeviceToken.
///   2. WhatsApp deferred until v2.
///   3. UserNotification row with the rejection reason in the body.
///
/// The body is parameterized with the rejection reason so the teacher knows
/// what to fix before resubmitting. Deep link routes back to the renewal screen
/// in manual mode.
/// </summary>
public class PendingPaymentRejectedNotificationJob : IPendingPaymentRejectedNotificationJob
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPushNotificationSender _pushSender;
    private readonly IStringLocalizer<Messages> _localizer;
    private readonly ILogger<PendingPaymentRejectedNotificationJob> _logger;

    public PendingPaymentRejectedNotificationJob(
        IUnitOfWork unitOfWork,
        IPushNotificationSender pushSender,
        IStringLocalizer<Messages> localizer,
        ILogger<PendingPaymentRejectedNotificationJob> logger)
    {
        _unitOfWork = unitOfWork;
        _pushSender = pushSender;
        _localizer = localizer;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task SendAsync(long teacherId, long pendingPaymentId, string rejectionReason)
    {
        var teacher = await _unitOfWork.Users.GetTeacherForReminderAsync(teacherId);
        if (teacher is null)
        {
            _logger.LogInformation(
                "PendingPaymentRejectedNotificationJob: teacher {TeacherId} not found — skipping",
                teacherId);
            return;
        }

        // ── Render localized title / body ──
        SetCurrentCulture(teacher.LanguagePreference);
        string title = _localizer[SubscriptionConstants.Messages.PaymentRejectedTitle];
        string bodyTemplate = _localizer[SubscriptionConstants.Messages.PaymentRejectedBody];
        string body = string.Format(CultureInfo.CurrentCulture, bodyTemplate, rejectionReason);
        const string deepLink = "/subscription/renew";

        // ── Persist the inbox record FIRST — the badge (computed next) must reflect it ──
        await _unitOfWork.UserNotificationsRepo.InsertNotificationAsync(new UserNotification
        {
            UserId = teacher.UserId,
            Title = title,
            Body = body,
            DeepLinkPayload = deepLink,
            SentAt = DateTime.UtcNow,
            IsRead = false,
            Category = NotificationCategory.notifiction,
            CreateAt = DateTime.UtcNow
        });
        await _unitOfWork.SaveChangesAsync();

        // ── Push fan-out — badge = unread bell-inbox count AFTER the insert above ──
        int unreadNotificationCount =
            await _unitOfWork.UserNotificationsRepo.GetUnreadCountByUserAsync(teacher.UserId);
        var pushPayload = new PushPayload
        {
            Category = NotificationCategory.notifiction,
            Screen = deepLink,
            Badge = unreadNotificationCount
        };
        await SendPushAsync(teacher.UserId, title, body, pushPayload);
    }

    // ════════════════════════════════════════════════
    // PRIVATE HELPERS (mirror RenewalNotificationJob)
    // ════════════════════════════════════════════════

    private async Task SendPushAsync(long userId, string title, string body, PushPayload payload)
    {
        var tokens = await _unitOfWork.UserDeviceTokensRepo.GetActiveTokensForUserAsync(userId);
        if (tokens.Count == 0) return;

        var tokenValues = new List<string>(tokens.Count);
        foreach (var t in tokens) tokenValues.Add(t.FcmToken);

        var results = await _pushSender.SendMulticastAsync(tokenValues, title, body, payload);

        for (int i = 0; i < tokens.Count; i++)
        {
            if (!results[i].Success && results[i].ShouldDeactivateToken)
            {
                await _unitOfWork.UserDeviceTokensRepo.DeactivateTokenAsync(tokens[i].Id);
            }
        }
    }
    private static void SetCurrentCulture(string? languagePreference)
    {
        string code = languagePreference?.Trim().ToLowerInvariant() switch
        {
            "ar" => "ar",
            "en" => "en",
            _ => "en"
        };

        var culture = new CultureInfo(code);
        CultureInfo.CurrentCulture = culture;
        CultureInfo.CurrentUICulture = culture;
    }
}