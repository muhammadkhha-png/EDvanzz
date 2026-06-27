using Edvanz.Application.Dtos;
using Edvanz.Application.Dtos.Chat;
using Edvanz.Application.IservicesContract;
using Edvanz.Domain.Entities.Chat;
using Edvanz.Domain.Enums;
using Edvanz.Domain.Interfaces;
using Edvanz.Domain.Resources;
using Microsoft.Extensions.Localization;
using System.Net;

namespace Edvanz.Application.Services;

/// <summary>
/// 1:1 direct-chat service. Implements <see cref="IChatService"/>.
///
/// COMMIT BOUNDARY: InsertConversationAsync, InsertMessageAsync, and the
/// tracked-entity mutation on the Conversation are all staged and committed in a
/// single SaveChangesAsync call. No internal SaveChanges inside helpers (§5.2).
///
/// PUSH DISPATCH: IChatPushDispatcher.Dispatch is called AFTER the commit so that
/// the push job runs against an already-durable message. A push failure never rolls
/// back the message.
///
/// ELIGIBILITY GATE: CheckEligibilityAsync is called before any conversation is
/// created or accessed. See IChatService XML doc for the full rule set.
/// </summary>
public sealed class ChatService : IChatService
{
    private const int MaxBodyLength = 4000;
    private const int PreviewTruncateLength = 100;

    private readonly IUnitOfWork _unitOfWork;
    private readonly IChatPushDispatcher _dispatcher;
    private readonly IStringLocalizer<Messages> _localizer;

    public ChatService(
        IUnitOfWork unitOfWork,
        IChatPushDispatcher dispatcher,
        IStringLocalizer<Messages> localizer)
    {
        _unitOfWork = unitOfWork;
        _dispatcher = dispatcher;
        _localizer = localizer;
    }

    // ════════════════════════════════════════════════
    // GET OR CREATE CONVERSATION
    // ════════════════════════════════════════════════

    /// <inheritdoc />
    public async Task<Result<ConversationDto>> GetOrCreateConversationAsync(
        long callerUserId, long targetUserId)
    {
        if (callerUserId == targetUserId)
            return Result<ConversationDto>.Failure(
                _localizer, "Chat_CannotMessageSelf", HttpStatusCode.BadRequest);

        // ── Eligibility gate (resolves both UserTypes + target display name) ──
        var eligibility = await CheckEligibilityAsync(callerUserId, targetUserId);
        if (!eligibility.Allowed)
            return Result<ConversationDto>.Failure(
                _localizer, eligibility.ErrorKey!, HttpStatusCode.Forbidden);

        // ── Canonical ordering: smaller Id is always ParticipantA ──
        long a = Math.Min(callerUserId, targetUserId);
        long b = Math.Max(callerUserId, targetUserId);

        // ── Idempotent get-or-create ──
        var existing = await _unitOfWork.ChatRepo.GetByParticipantsAsync(a, b);
        if (existing is not null)
            return Result<ConversationDto>.Success(
                MapConversation(existing, callerUserId,
                    eligibility.TargetName!, eligibility.TargetUserType!.Value),
                _localizer);

        var now = DateTime.UtcNow;
        var conversation = new Conversation
        {
            ParticipantAUserId = a,
            ParticipantBUserId = b,
            CreateAt = now
        };

        await _unitOfWork.ChatRepo.InsertConversationAsync(conversation);
        await _unitOfWork.SaveChangesAsync();

        return Result<ConversationDto>.Success(
            MapConversation(conversation, callerUserId,
                eligibility.TargetName!, eligibility.TargetUserType!.Value),
            _localizer, "Chat_ConversationCreated", HttpStatusCode.Created);
    }

    // ════════════════════════════════════════════════
    // SEND MESSAGE
    // ════════════════════════════════════════════════

    /// <inheritdoc />
    public async Task<Result<ChatMessageDto>> SendMessageAsync(
        long callerUserId, long conversationId, string body)
    {
        // ── Validate body ──
        body = body?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(body))
            return Result<ChatMessageDto>.Failure(
                _localizer, "Chat_MessageBodyRequired", HttpStatusCode.BadRequest);

        if (body.Length > MaxBodyLength)
            return Result<ChatMessageDto>.Failure(
                _localizer, "Chat_MessageBodyTooLong", HttpStatusCode.BadRequest);

        // ── Participant guard — returns TRACKED entity so we can mutate it ──
        var conversation = await _unitOfWork.ChatRepo
            .GetByIdAndParticipantAsync(conversationId, callerUserId);

        if (conversation is null)
            return Result<ChatMessageDto>.Failure(
                _localizer, "Chat_ConversationNotFound", HttpStatusCode.NotFound);

        long recipientUserId = conversation.ParticipantAUserId == callerUserId
            ? conversation.ParticipantBUserId
            : conversation.ParticipantAUserId;

        // ── Resolve sender name before commit (needed for push payload) ──
        string senderName =
            await _unitOfWork.Users.GetUserFullNameByUserIdAsync(callerUserId)
            ?? "Unknown";

        DateTime now = DateTime.UtcNow;
        string preview = body.Length <= PreviewTruncateLength
            ? body
            : string.Concat(body.AsSpan(0, PreviewTruncateLength), "…");

        // ── Stage message ──
        var message = new ChatMessage
        {
            ConversationId = conversationId,
            SenderUserId = callerUserId,
            Body = body,
            SentAt = now,
            IsRead = false,
            CreateAt = now
        };
        await _unitOfWork.ChatRepo.InsertMessageAsync(message);

        // ── Update conversation's denormalized last-message fields (tracked) ──
        conversation.LastMessageAt = now;
        conversation.LastMessagePreview = preview;
        conversation.LastMessageSenderUserId = callerUserId;

        // ── Single commit — both writes land atomically ──
        await _unitOfWork.SaveChangesAsync();

        // ── Fire-and-forget push after durable commit ──
        _dispatcher.Dispatch(conversationId, senderName, preview, recipientUserId);

        return Result<ChatMessageDto>.Success(
            MapMessage(message, senderName, callerUserId),
            _localizer, "Chat_MessageSent", HttpStatusCode.Created);
    }

    // ════════════════════════════════════════════════
    // CONVERSATION LIST
    // ════════════════════════════════════════════════

    /// <inheritdoc />
    public async Task<Result<PaginatedResponse<List<ConversationDto>>>> GetConversationListAsync(
        long callerUserId, int page, int pageSize)
    {
        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 50) pageSize = 20;

        var (rows, totalCount) = await _unitOfWork.ChatRepo
            .GetConversationListAsync(callerUserId, page, pageSize);

        var dtos = rows.Select(r => new ConversationDto
        {
            ConversationId = r.ConversationId,
            OtherParticipantUserId = r.OtherParticipantUserId,
            OtherParticipantName = r.OtherParticipantName,
            OtherParticipantRole = r.OtherParticipantUserType.ToString(),
            LastMessagePreview = r.LastMessagePreview,
            LastMessageAt = r.LastMessageAt,
            IsLastMessageMine = r.LastMessageSenderUserId == callerUserId,
            UnreadCount = r.UnreadCount
        }).ToList();

        return Result<PaginatedResponse<List<ConversationDto>>>.Success(
            new PaginatedResponse<List<ConversationDto>>
            {
                data = dtos,
                page = page,
                pageSize = pageSize,
                totalCount = totalCount,
                totalPages = (int)Math.Ceiling((double)totalCount / pageSize)
            },
            _localizer);
    }

    // ════════════════════════════════════════════════
    // MESSAGE THREAD
    // ════════════════════════════════════════════════

    /// <inheritdoc />
    public async Task<Result<PaginatedResponse<List<ChatMessageDto>>>> GetThreadAsync(
        long callerUserId, long conversationId, int page, int pageSize)
    {
        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 100) pageSize = 30;

        // Participant guard — read-only check (AsNoTracking acceptable here; we don't mutate).
        var conversation = await _unitOfWork.ChatRepo
            .GetByIdAndParticipantAsync(conversationId, callerUserId);

        if (conversation is null)
            return Result<PaginatedResponse<List<ChatMessageDto>>>.Failure(
                _localizer, "Chat_ConversationNotFound", HttpStatusCode.NotFound);

        var (messages, totalCount) = await _unitOfWork.ChatRepo
            .GetThreadAsync(conversationId, page, pageSize);

        // ── Resolve sender names in one batch query (max 2 distinct senders in 1:1) ──
        var senderIds = messages.Select(m => m.SenderUserId).Distinct();
        var nameMap = await _unitOfWork.Users.GetUserFullNamesByUserIdsAsync(senderIds);

        var dtos = messages.Select(m => new ChatMessageDto
        {
            MessageId = m.Id,
            ConversationId = m.ConversationId,
            SenderUserId = m.SenderUserId,
            SenderName = nameMap.GetValueOrDefault(m.SenderUserId, "Unknown"),
            Body = m.Body,
            SentAt = m.SentAt,
            IsRead = m.IsRead,
            ReadAt = m.ReadAt,
            IsFromMe = m.SenderUserId == callerUserId
        }).ToList();

        return Result<PaginatedResponse<List<ChatMessageDto>>>.Success(
            new PaginatedResponse<List<ChatMessageDto>>
            {
                data = dtos,
                page = page,
                pageSize = pageSize,
                totalCount = totalCount,
                totalPages = (int)Math.Ceiling((double)totalCount / pageSize)
            },
            _localizer);
    }

    // ════════════════════════════════════════════════
    // MARK READ
    // ════════════════════════════════════════════════

    /// <inheritdoc />
    public async Task<Result<int>> MarkReadAsync(long callerUserId, long conversationId)
    {
        var conversation = await _unitOfWork.ChatRepo
            .GetByIdAndParticipantAsync(conversationId, callerUserId);

        if (conversation is null)
            return Result<int>.Failure(
                _localizer, "Chat_ConversationNotFound", HttpStatusCode.NotFound);

        int updated = await _unitOfWork.ChatRepo
            .MarkConversationReadAsync(conversationId, callerUserId, DateTime.UtcNow);

        return Result<int>.Success(updated, _localizer, "Chat_MarkedRead");
    }

    // ════════════════════════════════════════════════
    // UNREAD COUNT
    // ════════════════════════════════════════════════

    /// <inheritdoc />
    public async Task<Result<int>> GetUnreadCountAsync(long callerUserId)
    {
        int count = await _unitOfWork.ChatRepo.GetTotalUnreadCountAsync(callerUserId);
        return Result<int>.Success(count, _localizer);
    }

    // ════════════════════════════════════════════════
    // ELIGIBILITY GATE (PRIVATE)
    // ════════════════════════════════════════════════

    private async Task<EligibilityResult> CheckEligibilityAsync(
        long callerUserId, long targetUserId)
    {
        var callerType = await _unitOfWork.Users.GetUserTypeByUserIdAsync(callerUserId);
        if (callerType is null)
            return EligibilityResult.Deny("Chat_CallerNotFound");

        var targetType = await _unitOfWork.Users.GetUserTypeByUserIdAsync(targetUserId);
        if (targetType is null)
            return EligibilityResult.Deny("Chat_TargetNotFound");

        // Pre-resolve display name; used in the ConversationDto regardless of gate outcome.
        string targetName =
            await _unitOfWork.Users.GetUserFullNameByUserIdAsync(targetUserId) ?? "Unknown";

        bool callerIsStudent = callerType == UserType.Student;
        bool targetIsStudent = targetType == UserType.Student;

        // Adult↔adult — open, no link check required.
        if (!callerIsStudent && !targetIsStudent)
            return EligibilityResult.Allow(targetName, targetType.Value);

        // Student↔Student — disabled in v1.
        if (callerIsStudent && targetIsStudent)
            return EligibilityResult.Deny("Chat_StudentToStudentNotSupported");

        // One side is a Student — determine which and check the link graph.
        long studentUserId = callerIsStudent ? callerUserId : targetUserId;
        long otherUserId = callerIsStudent ? targetUserId : callerUserId;
        UserType otherType = callerIsStudent ? targetType.Value : callerType.Value;

        bool linked = otherType switch
        {
            UserType.Teacher => await _unitOfWork.Users
                .AreStudentAndTeacherLinkedByUserIdsAsync(studentUserId, otherUserId),

            UserType.Parent => await _unitOfWork.Users
                .AreStudentAndParentLinkedByUserIdsAsync(studentUserId, otherUserId),

            UserType.Assistant => await _unitOfWork.Users
                .AreStudentAndAssistantLinkedByUserIdsAsync(studentUserId, otherUserId),

            // SuperAdmin or any unrecognized type — no chat with students.
            _ => false
        };

        return linked
            ? EligibilityResult.Allow(targetName, targetType.Value)
            : EligibilityResult.Deny("Chat_NoLinkFound");
    }

    // ════════════════════════════════════════════════
    // MAPPING HELPERS (PRIVATE / STATIC)
    // ════════════════════════════════════════════════

    private static ConversationDto MapConversation(
        Conversation conv, long callerUserId, string otherName, UserType otherType)
        => new()
        {
            ConversationId = conv.Id,
            OtherParticipantUserId = conv.ParticipantAUserId == callerUserId
                                        ? conv.ParticipantBUserId
                                        : conv.ParticipantAUserId,
            OtherParticipantName = otherName,
            OtherParticipantRole = otherType.ToString(),
            LastMessagePreview = conv.LastMessagePreview,
            LastMessageAt = conv.LastMessageAt,
            IsLastMessageMine = conv.LastMessageSenderUserId == callerUserId,
            UnreadCount = 0  // not computed on this path; populated by list query
        };

    private static ChatMessageDto MapMessage(
        ChatMessage msg, string senderName, long callerUserId)
        => new()
        {
            MessageId = msg.Id,
            ConversationId = msg.ConversationId,
            SenderUserId = msg.SenderUserId,
            SenderName = senderName,
            Body = msg.Body,
            SentAt = msg.SentAt,
            IsRead = msg.IsRead,
            ReadAt = msg.ReadAt,
            IsFromMe = msg.SenderUserId == callerUserId
        };

    // ════════════════════════════════════════════════
    // ELIGIBILITY RESULT (PRIVATE VALUE TYPE)
    // ════════════════════════════════════════════════

    private sealed record EligibilityResult(
        bool Allowed,
        string? ErrorKey,
        string? TargetName,
        UserType? TargetUserType)
    {
        public static EligibilityResult Allow(string name, UserType type) =>
            new(true, null, name, type);

        public static EligibilityResult Deny(string errorKey) =>
            new(false, errorKey, null, null);
    }
}