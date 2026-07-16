using Edvanz.API.Attributes;
using Edvanz.Application.Dtos.Subscription;
using Edvanz.Application.IservicesContract;
using Edvanz.Application.ServiceContract;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Net;

namespace Edvanz.API.Controllers;

/// <summary>
/// Bell-icon inbox + FCM token registration (§4.2 / FR-SUB-050…054).
///
/// SCOPE: PUSH notifications only (D-05). WhatsApp messages live in the
/// Messaging module's MessageLog and are NOT exposed through this controller.
///
/// AUTHORIZATION:
///   [Authorize] required; [AllowExpiredSubscription] applied class-wide so an
///   expired tutor can still see "your subscription expired" in the inbox.
/// </summary>
[Route("api/notifications")]
[Authorize]
[AllowExpiredSubscription]
public class NotificationsController : ApiBaseController
{
    private readonly INotificationHistoryService _notificationService;
    private readonly ICurrentUserService _currentUser;

    public NotificationsController(
        INotificationHistoryService notificationService,
        ICurrentUserService currentUser)
    {
        _notificationService = notificationService;
        _currentUser = currentUser;
    }

    // ══════════════════════════════════════════════════════════════════════════
    // ENDPOINT 1: GET PAGINATED INBOX (FR-SUB-050 / FR-SUB-051)
    // ══════════════════════════════════════════════════════════════════════════
    //
    // SAMPLE: GET /api/notifications?page=1&pageSize=20
    //
    // ══════════════════════════════════════════════════════════════════════════
    [HttpGet]
    [ProducesResponseType(typeof(Edvanz.Application.Dtos.Result<Edvanz.Application.Dtos.PaginatedResponse<System.Collections.Generic.List<Edvanz.Application.Dtos.Subscription.NotificationDto>>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPaged(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        long? userId = _currentUser.UserId;
        if (userId is null) return UserNotResolved();

        var result = await _notificationService.GetPagedAsync(userId.Value, page, pageSize);
        return ToResponse(result);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // ENDPOINT 2: GET UNREAD COUNT (FR-SUB-052)
    // ══════════════════════════════════════════════════════════════════════════
    //
    // SAMPLE: GET /api/notifications/unread-count
    //
    // ══════════════════════════════════════════════════════════════════════════
    [HttpGet("unread-count")]
    [ProducesResponseType(typeof(Edvanz.Application.Dtos.Result<int>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetUnreadCount()
    {
        long? userId = _currentUser.UserId;
        if (userId is null) return UserNotResolved();

        var result = await _notificationService.GetUnreadCountAsync(userId.Value);
        return ToResponse(result);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // ENDPOINT 3: MARK SINGLE READ (FR-SUB-053)
    // ══════════════════════════════════════════════════════════════════════════
    //
    // SAMPLE: PUT /api/notifications/12345/mark-read
    //
    // ══════════════════════════════════════════════════════════════════════════
    [HttpPut("{notificationId:long}/mark-read")]
    [ProducesResponseType(typeof(Edvanz.Application.Dtos.Result<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> MarkRead([FromRoute] long notificationId)
    {
        long? userId = _currentUser.UserId;
        if (userId is null) return UserNotResolved();

        var result = await _notificationService.MarkReadAsync(userId.Value, notificationId);
        return ToResponse(result);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // ENDPOINT 4: MARK ALL READ (FR-SUB-053)
    // ══════════════════════════════════════════════════════════════════════════
    //
    // SAMPLE: PUT /api/notifications/mark-all-read
    //
    // ══════════════════════════════════════════════════════════════════════════
    [HttpPut("mark-all-read")]
    [ProducesResponseType(typeof(Edvanz.Application.Dtos.Result<int>), StatusCodes.Status200OK)]
    public async Task<IActionResult> MarkAllRead()
    {
        long? userId = _currentUser.UserId;
        if (userId is null) return UserNotResolved();

        var result = await _notificationService.MarkAllReadAsync(userId.Value);
        return ToResponse(result);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // ENDPOINT 5: REGISTER FCM TOKEN (FR-SUB-054)
    // ══════════════════════════════════════════════════════════════════════════
    //
    // WHAT IT DOES:
    //   Upserts the user's FCM device token. Idempotent on (UserId, FcmToken).
    //
    // TABLES WRITTEN: UserDeviceTokens
    //
    // SAMPLE: POST /api/notifications/register-fcm-token
    //   { "token": "fGhJ…long-fcm-token", "platform": "Android" }
    //
    // ══════════════════════════════════════════════════════════════════════════
    [HttpPost("register-fcm-token")]
    [ProducesResponseType(typeof(Edvanz.Application.Dtos.Result<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> RegisterFcmToken([FromBody] RegisterFcmTokenRequest request)
    {
        long? userId = _currentUser.UserId;
        if (userId is null) return UserNotResolved();

        var result = await _notificationService.RegisterFcmTokenAsync(userId.Value, request);
        return ToResponse(result);
    }

    // ════════════════════════════════════════════════
    // PRIVATE HELPERS
    // ════════════════════════════════════════════════

    private IActionResult UserNotResolved()
    {
        return new ObjectResult(new { success = false, message = "User not resolved" })
        {
            StatusCode = (int)HttpStatusCode.Unauthorized
        };
    }
}