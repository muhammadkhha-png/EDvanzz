using Edvanz.API.Attributes;
using Edvanz.Application.Dtos.Chat;
using Edvanz.Application.IservicesContract;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Edvanz.API.Controllers;

/// <summary>
/// 1:1 direct-chat subsystem — all six chat endpoints for every account type.
///
/// AUTHORIZATION:
///   Class-level [Authorize] requires a valid JWT.
///   Class-level [AllowExpiredSubscription] keeps chat accessible when a teacher's
///   subscription has lapsed (same rationale as NotificationsController — chat is
///   a communication lifeline, not a feature that should be subscription-gated).
///
/// CALLER IDENTITY:
///   callerUserId is ALWAYS derived from _currentUser.UserId (JWT NameIdentifier).
///   It is NEVER read from the request body or route — doing so would be an IDOR
///   vulnerability (CLAUDE.md §3.3). The service layer enforces the eligibility gate:
///   adult↔adult open; any conversation touching a Student requires an active link.
///
/// ONE-WAY ENFORCEMENT (AAM-BR-08 override):
///   Both participants can send on every conversation — one-way restriction was
///   deliberately removed in favour of bidirectional chat (product decision, confirmed).
///   No send-restriction logic appears in this controller.
/// </summary>
[Route("api/chat")]
[Authorize]
[AllowExpiredSubscription]
public sealed class ChatController : ApiBaseController
{
    private readonly IChatService _chatService;
    private readonly ICurrentUserService _currentUser;

    public ChatController(IChatService chatService, ICurrentUserService currentUser)
    {
        _chatService = chatService;
        _currentUser = currentUser;
    }

    // ══════════════════════════════════════════════════════════════════════════
    // ENDPOINT 1 — START OR GET CONVERSATION
    // POST /api/chat/conversations
    // ══════════════════════════════════════════════════════════════════════════
    //
    // WHAT IT DOES:
    //   Opens a new 1:1 conversation between the caller and TargetUserId, or returns
    //   the existing one if it already exists (idempotent get-or-create).
    //   Applies the eligibility gate:
    //     - Adult↔adult: always allowed.
    //     - Student-involving: requires an active StudentTeacherLink / ParentChild link.
    //     - Student↔Student: not supported in v1.
    //
    // TABLES WRITTEN: Conversations (on first call for this pair)
    // TABLES READ:    Users (UserType), StudentTeacherLinks, ParentChild, Assistants
    //
    // SAMPLE REQUEST:
    //   POST /api/chat/conversations
    //   { "targetUserId": 42 }
    //
    // SAMPLE RESPONSE (201 first call / 200 subsequent):
    //   { "success": true, "data": { "conversationId": 7, "otherParticipantName": "...", ... } }
    //
    // ══════════════════════════════════════════════════════════════════════════
    [HttpPost("conversations")]
    [ProducesResponseType(typeof(Edvanz.Application.Dtos.Result<Edvanz.Application.Dtos.Chat.ConversationDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Edvanz.Application.Dtos.Result<Edvanz.Application.Dtos.Chat.ConversationDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(object), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(object), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> StartConversation(
        [FromBody] StartConversationRequest request)
    {
        long? callerUserId = _currentUser.UserId;
        if (callerUserId is null) return UserNotResolved();

        var result = await _chatService
            .GetOrCreateConversationAsync(callerUserId.Value, request.TargetUserId);

        return ToResponse(result);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // ENDPOINT 2 — SEND MESSAGE
    // POST /api/chat/conversations/{conversationId}/messages
    // ══════════════════════════════════════════════════════════════════════════
    //
    // WHAT IT DOES:
    //   Sends a message in an existing conversation. The caller must be one of the
    //   two participants — the service validates this before any write. Persists
    //   the ChatMessage row, refreshes the conversation's denormalized last-message
    //   fields, and enqueues an FCM push job for the recipient (fire-and-forget on
    //   the "notifications" queue, 30 s target latency).
    //
    // TABLES WRITTEN: ChatMessages (1 row), Conversations (LastMessageAt/Preview/Sender)
    // TABLES READ:    Conversations (participant guard), Users (sender name for push)
    //
    // SAMPLE REQUEST:
    //   POST /api/chat/conversations/7/messages
    //   { "body": "مرحباً، موعد الحصة القادمة؟" }
    //
    // ══════════════════════════════════════════════════════════════════════════
    [HttpPost("conversations/{conversationId:long}/messages")]
    [EnableRateLimiting("chat-send")]
    [ProducesResponseType(typeof(Edvanz.Application.Dtos.Result<Edvanz.Application.Dtos.Chat.ChatMessageDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(object), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> SendMessage(
           [FromRoute] long conversationId,
           [FromBody] SendMessageRequest request)
    {
        long? callerUserId = _currentUser.UserId;
        if (callerUserId is null) return UserNotResolved();

        var result = await _chatService
            .SendMessageAsync(callerUserId.Value, conversationId, request.Body);

        return ToResponse(result);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // ENDPOINT 3 — GET CONVERSATION LIST
    // GET /api/chat/conversations?page=1&pageSize=20
    // ══════════════════════════════════════════════════════════════════════════
    //
    // WHAT IT DOES:
    //   Returns the caller's conversation list ordered by most-recent message first.
    //   Each entry includes the other participant's name, role, last-message preview,
    //   last-message timestamp, whether the last message was sent by the caller, and
    //   the per-conversation unread count (for the Flutter badge on each list tile).
    //
    // TABLES READ: Conversations (participant A/B indexes), Users (names/types),
    //              ChatMessages (unread correlated subquery)
    //
    // SAMPLE: GET /api/chat/conversations?page=1&pageSize=20
    //
    // ══════════════════════════════════════════════════════════════════════════
    [HttpGet("conversations")]
    [ProducesResponseType(typeof(Edvanz.Application.Dtos.Result<Edvanz.Application.Dtos.PaginatedResponse<System.Collections.Generic.List<Edvanz.Application.Dtos.Chat.ConversationDto>>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetConversations(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        long? callerUserId = _currentUser.UserId;
        if (callerUserId is null) return UserNotResolved();

        var result = await _chatService
            .GetConversationListAsync(callerUserId.Value, page, pageSize);

        return ToResponse(result);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // ENDPOINT 4 — GET MESSAGE THREAD
    // GET /api/chat/conversations/{conversationId}/messages?page=1&pageSize=30
    // ══════════════════════════════════════════════════════════════════════════
    //
    // WHAT IT DOES:
    //   Returns the paginated message thread for a conversation.
    //   Page 1 = most recent messages (DESC by SentAt). Flutter reverses the list
    //   for natural top-to-bottom display and appends older pages on scroll-up.
    //   Validates that the caller is a participant — returns 404 to any non-member
    //   (no information disclosure about conversation existence).
    //   Does NOT mark messages as read — that is an explicit action (Endpoint 5).
    //
    // TABLES READ: Conversations (participant guard), ChatMessages (thread),
    //              Users (sender names, batched in one round-trip)
    //
    // SAMPLE: GET /api/chat/conversations/7/messages?page=1&pageSize=30
    //
    // ══════════════════════════════════════════════════════════════════════════
    [HttpGet("conversations/{conversationId:long}/messages")]
    [ProducesResponseType(typeof(Edvanz.Application.Dtos.Result<Edvanz.Application.Dtos.PaginatedResponse<System.Collections.Generic.List<Edvanz.Application.Dtos.Chat.ChatMessageDto>>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetThread(
        [FromRoute] long conversationId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 30)
    {
        long? callerUserId = _currentUser.UserId;
        if (callerUserId is null) return UserNotResolved();

        var result = await _chatService
            .GetThreadAsync(callerUserId.Value, conversationId, page, pageSize);

        return ToResponse(result);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // ENDPOINT 5 — MARK CONVERSATION READ
    // PUT /api/chat/conversations/{conversationId}/mark-read
    // ══════════════════════════════════════════════════════════════════════════
    //
    // WHAT IT DOES:
    //   Marks all unread messages sent by the OTHER participant in this conversation
    //   as read for the calling user. Idempotent: calling it on an already-read
    //   conversation returns 0 rows changed with no error.
    //   Returns the count of rows transitioned (useful for badge decrement on mobile).
    //
    // TABLES WRITTEN: ChatMessages (IsRead, ReadAt — ExecuteUpdateAsync, no entity load)
    // TABLES READ:    Conversations (participant guard)
    //
    // Flutter integration: call this when the user opens a conversation thread.
    //
    // SAMPLE: PUT /api/chat/conversations/7/mark-read
    //
    // ══════════════════════════════════════════════════════════════════════════
    [HttpPut("conversations/{conversationId:long}/mark-read")]
    [ProducesResponseType(typeof(Edvanz.Application.Dtos.Result<int>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> MarkRead([FromRoute] long conversationId)
    {
        long? callerUserId = _currentUser.UserId;
        if (callerUserId is null) return UserNotResolved();

        var result = await _chatService.MarkReadAsync(callerUserId.Value, conversationId);
        return ToResponse(result);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // ENDPOINT 6 — GET TOTAL UNREAD COUNT
    // GET /api/chat/unread-count
    // ══════════════════════════════════════════════════════════════════════════
    //
    // WHAT IT DOES:
    //   Returns the total count of unread chat messages across ALL conversations
    //   for the calling user. Backs the chat tab-bar badge on Flutter's bottom
    //   navigation bar. Single SQL COUNT with an INNER JOIN — fast at any scale.
    //
    // TABLES READ: Conversations (participant filter), ChatMessages (unread count)
    //
    // Flutter integration: poll on app foreground / FCM data-message receipt to
    //   refresh the tab badge. Do NOT poll on a timer — use FCM data pushes instead.
    //
    // SAMPLE: GET /api/chat/unread-count
    //   → { "success": true, "data": 3 }
    //
    // ══════════════════════════════════════════════════════════════════════════
    [HttpGet("unread-count")]
    [ProducesResponseType(typeof(Edvanz.Application.Dtos.Result<int>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetUnreadCount()
    {
        long? callerUserId = _currentUser.UserId;
        if (callerUserId is null) return UserNotResolved();

        var result = await _chatService.GetUnreadCountAsync(callerUserId.Value);
        return ToResponse(result);
    }
}