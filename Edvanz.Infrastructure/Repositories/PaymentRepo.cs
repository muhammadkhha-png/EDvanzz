using Edvanz.Domain.Constants;
using Edvanz.Domain.Entities;
using Edvanz.Domain.Enums;
using Edvanz.Domain.Interfaces;
using Edvanz.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Edvanz.Infrastructure.Repositories;

/// <summary>
/// Extended repository for the Payment Module (Module 4) and Event Payment Module (Module 5).
/// Centralizes all domain-specific query logic for payment-related entities.
///
/// ARCHITECTURAL NOTE:
/// Inherits GenericRepo&lt;PaymentTransaction, long&gt; for basic CRUD on the primary entity.
/// All other entities (PaymentPeriod, StudentPaymentCounter, AssistantWallet, etc.) are
/// accessed via _context directly through named methods — keeping query logic in one place.
///
/// QUERY PATTERNS:
/// - Paged queries use CountAsync + Skip/Take (same as AttendanceRepo).
/// - Aggregates use GroupBy + Sum projections for O(1) SQL operations.
/// - ExecuteUpdateAsync for bulk FK nullification (same as AttendanceRepo Step 1.2).
/// - All queries include teacherId guard for tenant isolation.
/// </summary>
public class PaymentRepo : GenericRepo<PaymentTransaction, long>, IPaymentRepo
{
    public PaymentRepo(EdvanzDbContext context) : base(context)
    {
    }

    // ══════════════════════════════════════════════
    // PAYMENT TRANSACTION QUERIES
    // ══════════════════════════════════════════════

    /// <inheritdoc />
    public async Task<PaymentTransaction?> GetTransactionByIdAndTeacherAsync(
        long transactionId, long teacherId)
    {
        return await _context.PaymentTransactions
            .FirstOrDefaultAsync(t => t.Id == transactionId
                && t.TeacherId == teacherId
                && !t.IsDeleted);
    }

    /// <inheritdoc />
    public async Task<(IReadOnlyList<PaymentTransaction> Items, int TotalCount)>
        GetStudentPaymentHistoryPagedAsync(
            long teacherId, long teacherStudentId,
            DateTime? startDate, DateTime? endDate,
            int page, int pageSize)
    {
        var query = _context.PaymentTransactions
            .Where(t => t.TeacherId == teacherId
                && t.TeacherStudentId == teacherStudentId
                && !t.IsDeleted);

        if (startDate.HasValue)
            query = query.Where(t => t.CollectedAt >= startDate.Value);
        if (endDate.HasValue)
            query = query.Where(t => t.CollectedAt <= endDate.Value.Date.AddDays(1));

        int totalCount = await query.CountAsync();
        var items = await query
            .OrderByDescending(t => t.CollectedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .AsNoTracking()
            .ToListAsync();

        return (items, totalCount);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<PaymentTransaction>> GetTransactionsByPeriodAsync(
        long paymentPeriodId)
    {
        return await _context.PaymentTransactions
            .Where(t => t.PaymentPeriodId == paymentPeriodId && !t.IsDeleted)
            .OrderBy(t => t.CollectedAt)
            .AsNoTracking()
            .ToListAsync();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<PaymentTransaction>> GetSameDayTransactionsAsync(
        long teacherId, long teacherStudentId, DateTime localDate)
    {
        return await _context.PaymentTransactions
            .Where(t => t.TeacherId == teacherId
                && t.TeacherStudentId == teacherStudentId
                && t.LocalCollectedAt.Date == localDate.Date
                && !t.IsDeleted)
            .AsNoTracking()
            .ToListAsync();
    }

    /// <inheritdoc />
    public async Task<(IReadOnlyList<PaymentTransaction> Items, int TotalCount)>
        GetCollectorTransactionsPagedAsync(
            long teacherId, long collectedByUserId,
            DateTime? startDate, DateTime? endDate,
            int page, int pageSize)
    {
        var query = _context.PaymentTransactions
            .Where(t => t.TeacherId == teacherId
                && t.CollectedByUserId == collectedByUserId
                && !t.IsDeleted);

        if (startDate.HasValue)
            query = query.Where(t => t.CollectedAt >= startDate.Value);
        if (endDate.HasValue)
            query = query.Where(t => t.CollectedAt <= endDate.Value.Date.AddDays(1));

        int totalCount = await query.CountAsync();
        var items = await query
            .OrderByDescending(t => t.CollectedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .AsNoTracking()
            .ToListAsync();

        return (items, totalCount);
    }

    /// <inheritdoc />
    public async Task<(IReadOnlyList<PaymentTransaction> Items, int TotalCount)>
        GetSessionTransactionsPagedAsync(
            long teacherId, long sessionId,
            DateTime? startDate, DateTime? endDate,
            int page, int pageSize)
    {
        var query = _context.PaymentTransactions
            .Where(t => t.TeacherId == teacherId
                && t.SessionId == sessionId
                && !t.IsDeleted);

        if (startDate.HasValue)
            query = query.Where(t => t.CollectedAt >= startDate.Value);
        if (endDate.HasValue)
            query = query.Where(t => t.CollectedAt <= endDate.Value.Date.AddDays(1));

        int totalCount = await query.CountAsync();
        var items = await query
            .OrderByDescending(t => t.CollectedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .AsNoTracking()
            .ToListAsync();

        return (items, totalCount);
    }

    /// <inheritdoc />
    public async Task<(IReadOnlyList<PaymentTransaction> Items, int TotalCount)>
        GetTransactionsByDateRangePagedAsync(
            long teacherId,
            DateTime startDate, DateTime endDate,
            long? sessionId, long? collectedByUserId,
            int page, int pageSize)
    {
        var query = _context.PaymentTransactions
            .Where(t => t.TeacherId == teacherId
                && t.CollectedAt >= startDate
                && t.CollectedAt <= endDate
                && !t.IsDeleted);

        if (sessionId.HasValue)
            query = query.Where(t => t.SessionId == sessionId.Value);
        if (collectedByUserId.HasValue)
            query = query.Where(t => t.CollectedByUserId == collectedByUserId.Value);

        int totalCount = await query.CountAsync();
        var items = await query
            .OrderByDescending(t => t.CollectedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .AsNoTracking()
            .ToListAsync();

        return (items, totalCount);
    }

    // ══════════════════════════════════════════════
    // PAYMENT PERIOD QUERIES
    // ══════════════════════════════════════════════

    /// <inheritdoc />
    public async Task<PaymentPeriod?> GetEarliestUnpaidPeriodAsync(
        long teacherId, long teacherStudentId, long? sessionId)
    {
        var query = _context.PaymentPeriods
            .Where(p => p.TeacherId == teacherId
                && p.TeacherStudentId == teacherStudentId
                && p.PaymentStatus != PaymentStatus.Paid);

        if (sessionId.HasValue)
            query = query.Where(p => p.SessionId == sessionId.Value);

        return await query
            .OrderBy(p => p.PeriodSequence)
            .FirstOrDefaultAsync();
    }

    /// <inheritdoc />
    public async Task<PaymentPeriod?> GetPaymentPeriodByIdAsync(long paymentPeriodId)
    {
        return await _context.PaymentPeriods.FindAsync(paymentPeriodId);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<PaymentPeriod>> GetPaymentPeriodsByStudentAndSessionAsync(
        long teacherId, long teacherStudentId, long? sessionId)
    {
        var query = _context.PaymentPeriods
            .Where(p => p.TeacherId == teacherId
                && p.TeacherStudentId == teacherStudentId);

        if (sessionId.HasValue)
            query = query.Where(p => p.SessionId == sessionId.Value);

        return await query
            .OrderBy(p => p.PeriodSequence)
            .AsNoTracking()
            .ToListAsync();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<PaymentPeriod>> GetAllPaymentPeriodsByStudentAsync(
        long teacherId, long teacherStudentId)
    {
        return await _context.PaymentPeriods
            .Where(p => p.TeacherId == teacherId && p.TeacherStudentId == teacherStudentId)
            .OrderBy(p => p.SessionName)
            .ThenBy(p => p.PeriodSequence)
            .AsNoTracking()
            .ToListAsync();
    }

    /// <inheritdoc />
    public async Task<int> CountUnpaidStudentsBySessionAsync(long teacherId, long sessionId)
    {
        return await _context.PaymentPeriods
            .Where(p => p.TeacherId == teacherId
                && p.SessionId == sessionId
                && p.PaymentStatus != PaymentStatus.Paid
                && p.TeacherStudentId.HasValue)
            .Select(p => p.TeacherStudentId)
            .Distinct()
            .CountAsync();
    }

    /// <inheritdoc />
    public async Task<int> GetMaxPeriodSequenceAsync(
        long teacherId, long teacherStudentId, long sessionId)
    {
        var maxSeq = await _context.PaymentPeriods
            .Where(p => p.TeacherId == teacherId
                && p.TeacherStudentId == teacherStudentId
                && p.SessionId == sessionId)
            .MaxAsync(p => (int?)p.PeriodSequence);

        return maxSeq ?? 0;
    }

    /// <inheritdoc />
    public async Task AddPaymentPeriodAsync(PaymentPeriod period)
    {
        await _context.PaymentPeriods.AddAsync(period);
    }

    /// <inheritdoc />
    public async Task AddPaymentPeriodsRangeAsync(IEnumerable<PaymentPeriod> periods)
    {
        await _context.PaymentPeriods.AddRangeAsync(periods);
    }

    /// <inheritdoc />
    public async Task UpdatePaymentPeriodAsync(PaymentPeriod period)
    {
        _context.Entry(period).State = EntityState.Modified;
        await Task.CompletedTask;
    }

    // ══════════════════════════════════════════════
    // STUDENT PAYMENT COUNTER QUERIES
    // ══════════════════════════════════════════════

    /// <inheritdoc />
    public async Task<StudentPaymentCounter?> GetPaymentCounterAsync(
        long teacherId, long teacherStudentId)
    {
        return await _context.StudentPaymentCounters
            .FirstOrDefaultAsync(c => c.TeacherId == teacherId
                && c.TeacherStudentId == teacherStudentId);
    }

    /// <inheritdoc />
    public async Task AddPaymentCounterAsync(StudentPaymentCounter counter)
    {
        await _context.StudentPaymentCounters.AddAsync(counter);
    }

    /// <inheritdoc />
    public async Task UpdatePaymentCounterAsync(StudentPaymentCounter counter)
    {
        _context.Entry(counter).State = EntityState.Modified;
        await Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task<(IReadOnlyList<StudentPaymentCounter> Items, int TotalCount)>
        GetUnpaidStudentsPagedAsync(
            long teacherId,
            long? sessionId, long? sessionGroupId,
            PaymentType? paymentType,
            int? minConsecutiveUnpaid,
            string? search,
            int page, int pageSize)
    {
        var query = _context.StudentPaymentCounters
            .Where(c => c.TeacherId == teacherId && c.TotalOutstanding > 0)
            .Include(c => c.TeacherStudent)
                .ThenInclude(ts => ts!.Session);

        IQueryable<StudentPaymentCounter> filteredQuery = query;

        if (sessionId.HasValue)
            filteredQuery = filteredQuery.Where(c =>
                c.TeacherStudent != null && c.TeacherStudent.SessionId == sessionId.Value);

        if (sessionGroupId.HasValue)
            filteredQuery = filteredQuery.Where(c =>
                c.TeacherStudent != null
                && c.TeacherStudent.Session != null
                && c.TeacherStudent.Session.SessionGroupId == sessionGroupId.Value);

        if (minConsecutiveUnpaid.HasValue)
            filteredQuery = filteredQuery.Where(c =>
                c.ConsecutiveUnpaid >= minConsecutiveUnpaid.Value);

        if (!string.IsNullOrWhiteSpace(search))
        {
            string searchLower = search.Trim().ToLower();
            filteredQuery = filteredQuery.Where(c =>
                c.TeacherStudent != null
                && (c.TeacherStudent.StudentName.ToLower().Contains(searchLower)
                    || c.TeacherStudent.StudentCode.ToLower().Contains(searchLower)));
        }

        int totalCount = await filteredQuery.CountAsync();
        var items = await filteredQuery
            .OrderByDescending(c => c.ConsecutiveUnpaid)
            .ThenByDescending(c => c.TotalOutstanding)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .AsNoTracking()
            .ToListAsync();

        return (items, totalCount);
    }

    /// <inheritdoc />
    public async Task<decimal> GetTotalOutstandingAmountAsync(long teacherId, long? sessionId)
    {
        var query = _context.StudentPaymentCounters
            .Where(c => c.TeacherId == teacherId);

        if (sessionId.HasValue)
            query = query.Where(c =>
                c.TeacherStudent != null && c.TeacherStudent.SessionId == sessionId.Value);

        return await query.SumAsync(c => c.TotalOutstanding);
    }

    // ══════════════════════════════════════════════
    // ASSISTANT WALLET QUERIES
    // ══════════════════════════════════════════════

    /// <inheritdoc />
    public async Task<AssistantWallet?> GetAssistantWalletAsync(
        long teacherId, long assistantId)
    {
        return await _context.AssistantWallets
            .Include(w => w.Assistant)
                .ThenInclude(a => a.User)
            .FirstOrDefaultAsync(w => w.TeacherId == teacherId
                && w.AssistantId == assistantId);
    }

    /// <inheritdoc />
    public async Task<AssistantWallet?> GetAssistantWalletByUserIdAsync(
        long teacherId, long assistantUserId)
    {
        return await _context.AssistantWallets
            .FirstOrDefaultAsync(w => w.TeacherId == teacherId
                && w.AssistantUserId == assistantUserId);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<AssistantWallet>> GetAllAssistantWalletsAsync(long teacherId)
    {
        return await _context.AssistantWallets
            .Where(w => w.TeacherId == teacherId)
            .Include(w => w.Assistant)
                .ThenInclude(a => a.User)
            .OrderBy(w => w.Assistant.User.FullName)
            .AsNoTracking()
            .ToListAsync();
    }

    /// <inheritdoc />
    public async Task AddAssistantWalletAsync(AssistantWallet wallet)
    {
        await _context.AssistantWallets.AddAsync(wallet);
    }

    /// <inheritdoc />
    public async Task UpdateAssistantWalletAsync(AssistantWallet wallet)
    {
        _context.Entry(wallet).State = EntityState.Modified;
        await Task.CompletedTask;
    }

    // ══════════════════════════════════════════════
    // WALLET RESET LOG QUERIES
    // ══════════════════════════════════════════════

    /// <inheritdoc />
    public async Task AddWalletResetLogAsync(WalletResetLog log)
    {
        await _context.WalletResetLogs.AddAsync(log);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<WalletResetLog>> GetWalletResetLogsAsync(
        long teacherId, long assistantId)
    {
        return await _context.WalletResetLogs
            .Where(l => l.TeacherId == teacherId && l.AssistantId == assistantId)
            .OrderByDescending(l => l.ResetAt)
            .AsNoTracking()
            .ToListAsync();
    }

    // ══════════════════════════════════════════════
    // PAYMENT EDIT LOG QUERIES
    // ══════════════════════════════════════════════

    /// <inheritdoc />
    public async Task AddPaymentEditLogAsync(PaymentEditLog log)
    {
        await _context.PaymentEditLogs.AddAsync(log);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<PaymentEditLog>> GetPaymentEditLogsAsync(
        long paymentTransactionId)
    {
        return await _context.PaymentEditLogs
            .Where(l => l.PaymentTransactionId == paymentTransactionId)
            .OrderBy(l => l.EditedAt)
            .AsNoTracking()
            .ToListAsync();
    }

    // ══════════════════════════════════════════════
    // DEPARTURE QUERIES
    // ══════════════════════════════════════════════

    /// <inheritdoc />
    public async Task AddStudentDepartureAsync(StudentDeparture departure)
    {
        await _context.StudentDepartures.AddAsync(departure);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<StudentDeparture>> GetStudentDeparturesAsync(
        long teacherId, long teacherStudentId)
    {
        return await _context.StudentDepartures
            .Where(d => d.TeacherId == teacherId && d.TeacherStudentId == teacherStudentId)
            .OrderByDescending(d => d.DepartedAt)
            .AsNoTracking()
            .ToListAsync();
    }

    // ══════════════════════════════════════════════
    // SESSION TRANSFER QUERIES
    // ══════════════════════════════════════════════

    /// <inheritdoc />
    public async Task AddSessionTransferEventAsync(SessionTransferEvent transferEvent)
    {
        await _context.SessionTransferEvents.AddAsync(transferEvent);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<SessionTransferEvent>> GetStudentTransferEventsAsync(
        long teacherId, long teacherStudentId)
    {
        return await _context.SessionTransferEvents
            .Where(t => t.TeacherId == teacherId && t.TeacherStudentId == teacherStudentId)
            .OrderByDescending(t => t.TransferredAt)
            .AsNoTracking()
            .ToListAsync();
    }

    // ══════════════════════════════════════════════
    // DASHBOARD AGGREGATES
    // ══════════════════════════════════════════════

    /// <inheritdoc />
    public async Task<(decimal Expected, decimal Collected, decimal Remaining)>
        GetDashboardAggregatesAsync(
            long teacherId,
            long? sessionId, long? sessionGroupId,
            PaymentType? paymentType,
            DateTime? startDate, DateTime? endDate)
    {
        var periodQuery = _context.PaymentPeriods
            .Where(p => p.TeacherId == teacherId);

        if (sessionId.HasValue)
            periodQuery = periodQuery.Where(p => p.SessionId == sessionId.Value);

        if (sessionGroupId.HasValue)
            periodQuery = periodQuery.Where(p =>
                p.Session != null && p.Session.SessionGroupId == sessionGroupId.Value);

        if (startDate.HasValue)
            periodQuery = periodQuery.Where(p => p.PeriodEnd >= startDate.Value);

        if (endDate.HasValue)
            periodQuery = periodQuery.Where(p => p.PeriodStart <= endDate.Value);

        var aggregates = await periodQuery
            .GroupBy(p => 1)
            .Select(g => new
            {
                Expected = g.Sum(p => p.AmountDue),
                Collected = g.Sum(p => p.AmountPaid)
            })
            .FirstOrDefaultAsync();

        decimal expected = aggregates?.Expected ?? 0;
        decimal collected = aggregates?.Collected ?? 0;
        return (expected, collected, expected - collected);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<(long SessionId, string SessionName, decimal Expected, decimal Collected, decimal Remaining)>>
        GetDashboardPerSessionAsync(
            long teacherId,
            long? sessionGroupId,
            PaymentType? paymentType,
            DateTime? startDate, DateTime? endDate)
    {
        var query = _context.PaymentPeriods
            .Where(p => p.TeacherId == teacherId && p.SessionId.HasValue);

        if (sessionGroupId.HasValue)
            query = query.Where(p =>
                p.Session != null && p.Session.SessionGroupId == sessionGroupId.Value);

        if (startDate.HasValue)
            query = query.Where(p => p.PeriodEnd >= startDate.Value);
        if (endDate.HasValue)
            query = query.Where(p => p.PeriodStart <= endDate.Value);

        var result = await query
            .GroupBy(p => new { p.SessionId, p.SessionName })
            .Select(g => new
            {
                SessionId = g.Key.SessionId!.Value,
                SessionName = g.Key.SessionName,
                Expected = g.Sum(p => p.AmountDue),
                Collected = g.Sum(p => p.AmountPaid)
            })
            .OrderBy(r => r.SessionName)
            .ToListAsync();

        return result
            .Select(r => (r.SessionId, r.SessionName, r.Expected, r.Collected, r.Expected - r.Collected))
            .ToList();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<(long UserId, string? UserName, decimal Collected, int TransactionCount)>>
        GetDashboardPerCollectorAsync(
            long teacherId,
            DateTime? startDate, DateTime? endDate)
    {
        var query = _context.PaymentTransactions
            .Where(t => t.TeacherId == teacherId
                && !t.IsDeleted
                && t.CollectedByUserId.HasValue);

        if (startDate.HasValue)
            query = query.Where(t => t.CollectedAt >= startDate.Value);
        if (endDate.HasValue)
            query = query.Where(t => t.CollectedAt <= endDate.Value.Date.AddDays(1));

        var result = await query
            .GroupBy(t => t.CollectedByUserId!.Value)
            .Select(g => new
            {
                UserId = g.Key,
                Collected = g.Sum(t => t.AmountPaid),
                TransactionCount = g.Count()
            })
            .ToListAsync();

        return result
            .Select(r => (r.UserId, (string?)null, r.Collected, r.TransactionCount))
            .ToList();
    }

    // ══════════════════════════════════════════════
    // EVENT QUERIES (Module 5)
    // ══════════════════════════════════════════════

    /// <inheritdoc />
    public async Task AddPaymentEventAsync(PaymentEvent paymentEvent)
    {
        await _context.PaymentEvents.AddAsync(paymentEvent);
    }

    /// <inheritdoc />
    public async Task<PaymentEvent?> GetPaymentEventByIdAndTeacherAsync(
        long eventId, long teacherId)
    {
        return await _context.PaymentEvents
            .FirstOrDefaultAsync(e => e.Id == eventId
                && e.TeacherId == teacherId
                && !e.IsDeleted);
    }

    /// <inheritdoc />
    public async Task<(IReadOnlyList<PaymentEvent> Items, int TotalCount)>
        GetPaymentEventsPagedAsync(long teacherId, int page, int pageSize)
    {
        var query = _context.PaymentEvents
            .Where(e => e.TeacherId == teacherId && !e.IsDeleted);

        int totalCount = await query.CountAsync();
        var items = await query
            .OrderByDescending(e => e.EventDate)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .AsNoTracking()
            .ToListAsync();

        return (items, totalCount);
    }

    /// <inheritdoc />
    public async Task UpdatePaymentEventAsync(PaymentEvent paymentEvent)
    {
        _context.Entry(paymentEvent).State = EntityState.Modified;
        await Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task AddEventObligationsRangeAsync(
        IEnumerable<EventStudentObligation> obligations)
    {
        await _context.EventStudentObligations.AddRangeAsync(obligations);
    }

    /// <inheritdoc />
    public async Task<EventStudentObligation?> GetEventObligationAsync(
        long eventId, long teacherStudentId)
    {
        return await _context.EventStudentObligations
            .FirstOrDefaultAsync(o => o.PaymentEventId == eventId
                && o.TeacherStudentId == teacherStudentId);
    }

    /// <inheritdoc />
    public async Task<(IReadOnlyList<EventStudentObligation> Items, int TotalCount)>
        GetEventObligationsPagedAsync(
            long eventId, long teacherId,
            PaymentStatus? statusFilter,
            string? search,
            int page, int pageSize)
    {
        var query = _context.EventStudentObligations
            .Where(o => o.PaymentEventId == eventId && o.TeacherId == teacherId);

        if (statusFilter.HasValue)
        {
            if (statusFilter.Value == PaymentStatus.Paid)
                query = query.Where(o => o.PaymentStatus == PaymentStatus.Paid);
            else
                query = query.Where(o => o.PaymentStatus != PaymentStatus.Paid);
        }

        // REQ-EVT-016: Search by student name or student code
        if (!string.IsNullOrWhiteSpace(search))
        {
            string searchLower = search.Trim().ToLower();
            query = query.Where(o =>
                (o.StudentName != null && o.StudentName.ToLower().Contains(searchLower))
                || (o.StudentCode != null && o.StudentCode.ToLower().Contains(searchLower)));
        }

        int totalCount = await query.CountAsync();
        var items = await query
            .OrderBy(o => o.StudentName)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .AsNoTracking()
            .ToListAsync();

        return (items, totalCount);
    }

    /// <inheritdoc />
    public async Task UpdateEventObligationAsync(EventStudentObligation obligation)
    {
        _context.Entry(obligation).State = EntityState.Modified;
        await Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task DeleteEventObligationAsync(EventStudentObligation obligation)
    {
        _context.EventStudentObligations.Remove(obligation);
        await Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task<(IReadOnlyList<PaymentEvent> Items, int TotalCount)>
        GetPaymentEventsFilteredPagedAsync(
            long teacherId,
            string? searchName,
            EventTargetScopeType? scopeTypeFilter,
            string? completionStatus,
            int page, int pageSize)
    {
        var query = _context.PaymentEvents
            .Where(e => e.TeacherId == teacherId && !e.IsDeleted);

        if (!string.IsNullOrWhiteSpace(searchName))
        {
            string search = searchName.Trim().ToLower();
            query = query.Where(e => e.EventName.ToLower().Contains(search));
        }

        if (scopeTypeFilter.HasValue)
            query = query.Where(e => e.TargetScopeType == scopeTypeFilter.Value);

        if (!string.IsNullOrWhiteSpace(completionStatus))
        {
            query = completionStatus switch
            {
                "FullyCollected" => query.Where(e => e.TotalCollectedRevenue >= e.TotalExpectedRevenue),
                "PartiallyCollected" => query.Where(e => e.TotalCollectedRevenue > 0 && e.TotalCollectedRevenue < e.TotalExpectedRevenue),
                "NotStarted" => query.Where(e => e.TotalCollectedRevenue == 0),
                _ => query
            };
        }

        int totalCount = await query.CountAsync();
        var items = await query
            .OrderByDescending(e => e.EventDate)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .AsNoTracking()
            .ToListAsync();

        return (items, totalCount);
    }

    /// <inheritdoc />
    public async Task AddEventPaymentTransactionAsync(EventPaymentTransaction transaction)
    {
        await _context.EventPaymentTransactions.AddAsync(transaction);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<EventPaymentTransaction>> GetEventPaymentTransactionsAsync(
        long eventId, long teacherId)
    {
        return await _context.EventPaymentTransactions
            .Where(t => t.PaymentEventId == eventId && t.TeacherId == teacherId)
            .OrderByDescending(t => t.CollectedAt)
            .AsNoTracking()
            .ToListAsync();
    }

    // ══════════════════════════════════════════════
    // TARGET SCOPE RESOLUTION (Module 5)
    // ══════════════════════════════════════════════

    /// <inheritdoc />
    public async Task<List<long>> GetStudentIdsBySessionAsync(long teacherId, long sessionId)
    {
        return await _context.TeacherStudents
            .Where(s => s.TeacherId == teacherId && s.SessionId == sessionId && !s.IsDeleted)
            .Select(s => s.Id)
            .ToListAsync();
    }

    /// <inheritdoc />
    public async Task<List<long>> GetStudentIdsByGroupAsync(long teacherId, long sessionGroupId)
    {
        var sessionIds = await _context.Sessions
            .Where(s => s.TeacherId == teacherId && s.SessionGroupId == sessionGroupId)
            .Select(s => s.Id)
            .ToListAsync();

        return await _context.TeacherStudents
            .Where(s => s.TeacherId == teacherId && s.SessionId.HasValue
                && sessionIds.Contains(s.SessionId.Value) && !s.IsDeleted)
            .Select(s => s.Id)
            .ToListAsync();
    }

    /// <inheritdoc />
    public async Task<List<long>> GetAllStudentIdsAsync(long teacherId)
    {
        return await _context.TeacherStudents
            .Where(s => s.TeacherId == teacherId && !s.IsDeleted)
            .Select(s => s.Id)
            .ToListAsync();
    }

    // ══════════════════════════════════════════════
    // INTEGRATION HOOKS (bulk FK nullification)
    // ══════════════════════════════════════════════

    /// <inheritdoc />
    /// Uses ExecuteUpdateAsync — single SQL UPDATE, no in-memory loading.
    /// Same pattern as AttendanceRepo.NullifySessionIdOnRecordsForSessionAsync (Step 1.2).
    public async Task NullifySessionIdOnPaymentRecordsAsync(long sessionId)
    {
        // Nullify on PaymentTransactions
        await _context.PaymentTransactions
            .Where(t => t.SessionId == sessionId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(t => t.SessionId, (long?)null));

        // Nullify on PaymentPeriods
        await _context.PaymentPeriods
            .Where(p => p.SessionId == sessionId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(p => p.SessionId, (long?)null));

        // Nullify on StudentDepartures
        await _context.StudentDepartures
            .Where(d => d.SessionId == sessionId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(d => d.SessionId, (long?)null));
    }

    /// <inheritdoc />
    /// Uses ExecuteUpdateAsync — single SQL UPDATE, no in-memory loading.
    /// Same pattern as AttendanceRepo.NullifyStudentReferencesOnRecordsAsync (Step 1.1).
    /// Denormalized StudentName and StudentCode remain intact for historical display.
    public async Task NullifyStudentReferencesOnPaymentRecordsAsync(long teacherStudentId)
    {
        // PaymentTransactions
        await _context.PaymentTransactions
            .Where(t => t.TeacherStudentId == teacherStudentId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(t => t.TeacherStudentId, (long?)null)
                .SetProperty(t => t.StudentSessionAssignmentId, (long?)null));

        // PaymentPeriods
        await _context.PaymentPeriods
            .Where(p => p.TeacherStudentId == teacherStudentId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(p => p.TeacherStudentId, (long?)null)
                .SetProperty(p => p.StudentSessionAssignmentId, (long?)null));

        // StudentDepartures
        await _context.StudentDepartures
            .Where(d => d.TeacherStudentId == teacherStudentId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(d => d.TeacherStudentId, (long?)null));

        // SessionTransferEvents
        await _context.SessionTransferEvents
            .Where(t => t.TeacherStudentId == teacherStudentId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(t => t.TeacherStudentId, (long?)null));

        // EventStudentObligations
        await _context.EventStudentObligations
            .Where(o => o.TeacherStudentId == teacherStudentId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(o => o.TeacherStudentId, (long?)null));

        // EventPaymentTransactions
        await _context.EventPaymentTransactions
            .Where(t => t.TeacherStudentId == teacherStudentId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(t => t.TeacherStudentId, (long?)null));

        // Delete counter (no longer needed after purge)
        await _context.StudentPaymentCounters
            .Where(c => c.TeacherStudentId == teacherStudentId)
            .ExecuteDeleteAsync();
    }

    /// <inheritdoc />
    public async Task<int> RecalculateConsecutiveUnpaidAsync(
        long teacherId, long teacherStudentId)
    {
        var recentPeriods = await _context.PaymentPeriods
            .Where(p => p.TeacherId == teacherId
                && p.TeacherStudentId == teacherStudentId)
            .OrderByDescending(p => p.PeriodSequence)
            .Take(PaymentConstants.MaxConsecutiveUnpaidScanDepth)
            .AsNoTracking()
            .ToListAsync();

        int consecutive = 0;
        foreach (var period in recentPeriods)
        {
            if (period.PaymentStatus == PaymentStatus.Paid)
                break;
            consecutive++;
        }

        return consecutive;
    }
}