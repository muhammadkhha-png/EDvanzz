using Edvanz.Application.Dtos;
using Edvanz.Application.Dtos.Subscription;
using Edvanz.Application.ServiceContract;
using Edvanz.Domain.Constants;
using Edvanz.Domain.Entities;
using Edvanz.Domain.Interfaces;
using Edvanz.Domain.Resources;
using Microsoft.Extensions.Localization;
using System.Net;

namespace Edvanz.Application.Services;

/// <summary>
/// Bell-icon inbox operations (§4.2 / FR-SUB-050…054).
///
/// SCOPE: PUSH notifications only (D-05). WhatsApp messages live in the
/// Messaging module's MessageLog and are NOT exposed through this service.
///
/// All endpoints backed by this service are whitelisted via [AllowExpiredSubscription]
/// so an expired tutor can still see why their subscription expired.
/// </summary>
public class NotificationHistoryService : INotificationHistoryService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IStringLocalizer<Messages> _localizer;

    public NotificationHistoryService(
        IUnitOfWork unitOfWork,
        IStringLocalizer<Messages> localizer)
    {
        _unitOfWork = unitOfWork;
        _localizer = localizer;
    }

    // ════════════════════════════════════════════════
    // INBOX FEED
    // ════════════════════════════════════════════════

    /// <inheritdoc />
    public async Task<Result<PaginatedResponse<List<NotificationDto>>>> GetPagedAsync(
        long userId, int page, int pageSize)
    {
        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 100) pageSize = 20;

        var (rows, totalCount) = await _unitOfWork.UserNotificationsRepo
            .GetPagedByUserAsync(userId, page, pageSize);

        var dtos = rows.Select(n => new NotificationDto
        {
            Id = n.Id,
            Title = n.Title,
            Body = n.Body,
            DeepLinkPayload = n.DeepLinkPayload,
            SentAt = n.SentAt,
            IsRead = n.IsRead,
            ReadAt = n.ReadAt,
            Category = n.Category
        }).ToList();

        var paged = new PaginatedResponse<List<NotificationDto>>
        {
            data = dtos,
           page = page,
            pageSize = pageSize,
            totalCount = totalCount
        };

        return Result<PaginatedResponse<List<NotificationDto>>>.Success(paged, _localizer);
    }

    /// <inheritdoc />
    public async Task<Result<int>> GetUnreadCountAsync(long userId)
    {
        int count = await _unitOfWork.UserNotificationsRepo.GetUnreadCountByUserAsync(userId);
        return Result<int>.Success(count, _localizer);
    }

    // ════════════════════════════════════════════════
    // STATE TRANSITIONS
    // ════════════════════════════════════════════════

    /// <inheritdoc />
    public async Task<Result<bool>> MarkReadAsync(long userId, long notificationId)
    {
        // Tenant-guarded fetch — a user cannot mark another user's notification.
        var notification = await _unitOfWork.UserNotificationsRepo
            .GetByIdAndUserAsync(notificationId, userId);

        if (notification is null)
        {
            return Result<bool>.Failure(
                _localizer, SubscriptionConstants.Messages.NotificationNotFound, HttpStatusCode.NotFound);
        }

        // Idempotent: re-marking an already-read row is a no-op.
        if (!notification.IsRead)
        {
            notification.IsRead = true;
            notification.ReadAt = DateTime.UtcNow;
            await _unitOfWork.SaveChangesAsync();
        }

        return Result<bool>.Success(true, _localizer, SubscriptionConstants.Messages.NotificationMarkedRead);
    }

    /// <inheritdoc />
    public async Task<Result<int>> MarkAllReadAsync(long userId)
    {
        int updated = await _unitOfWork.UserNotificationsRepo
            .MarkAllReadByUserAsync(userId, DateTime.UtcNow);

        return Result<int>.Success(
            updated, _localizer, SubscriptionConstants.Messages.NotificationsAllMarkedRead);
    }

    // ════════════════════════════════════════════════
    // FCM TOKEN REGISTRATION
    // ════════════════════════════════════════════════

    /// <inheritdoc />
    public async Task<Result<bool>> RegisterFcmTokenAsync(
        long userId, RegisterFcmTokenRequest request)
    {
        // ── Validation ──
        if (string.IsNullOrWhiteSpace(request.Token))
        {
            return Result<bool>.Failure(
                _localizer, SubscriptionConstants.Messages.FcmTokenRequired);
        }

        string token = request.Token.Trim();
        if (token.Length > SubscriptionConstants.FcmTokenMaxLength)
        {
            // Truncate defensively — Firebase tokens are typically <250 chars but
            // future SDK versions could change format.
            token = token[..SubscriptionConstants.FcmTokenMaxLength];
        }

        // ── Upsert on (UserId, FcmToken) ──
        var existing = await _unitOfWork.UserDeviceTokensRepo
            .GetByUserAndTokenAsync(userId, token);

        DateTime now = DateTime.UtcNow;
        if (existing is null)
        {
            await _unitOfWork.UserDeviceTokensRepo.InsertTokenAsync(new UserDeviceToken
            {
                UserId = userId,
                FcmToken = token,
                Platform = request.Platform,
                RegisteredAt = now,
                LastSeenAt = now,
                IsActive = true,
                CreateAt = now
            });
        }
        else
        {
            existing.LastSeenAt = now;
            existing.IsActive = true;
            existing.Platform = request.Platform;
        }

        await _unitOfWork.SaveChangesAsync();

        return Result<bool>.Success(true, _localizer, SubscriptionConstants.Messages.FcmTokenRegistered);
    }
}