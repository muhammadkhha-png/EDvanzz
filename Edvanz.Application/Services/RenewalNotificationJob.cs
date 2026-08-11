using Edvanz.Application.Dtos.Notifications;
using Edvanz.Application.IservicesContract;
using Edvanz.Domain.Constants;
using Edvanz.Domain.Entities;
using Edvanz.Domain.Enums;
using Edvanz.Domain.Interfaces;
using Edvanz.Domain.Resources;
using Microsoft.EntityFrameworkCore;
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
        const string deepLink = "/subscription/current";

        // ── Persist the inbox record FIRST — the badge (computed next) must reflect it ──
        // ── Persist the inbox record FIRST — the badge (computed next) must reflect it ──
        // Idempotency guard (mirrors SubscriptionReminderService): SourceType +
        // SourceEntityId = subscriptionId is unique per renewal, so a Hangfire retry that
        // re-executes this method after the first attempt already committed hits the
        // unique-index violation below instead of inserting a duplicate row / duplicate push.
        try
        {
            await _unitOfWork.UserNotificationsRepo.InsertNotificationAsync(new UserNotification
            {
                UserId = teacher.UserId,
                Title = title,
                Body = body,
                DeepLinkPayload = deepLink,
                SentAt = DateTime.UtcNow,
                IsRead = false,
                Category = NotificationCategory.notifiction,
                SourceType = NotificationSourceType.Renewal,
                SourceEntityId = subscriptionId,
                CreateAt = DateTime.UtcNow
            });
            await _unitOfWork.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            // A prior attempt (or a racing retry) already committed this notification —
            // do NOT send push here, that would duplicate it.
            _logger.LogInformation(
                "RenewalNotificationJob: unique-violation for subscription {SubscriptionId} — " +
                "notification already sent, skipping push", subscriptionId);
            return;
        }

        // ── Push fan-out (WhatsApp deliberately deferred until v2) ──
        // badge = unread bell-inbox count AFTER the insert above
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
    // PRIVATE HELPERS
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
                // EC-11: stale FCM token — deactivate so future runs skip it.
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
    private static bool IsUniqueViolation(DbUpdateException ex)
    {
        Exception? current = ex.InnerException ?? ex.GetBaseException();
        while (current is not null)
        {
            if (current.GetType().Name == "SqlException")
            {
                var numberProperty = current.GetType().GetProperty("Number");
                if (numberProperty?.GetValue(current) is int errorNumber)
                {
                    return errorNumber == 2601 || errorNumber == 2627;
                }
                return false;
            }
            current = current.InnerException;
        }
        return false;
    }
}