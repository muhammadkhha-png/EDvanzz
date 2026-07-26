using DocumentFormat.OpenXml.Office2010.Excel;
using Edvanz.Domain.Entities.Chat;
using Edvanz.Domain.Interfaces;
using Edvanz.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Edvanz.Infrastructure.Repositories;

/// <summary>
/// EF Core implementation of <see cref="IChatRepo"/>.
/// Inherits GenericRepo&lt;Conversation, long&gt; for basic CRUD; adds every
/// named method the 1:1 direct-chat subsystem needs. No raw LINQ predicates
/// are exposed to the Application layer.
///
/// QUERY FILTERS: HasQueryFilter(!c.IsDeleted) on Conversation and
/// HasQueryFilter(!m.IsDeleted) on ChatMessage are applied automatically by
/// EF Core to every query in this repo — soft-deleted rows are invisible.
///
/// TRACKING: GetByIdAndParticipantAsync returns a TRACKED entity so the
/// ChatService can mutate the denormalized last-message fields in the same
/// SaveChanges as the new message insert (§5.2 / CLAUDE.md §5.2).
/// All other reads are AsNoTracking for performance.
/// </summary>
public class ChatRepo : GenericRepo<Conversation, long>, IChatRepo
{
    public ChatRepo(EdvanzDbContext context) : base(context)
    {
    }

    // ── CONVERSATION ─────────────────────────────────────────────────────────

    /// <inheritdoc />
    public async Task<Conversation?> GetByParticipantsAsync(
        long smallerUserId, long largerUserId)
    {
        // HasQueryFilter(!IsDeleted) applied automatically.
        // UX_Conversations_Participants filtered-unique index backs this lookup.
        return await _context.Conversations
            .FirstOrDefaultAsync(c =>
                c.ParticipantAUserId == smallerUserId &&
                c.ParticipantBUserId == largerUserId);
    }

    /// <inheritdoc />
    public async Task InsertConversationAsync(Conversation conversation)
    {
        await _context.Conversations.AddAsync(conversation);
    }

    /// <inheritdoc />
    public async Task<(IReadOnlyList<ConversationListRow> Items, int TotalCount)>
        GetConversationListAsync(long callerUserId, int page, int pageSize)
    {
        // ── Count on the base query first (avoids a complex-projection COUNT) ──
        var baseQuery = _context.Conversations
            .Where(c =>
                c.ParticipantAUserId == callerUserId ||
                c.ParticipantBUserId == callerUserId);

        int totalCount = await baseQuery.CountAsync();
        if (totalCount == 0)
            return (Array.Empty<ConversationListRow>(), 0);

        // ── Full projection with unread subquery and other-participant join ──
        // c.ParticipantAUser / ParticipantBUser → EF generates INNER JOINs to Users.
        // c.Messages.Count(...) → EF generates a correlated COUNT subquery;
        //   backed by IX_ChatMessages_Conversation_Sender_IsRead.
        // SQL Server puts NULLs last on ORDER BY DESC — new conversations (no messages)
        //   sink naturally to the bottom without an explicit NULLS LAST clause.
        var items = await baseQuery
            .OrderByDescending(c => c.LastMessageAt)
            .Select(c => new ConversationListRow
            {
                ConversationId = c.Id,
                OtherParticipantUserId = c.ParticipantAUserId == callerUserId
                    ? c.ParticipantBUserId
                    : c.ParticipantAUserId,
                OtherParticipantName = c.ParticipantAUserId == callerUserId
                    ? c.ParticipantBUser.FullName
                    : c.ParticipantAUser.FullName,
                OtherParticipantUserType = c.ParticipantAUserId == callerUserId
                    ? c.ParticipantBUser.UserType
                    : c.ParticipantAUser.UserType,
                LastMessagePreview = c.LastMessagePreview,
                LastMessageAt = c.LastMessageAt,
                LastMessageSenderUserId = c.LastMessageSenderUserId,
                UnreadCount = c.Messages
                    .Count(m => m.SenderUserId != callerUserId && !m.IsRead)
            })
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items, totalCount);
    }

    /// <inheritdoc />
    public async Task<Conversation?> GetByIdAndParticipantAsync(
        long conversationId, long callerUserId)
    {
        // TRACKED — caller mutates LastMessageAt/Preview/SenderUserId on send path.
        // HasQueryFilter(!IsDeleted) applied automatically.
        return await _context.Conversations
            .FirstOrDefaultAsync(c =>
                c.Id == conversationId &&
                (c.ParticipantAUserId == callerUserId ||
                 c.ParticipantBUserId == callerUserId));
    }

    // ── MESSAGES ─────────────────────────────────────────────────────────────

    /// <inheritdoc />
    public async Task InsertMessageAsync(ChatMessage message)
    {
        await _context.Set<ChatMessage>().AddAsync(message);
    }

    /// <inheritdoc />
    public async Task<(IReadOnlyList<ChatMessage> Items, int TotalCount)> GetThreadAsync(
        long conversationId, int page, int pageSize)
    {
        // DESC order: page 1 = most recent. Flutter reverses for display.
        // Backed by IX_ChatMessages_ConversationId_SentAt.
        var query = _context.Set<ChatMessage>()
            .AsNoTracking()
            .Where(m => m.ConversationId == conversationId)
            .OrderByDescending(m => m.SentAt).ThenByDescending(m=>m.Id);

        int totalCount = await query.CountAsync();
        if (totalCount == 0)
            return (Array.Empty<ChatMessage>(), 0);

        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items, totalCount);
    }

    // ── READ STATE ────────────────────────────────────────────────────────────

    /// <inheritdoc />
    public async Task<int> MarkConversationReadAsync(
        long conversationId, long readerUserId, DateTime readAtUtc)
    {
        // ExecuteUpdateAsync: single SQL UPDATE, no entity materialization.
        // Marks messages sent by the OTHER participant as read.
        // Idempotent: already-read rows don't match the WHERE and are skipped.
        return await _context.Set<ChatMessage>()
            .Where(m =>
                m.ConversationId == conversationId &&
                m.SenderUserId != readerUserId &&
                !m.IsRead)
            .ExecuteUpdateAsync(s => s
                .SetProperty(m => m.IsRead, true)
                .SetProperty(m => m.ReadAt, readAtUtc));
    }

    /// <inheritdoc />
    public async Task<int> GetTotalUnreadCountAsync(long userId)
    {
        // SelectMany: EF generates INNER JOIN Conversations → ChatMessages.
        // HasQueryFilter on both entities excludes soft-deleted rows.
        // IX_ChatMessages_Conversation_Sender_IsRead backs the inner filter.
        return await _context.Conversations
            .Where(c =>
                c.ParticipantAUserId == userId ||
                c.ParticipantBUserId == userId)
            .SelectMany(c => c.Messages
                .Where(m => m.SenderUserId != userId && !m.IsRead))
            .CountAsync();
    }
}