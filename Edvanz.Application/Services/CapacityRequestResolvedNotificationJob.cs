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
/// Capacity-request resolution notification worker. Triggered fire-and-forget by
/// AdminSubscriptionService approve/reject, post-commit and best-effort.
///
/// CHANNELS (same channels as PendingPaymentRejectedNotificationJob):
///   1. Push to every IsActive UserDeviceToken.
///   2. UserNotification inbox row (approved body carries the new capacity; rejected
///      body carries the admin's reason).
///
/// Localized to the RECIPIENT teacher's LanguagePreference. Idempotent-safe in the
/// Hangfire-retry sense: a re-run re-sends the same message (acceptable, matches the
/// other subscription notification jobs).
/// </summary>
public class CapacityRequestResolvedNotificationJob : ICapacityRequestResolvedNotificationJob
{
    private const string DeepLink = "/subscription/capacity-requests";

    private readonly IUnitOfWork _unitOfWork;
    private readonly IPushNotificationSender _pushSender;
    private readonly IStringLocalizer<Messages> _localizer;
    private readonly ILogger<CapacityRequestResolvedNotificationJob> _logger;

    public CapacityRequestResolvedNotificationJob(
        IUnitOfWork unitOfWork,
        IPushNotificationSender pushSender,
        IStringLocalizer<Messages> localizer,
        ILogger<CapacityRequestResolvedNotificationJob> logger)
    {
        _unitOfWork = unitOfWork;
        _pushSender = pushSender;
        _localizer = localizer;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task SendAsync(long teacherId, long requestId, bool approved, string? rejectionReason)
    {
        var teacher = await _unitOfWork.Users.GetTeacherForReminderAsync(teacherId);
        if (teacher is null)
        {
            _logger.LogInformation(
                "CapacityRequestResolvedNotificationJob: teacher {TeacherId} not found — skipping",
                teacherId);
            return;
        }

        var request = await _unitOfWork.CapacityRequestsRepo.GetByIdForAdminAsync(requestId);
        if (request is null)
        {
            _logger.LogInformation(
                "CapacityRequestResolvedNotificationJob: request {RequestId} not found — skipping",
                requestId);
            return;
        }

        // ── Render localized title / body in the RECIPIENT's language ──
        SetCurrentCulture(teacher.LanguagePreference);
        string title, body;
        // The enum collapse (msg / notification) removed the Approved/Rejected
        // distinction — both branches now land on the same category. Kept as an
        // explicit variable (not inlined) so a future third bucket only touches these
        // two branches.
        NotificationCategory category = NotificationCategory.notifiction;
        if (approved)
        {
            title = _localizer[SubscriptionConstants.Messages.CapacityRequestApprovedTitle];
            body = string.Format(
                CultureInfo.CurrentCulture,
                _localizer[SubscriptionConstants.Messages.CapacityRequestApprovedBody],
                request.RequestedCapacity);
        }
        else
        {
            title = _localizer[SubscriptionConstants.Messages.CapacityRequestRejectedTitle];
            body = string.Format(
                CultureInfo.CurrentCulture,
                _localizer[SubscriptionConstants.Messages.CapacityRequestRejectedBody],
                rejectionReason ?? request.RejectionReason ?? string.Empty);
        }

        // ── Persist the inbox record FIRST — the badge (computed next) must reflect it ──
        await _unitOfWork.UserNotificationsRepo.InsertNotificationAsync(new UserNotification
        {
            UserId = teacher.UserId,
            Title = title,
            Body = body,
            DeepLinkPayload = DeepLink,
            SentAt = DateTime.UtcNow,
            IsRead = false,
            Category = category,
            CreateAt = DateTime.UtcNow
        });
        await _unitOfWork.SaveChangesAsync();

        // ── Push fan-out — badge = unread bell-inbox count AFTER the insert above ──
        int unreadNotificationCount =
            await _unitOfWork.UserNotificationsRepo.GetUnreadCountByUserAsync(teacher.UserId);
        var pushPayload = new PushPayload
        {
            Category = category,
            Screen = DeepLink,
            Badge = unreadNotificationCount
        };
        await SendPushAsync(teacher.UserId, title, body, pushPayload);
    }

    // ════════════════════════════════════════════════
    // PRIVATE HELPERS (mirror PendingPaymentRejectedNotificationJob)
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
