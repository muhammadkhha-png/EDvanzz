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
/// Renewal-confirmation notification worker (§5.9 / I-2). Triggered fire-and-forget
/// by SubscriptionService.ConfirmPaymentAsync after a successful commit.
///
/// CHANNELS:
///   1. Push: every IsActive UserDeviceToken for the teacher's user.
///   2. WhatsApp: deliberately disabled until v2 (per Phase 06 directive — real
///      WhatsApp goes through IMessageDispatcher with a configured trigger).
///   3. UserNotification row: push record only, per D-05.
///
/// FAILURE HANDLING:
///   Any failure throws so Hangfire retries (3 attempts, exponential backoff).
///   Even if all retries fail, the subscription is still active — only the
///   confirmation notification is lost. The teacher still sees their renewed
///   status next time they open the app.
/// </summary>
public class RenewalNotificationJob : IRenewalNotificationJob
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPushNotificationSender _pushSender;
    private readonly IStringLocalizer<Messages> _localizer;
    private readonly ILogger<RenewalNotificationJob> _logger;

    public RenewalNotificationJob(
        IUnitOfWork unitOfWork,
        IPushNotificationSender pushSender,
        IStringLocalizer<Messages> localizer,
        ILogger<RenewalNotificationJob> logger)
    {
        _unitOfWork = unitOfWork;
        _pushSender = pushSender;
        _localizer = localizer;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task SendAsync(long teacherId, long subscriptionId)
    {
        // ── Load the new subscription row to render its EndDate in the body ──
        var subscription = await _unitOfWork.GetRepository<TeacherSubscription, long>()
            .GetByIdAsync(subscriptionId);

        if (subscription is null)
        {
            _logger.LogWarning(
                "RenewalNotificationJob: subscription {SubscriptionId} not found — skipping",
                subscriptionId);
            return;
        }

        var teacher = await _unitOfWork.Users.GetTeacherForReminderAsync(teacherId);
        if (teacher is null)
        {
            _logger.LogInformation(
                "RenewalNotificationJob: teacher {TeacherId} not found — skipping", teacherId);
            return;
        }

        // ── Render localized title / body ──
        SetCurrentCulture(teacher.LanguagePreference);

        string title = _localizer[SubscriptionConstants.Messages.RenewalConfirmationTitle];
        string bodyTemplate = _localizer[SubscriptionConstants.Messages.RenewalConfirmationBody];
        string body = string.Format(
            CultureInfo.CurrentCulture,
            bodyTemplate,
            subscription.EndDate.ToString("yyyy-MM-dd", CultureInfo.CurrentCulture));

        // ── Push fan-out (WhatsApp deliberately deferred until v2) ──
        await SendPushAsync(teacher.UserId, title, body);

        // ── Persist the inbox record ──
        await _unitOfWork.UserNotificationsRepo.InsertNotificationAsync(new UserNotification
        {
            UserId = teacher.UserId,
            Title = title,
            Body = body,
            DeepLinkPayload = "/subscription/current",
            SentAt = DateTime.UtcNow,
            IsRead = false,
            Category = NotificationCategory.RenewalConfirmed,
            CreateAt = DateTime.UtcNow
        });

        await _unitOfWork.SaveChangesAsync();
    }

    // ════════════════════════════════════════════════
    // PRIVATE HELPERS
    // ════════════════════════════════════════════════

    private async Task SendPushAsync(long userId, string title, string body)
    {
        var tokens = await _unitOfWork.UserDeviceTokensRepo.GetActiveTokensForUserAsync(userId);
        if (tokens.Count == 0) return;

        foreach (var token in tokens)
        {
            var result = await _pushSender.SendAsync(token.FcmToken, title, body, "/subscription/current");

            if (!result.Success && result.ShouldDeactivateToken)
            {
                // EC-11: stale FCM token — deactivate so future runs skip it.
                await _unitOfWork.UserDeviceTokensRepo.DeactivateTokenAsync(token.Id);
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