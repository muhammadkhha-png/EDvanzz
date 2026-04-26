using Edvanz.Application.Dtos;
using Edvanz.Application.IservicesContract;
using Edvanz.Application.ServiceContract;
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
/// Subscription reminder dispatcher + per-teacher worker (§7).
///
/// SEPARATION FROM HANGFIRE:
///   This service does NOT inject IBackgroundJobClient. The Phase 08 dispatcher job
///   calls GetTeachersDueForReminderAsync, gets the eligibility list, and performs
///   the per-teacher Enqueue calls itself. Keeping the fan-out in the job (not in
///   the service) means this class is unit-testable without a Hangfire fixture
///   and the Application layer has no compile-time dependency on the job framework.
///
/// WORKER (SendReminderAsync):
///   Invoked by ISubscriptionReminderJob. Idempotency check first — if the
///   SubscriptionAlert row for (teacherId, endDate.Date, alertDay) already exists,
///   exit successfully without resending (FR-SUB-014).
///
///   On a fresh send: dispatch push to every active FCM token, insert
///   UserNotification + SubscriptionAlert rows. WhatsApp is pessimistically
///   disabled until v2 wires IMessageDispatcher into this flow.
///
/// CONCURRENCY:
///   The unique index on SubscriptionAlerts (TeacherId, SubscriptionEndDate, AlertDay)
///   is the hard guarantee against duplicate sends. If two workers race past
///   HasAlertBeenSentAsync, the second InsertAsync + SaveChanges fails with 2601;
///   we catch and exit successfully (the user already got notified by worker #1).
/// </summary>
public class SubscriptionReminderService : ISubscriptionReminderService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPushNotificationSender _pushSender;
    private readonly IStringLocalizer<Messages> _localizer;
    private readonly ILogger<SubscriptionReminderService> _logger;

    public SubscriptionReminderService(
        IUnitOfWork unitOfWork,
        IPushNotificationSender pushSender,
        IStringLocalizer<Messages> localizer,
        ILogger<SubscriptionReminderService> logger)
    {
        _unitOfWork = unitOfWork;
        _pushSender = pushSender;
        _localizer = localizer;
        _logger = logger;
    }

    // ════════════════════════════════════════════════
    // DISPATCHER ELIGIBILITY SCAN (§7.2)
    // ════════════════════════════════════════════════

    /// <inheritdoc />
    public async Task<Result<IReadOnlyList<UpcomingExpiryProjection>>> GetTeachersDueForReminderAsync()
    {
        DateTime today = DateTime.UtcNow.Date;

        var eligibleTeachers = await _unitOfWork.Users
            .GetTeachersWithUpcomingExpiryAsync(today);

        _logger.LogInformation(
            "Reminder dispatcher: {Count} teachers eligible on {Date}",
            eligibleTeachers.Count, today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));

        return Result<IReadOnlyList<UpcomingExpiryProjection>>.Success(eligibleTeachers, _localizer);
    }

    // ════════════════════════════════════════════════
    // WORKER (§7.3)
    // ════════════════════════════════════════════════

    /// <inheritdoc />
    public async Task<Result<bool>> SendReminderAsync(long teacherId, DateTime endDate, int alertDay)
    {
        // ── Idempotency pre-check (cheap; the unique index is the hard guarantee) ──
        bool alreadySent = await _unitOfWork.SubscriptionAlertsRepo
            .HasAlertBeenSentAsync(teacherId, endDate, alertDay);

        if (alreadySent)
        {
            _logger.LogDebug(
                "Reminder already sent for teacher {TeacherId} endDate {EndDate} day {AlertDay} — skipping",
                teacherId, endDate.ToString("yyyy-MM-dd"), alertDay);
            return Result<bool>.Success(true, _localizer);
        }

        // ── Load teacher data the worker needs ──
        var teacher = await _unitOfWork.Users.GetTeacherForReminderAsync(teacherId);
        if (teacher is null)
        {
            _logger.LogInformation(
                "Teacher {TeacherId} no longer exists — skipping reminder", teacherId);
            return Result<bool>.Success(true, _localizer);
        }

        // ── Render localized title/body ──
        SetCurrentCulture(teacher.LanguagePreference);
        (string title, string body) = RenderReminderContent(endDate, alertDay);

        // ── Send across channels and aggregate which succeeded ──
        SubscriptionAlertChannel channelsSent = SubscriptionAlertChannel.None;

        bool pushSucceeded = await SendPushAsync(teacher.UserId, title, body);
        if (pushSucceeded) channelsSent |= SubscriptionAlertChannel.Push;

        // WhatsApp deliberately disabled until v2 (per directive). Real WhatsApp
        // dispatch goes through the existing IMessageDispatcher with a configured
        // SubscriptionExpiry trigger + template. Until that integration ships,
        // ChannelsSent will never carry the WhatsApp bit.
        // bool whatsAppSucceeded = await SendWhatsAppAsync(teacher, body);
        // if (whatsAppSucceeded) channelsSent |= SubscriptionAlertChannel.WhatsApp;

        // ── Persist UserNotification (push record only — D-05) + SubscriptionAlert ──
        // The unique index on (TeacherId, EndDate.Date, AlertDay) is the race winner.
        // If another worker beat us to it, SaveChanges throws on 2601 — catch + exit success.
        try
        {
            await _unitOfWork.UserNotificationsRepo.InsertNotificationAsync(new UserNotification
            {
                UserId = teacher.UserId,
                Title = title,
                Body = body,
                DeepLinkPayload = "/subscription/renew",
                SentAt = DateTime.UtcNow,
                IsRead = false,
                Category = NotificationCategory.SubscriptionReminder,
                CreateAt = DateTime.UtcNow
            });

            await _unitOfWork.SubscriptionAlertsRepo.InsertAlertAsync(new SubscriptionAlert
            {
                TeacherId = teacherId,
                SubscriptionEndDate = endDate.Date,
                AlertDay = alertDay,
                SentAt = DateTime.UtcNow,
                ChannelsSent = channelsSent,
                CreateAt = DateTime.UtcNow
            });

            await _unitOfWork.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            // Another worker won the race. The teacher already received the notification.
            _logger.LogInformation(
                "SubscriptionAlert unique-violation for teacher {TeacherId} day {AlertDay} — competing worker won",
                teacherId, alertDay);
            return Result<bool>.Success(true, _localizer);
        }

        return Result<bool>.Success(true, _localizer);
    }

    // ════════════════════════════════════════════════
    // PRIVATE: CONTENT RENDERING
    // ════════════════════════════════════════════════

    /// <summary>
    /// Picks the right title/body localization keys for the alert day.
    /// AlertDay 0 → "ExpiresToday" body. AlertDay 1..5 → "DaysRemaining" body
    /// formatted with the day count and end date.
    /// </summary>
    private (string Title, string Body) RenderReminderContent(DateTime endDate, int alertDay)
    {
        string title = _localizer[SubscriptionConstants.Messages.SubscriptionReminderTitle];

        if (alertDay <= 0)
        {
            string expiresTodayBody = _localizer[
                SubscriptionConstants.Messages.SubscriptionReminderBodyExpiresToday];
            return (title, expiresTodayBody);
        }

        string template = _localizer[
            SubscriptionConstants.Messages.SubscriptionReminderBodyDaysRemaining];
        string body = string.Format(
            CultureInfo.CurrentCulture,
            template,
            alertDay,
            endDate.ToString("yyyy-MM-dd", CultureInfo.CurrentCulture));

        return (title, body);
    }

    /// <summary>
    /// Switches the current thread's UI culture to the teacher's preference so the
    /// IStringLocalizer pulls the right .resx file. Defaults to "en" when null/empty
    /// or the value is not one of the supported cultures.
    /// </summary>
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

    // ════════════════════════════════════════════════
    // PRIVATE: PUSH FAN-OUT
    // ════════════════════════════════════════════════

    /// <summary>
    /// Sends the push to every active FCM token for the user. Returns true if AT LEAST
    /// ONE token accepted the message — that's enough to count Push as successful for
    /// SubscriptionAlertChannel purposes. Tokens reported as unregistered are flipped
    /// to IsActive = false (EC-11).
    /// </summary>
    private async Task<bool> SendPushAsync(long userId, string title, string body)
    {
        var tokens = await _unitOfWork.UserDeviceTokensRepo.GetActiveTokensForUserAsync(userId);
        if (tokens.Count == 0) return false;

        bool anySucceeded = false;
        foreach (var token in tokens)
        {
            var result = await _pushSender.SendAsync(token.FcmToken, title, body, "/subscription/renew");

            if (result.Success)
            {
                anySucceeded = true;
                continue;
            }

            if (result.ShouldDeactivateToken)
            {
                // EC-11: stale FCM token — deactivate so future runs skip it.
                await _unitOfWork.UserDeviceTokensRepo.DeactivateTokenAsync(token.Id);
            }
        }

        return anySucceeded;
    }

    // ════════════════════════════════════════════════
    // PRIVATE: UNIQUE-VIOLATION DETECTION (mirrors SubscriptionService)
    // ════════════════════════════════════════════════

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