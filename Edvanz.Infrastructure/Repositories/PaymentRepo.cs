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

    /// <inheritdoc />
    public async Task<int> CountEventsByTeacherAsync(long teacherId)
    {
        return await _context.PaymentEvents
            .CountAsync(e => e.TeacherId == teacherId && !e.IsDeleted);
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
            // CollectedAt is datetime2(0); ties are common within a batch collect.
            // Id is the deterministic tiebreaker that makes Skip/Take stable.
            .OrderByDescending(t => t.CollectedAt)
            .ThenByDescending(t => t.Id)
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
    public async Task<List<PaymentPeriod>> GetUnpaidPeriodsThroughAsync(
        long teacherId, long teacherStudentId, long? sessionId, DateTime throughMonthEnd)
    {
        // Tracked (NOT AsNoTracking) — the caller mutates AmountPaid/PaymentStatus and saves.
        // Earliest-first, and only periods that start on/before the cutoff (current month, or
        // current+1 when paying one month in advance). Ordered so a payment fills the oldest
        // debt first and cascades forward.
        var query = _context.PaymentPeriods
            .Where(p => p.TeacherId == teacherId
                && p.TeacherStudentId == teacherStudentId
                && p.PaymentStatus != PaymentStatus.Paid
                && p.PeriodStart <= throughMonthEnd);

        if (sessionId.HasValue)
            query = query.Where(p => p.SessionId == sessionId.Value);

        return await query.OrderBy(p => p.PeriodSequence).ToListAsync();
    }

    /// <inheritdoc />
    public async Task<decimal> GetOverdueTotalThroughAsync(
        long teacherId, long teacherStudentId, long? sessionId, DateTime throughMonthEnd)
    {
        // Total arrears the student owes THROUGH the given month (sum of each unpaid month's
        // remaining due). This is the server-owned "amount due" for lookup + mark-paid, and it
        // never includes months in advance.
        var query = _context.PaymentPeriods
            .Where(p => p.TeacherId == teacherId
                && p.TeacherStudentId == teacherStudentId
                && p.PaymentStatus != PaymentStatus.Paid
                && p.PeriodStart <= throughMonthEnd);

        if (sessionId.HasValue)
            query = query.Where(p => p.SessionId == sessionId.Value);

        return await query.SumAsync(p => (decimal?)(p.AmountDue - p.AmountPaid)) ?? 0m;
    }

    /// <inheritdoc />
    public async Task<PaymentPeriod?> GetLatestPaidPeriodAsync(
        long teacherId, long teacherStudentId, long? sessionId)
    {
        var query = _context.PaymentPeriods
            .Where(p => p.TeacherId == teacherId
                && p.TeacherStudentId == teacherStudentId
                && p.AmountPaid > 0);

        if (sessionId.HasValue)
            query = query.Where(p => p.SessionId == sessionId.Value);

        return await query
            .OrderByDescending(p => p.PeriodSequence)
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
    public async Task<int> CountAssignedStudentsAsync(long teacherId)
    {
        // Active (non-deleted, global filter applies) students currently assigned to a session.
        return await _context.TeacherStudents
            .CountAsync(ts => ts.TeacherId == teacherId && ts.SessionId != null);
    }

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
    public async Task<(int Paid, int ProRated, int Unpaid)> GetStudentPaymentStatusCountsAsync(
        long teacherId, DateTime selectedMonthEnd)
    {
        // Buckets are judged ONLY through the selected month — future months (periods
        // generated ahead of time up to session end) must never count as owed, otherwise
        // every student reads as unpaid. A student is in scope if they have any period that
        // starts on or before the selected month's end (i.e. assigned in/before that month).
        int totalStudentsWithPeriods = await _context.PaymentPeriods
            .Where(p => p.TeacherId == teacherId && p.TeacherStudentId.HasValue
                && p.PeriodStart <= selectedMonthEnd)
            .Select(p => p.TeacherStudentId!.Value)
            .Distinct()
            .CountAsync();

        // Per student, whether their earliest outstanding period (lowest PeriodSequence
        // among non-Paid periods THROUGH the selected month) is prorated. Same "earliest
        // unpaid" concept as GetEarliestUnpaidPeriodAsync, computed for every student in one
        // round trip. A student with no unpaid period on/before the selected month is "paid".
        var earliestOutstandingIsProRated = await _context.PaymentPeriods
            .Where(p => p.TeacherId == teacherId
                && p.TeacherStudentId.HasValue
                && p.PaymentStatus != PaymentStatus.Paid
                && p.PeriodStart <= selectedMonthEnd)
            .GroupBy(p => p.TeacherStudentId!.Value)
            .Select(g => g.OrderBy(p => p.PeriodSequence).First().IsProRated)
            .ToListAsync();

        int proRated = earliestOutstandingIsProRated.Count(isProRated => isProRated);
        int unpaid = earliestOutstandingIsProRated.Count - proRated;
        int paid = totalStudentsWithPeriods - earliestOutstandingIsProRated.Count;

        return (paid, proRated, unpaid);
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
    /// <inheritdoc />
    public async Task UpdatePaymentCounterAsync(StudentPaymentCounter counter)
    {
        // A counter created earlier in this same unit of work is still in the Added
        // state with a temporary key; forcing it to Modified throws. Leave Added entities
        // as-is — SaveChanges INSERTs them with the totals already set on the instance.
        // Only already-tracked/persisted rows need the explicit Modified flag.
        var entry = _context.Entry(counter);
        if (entry.State != EntityState.Added)
            entry.State = EntityState.Modified;

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
    // SCREEN QUERIES (api/v1 — frontend payment.json)
    // ══════════════════════════════════════════════

    /// <inheritdoc />
    public async Task<(IReadOnlyList<CollectStudentRow> Items, int TotalCount, int CountAll, int CountAssigned, int CountUnassigned)>
        GetCollectStudentsPagedAsync(
            long teacherId, string filter, string? search, int page, int pageSize,
            DateTime unpaidThroughMonthEnd)
    {
        // Global !IsDeleted query filter on TeacherStudent applies automatically.
        var baseQuery = _context.TeacherStudents.Where(ts => ts.TeacherId == teacherId);

        if (!string.IsNullOrWhiteSpace(search))
        {
            string s = search.Trim().ToLower();
            baseQuery = baseQuery.Where(ts =>
                ts.StudentName.ToLower().Contains(s) || ts.StudentCode.ToLower().Contains(s));
        }

        // Per-tab counts reflect the current search.
        int countAll = await baseQuery.CountAsync();
        int countAssigned = await baseQuery.CountAsync(ts => ts.SessionId != null);
        int countUnassigned = countAll - countAssigned;

        var filtered = baseQuery;
        if (string.Equals(filter, "assigned", StringComparison.OrdinalIgnoreCase))
            filtered = filtered.Where(ts => ts.SessionId != null);
        else if (string.Equals(filter, "unassigned", StringComparison.OrdinalIgnoreCase))
            filtered = filtered.Where(ts => ts.SessionId == null);

        int totalCount = await filtered.CountAsync();

        // Page first, then LEFT-join each row to its counter (correlated TOP-1 subquery,
        // bounded to pageSize rows — no N+1 across the full set).
        var raw = await filtered
            .OrderBy(ts => ts.StudentName)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(ts => new
            {
                ts.Id,
                ts.StudentName,
                ts.StudentCode,
                IsAssigned = ts.SessionId != null,
                SessionAmount = ts.Session != null ? ts.Session.SessionAmount : (decimal?)null,
                CustomAmount = _context.StudentPaymentCounters
                    .Where(c => c.TeacherId == teacherId && c.TeacherStudentId == ts.Id)
                    .Select(c => c.CustomPaymentAmount)
                    .FirstOrDefault(),
                // Unpaid status is judged THROUGH the current month only — future pre-generated
                // periods must not make an otherwise-caught-up student read as unpaid.
                UnpaidMonths = _context.PaymentPeriods
                    .Where(p => p.TeacherId == teacherId && p.TeacherStudentId == ts.Id
                        && p.PeriodStart <= unpaidThroughMonthEnd
                        && p.PaymentStatus != PaymentStatus.Paid)
                    .Count()
            })
            .AsNoTracking()
            .ToListAsync();

        var items = raw.Select(r => new CollectStudentRow
        {
            TeacherStudentId = r.Id,
            StudentName = r.StudentName,
            StudentCode = r.StudentCode,
            IsAssigned = r.IsAssigned,
            Amount = r.CustomAmount ?? r.SessionAmount ?? 0m,
            IsUnpaid = r.UnpaidMonths > 0,
            UnpaidMonths = r.UnpaidMonths
        }).ToList();

        return (items, totalCount, countAll, countAssigned, countUnassigned);
    }

    /// <inheritdoc />
    public async Task<(IReadOnlyList<StudentByStatusRow> Items, int TotalCount, decimal GroupCollected, decimal GroupExpected, decimal GroupUnpaid)>
        GetStudentsByPaymentStatusPagedAsync(
            long teacherId, string status,
            DateTime monthStart, DateTime monthEnd,
            int page, int pageSize)
    {
        // Everything is judged THROUGH the selected month — periods that start after the
        // month end (pre-generated future months) are excluded from every bucket and total.
        var withPeriods = _context.PaymentPeriods
            .Where(p => p.TeacherId == teacherId && p.TeacherStudentId.HasValue
                && p.PeriodStart <= monthEnd);

        // Per-student classification by the earliest outstanding period (same rule as
        // GetStudentPaymentStatusCountsAsync). One row per student with an outstanding period.
        var earliestOutstanding = withPeriods
            .Where(p => p.PaymentStatus != PaymentStatus.Paid)
            .GroupBy(p => p.TeacherStudentId!.Value)
            .Select(g => new
            {
                StudentId = g.Key,
                IsProRated = g.OrderBy(p => p.PeriodSequence).First().IsProRated
            });

        // Materialize the target student-id set for the requested status (bounded to the
        // teacher's students; avoids deeply-nested subqueries the provider may not translate).
        List<long> targetIds;
        if (string.Equals(status, "paid", StringComparison.OrdinalIgnoreCase))
        {
            var allIds = await withPeriods.Select(p => p.TeacherStudentId!.Value).Distinct().ToListAsync();
            var outstanding = (await earliestOutstanding.Select(e => e.StudentId).ToListAsync()).ToHashSet();
            targetIds = allIds.Where(id => !outstanding.Contains(id)).ToList();
        }
        else if (string.Equals(status, "prorated", StringComparison.OrdinalIgnoreCase))
        {
            targetIds = await earliestOutstanding.Where(e => e.IsProRated).Select(e => e.StudentId).ToListAsync();
        }

        else if (string.Equals(status, "partial", StringComparison.OrdinalIgnoreCase))
        {
            // "Part Paid" chip: students with a period IN the requested month that is
            // partially settled (0 < AmountPaid < AmountDue). Month-scoped by design —
            // the screen header is month-relative ("monthly collected (march)").
            targetIds = await _context.PaymentPeriods
                .Where(p => p.TeacherId == teacherId
                    && p.TeacherStudentId.HasValue
                    && p.PeriodStart >= monthStart && p.PeriodStart <= monthEnd
                    && p.PaymentStatus == PaymentStatus.PartiallyPaid)
                .Select(p => p.TeacherStudentId!.Value)
                .Distinct()
                .ToListAsync();
        }
        else // unpaid
        {
            targetIds = await earliestOutstanding.Where(e => !e.IsProRated).Select(e => e.StudentId).ToListAsync();
        }

        int totalCount = targetIds.Count;

        // Group aggregates: month-scoped collected/expected, plus total outstanding.
        var groupMonthPeriods = _context.PaymentPeriods
            .Where(p => p.TeacherId == teacherId
                && p.TeacherStudentId.HasValue
                && targetIds.Contains(p.TeacherStudentId!.Value)
                && p.PeriodStart >= monthStart && p.PeriodStart <= monthEnd);

        decimal groupCollected = await groupMonthPeriods.SumAsync(p => (decimal?)p.AmountPaid) ?? 0m;
        decimal groupExpected = await groupMonthPeriods.SumAsync(p => (decimal?)p.AmountDue) ?? 0m;
        // Outstanding is the arrears THROUGH the selected month only (not the all-time counter,
        // which includes pre-generated future months): sum of (due - paid) over unpaid periods
        // whose start is on/before the month end.
        decimal groupUnpaid = await _context.PaymentPeriods
            .Where(p => p.TeacherId == teacherId
                && p.TeacherStudentId.HasValue
                && targetIds.Contains(p.TeacherStudentId!.Value)
                && p.PeriodStart <= monthEnd
                && p.PaymentStatus != PaymentStatus.Paid)
            .SumAsync(p => (decimal?)(p.AmountDue - p.AmountPaid)) ?? 0m;

        // Page the students (name order) with their month amounts + counter fields.
        var items = await _context.TeacherStudents
            .Where(ts => ts.TeacherId == teacherId && targetIds.Contains(ts.Id))
            .OrderBy(ts => ts.StudentName)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(ts => new StudentByStatusRow
            {
                TeacherStudentId = ts.Id,
                StudentName = ts.StudentName,
                AmountPerMonth =
                    (_context.StudentPaymentCounters
                        .Where(c => c.TeacherId == teacherId && c.TeacherStudentId == ts.Id)
                        .Select(c => c.CustomPaymentAmount).FirstOrDefault())
                    ?? (ts.Session != null ? ts.Session.SessionAmount : 0m),
                AmountPaid = _context.PaymentPeriods
                    .Where(p => p.TeacherId == teacherId && p.TeacherStudentId == ts.Id
                        && p.PeriodStart >= monthStart && p.PeriodStart <= monthEnd)
                    .Sum(p => (decimal?)p.AmountPaid) ?? 0m,
                AmountDue = _context.PaymentPeriods
                    .Where(p => p.TeacherId == teacherId && p.TeacherStudentId == ts.Id
                        && p.PeriodStart >= monthStart && p.PeriodStart <= monthEnd)
                    .Sum(p => (decimal?)p.AmountDue) ?? 0m,
                // Arrears THROUGH the selected month only (not the all-time counter): sum of
                // (due - paid) and count of unpaid periods whose start is on/before month end.
                UnpaidAmount = _context.PaymentPeriods
                    .Where(p => p.TeacherId == teacherId && p.TeacherStudentId == ts.Id
                        && p.PeriodStart <= monthEnd && p.PaymentStatus != PaymentStatus.Paid)
                    .Sum(p => (decimal?)(p.AmountDue - p.AmountPaid)) ?? 0m,
                UnpaidMonths = _context.PaymentPeriods
                    .Where(p => p.TeacherId == teacherId && p.TeacherStudentId == ts.Id
                        && p.PeriodStart <= monthEnd && p.PaymentStatus != PaymentStatus.Paid)
                    .Count(),
                    StudentCode = ts.StudentCode,

                // "Paid on" + "session he paid on": the student's latest paying transaction
                // whose PERIOD falls in the requested month. Deterministic tiebreak (Id) so
                // the two subqueries below always resolve to the same transaction. The global
                // query filter already excludes soft-deleted transactions.
                PaidOn = _context.PaymentTransactions
                    .Where(t => t.TeacherId == teacherId
                        && t.TeacherStudentId == ts.Id
                        && t.PaymentPeriod != null
                        && t.PaymentPeriod.PeriodStart >= monthStart
                        && t.PaymentPeriod.PeriodStart <= monthEnd)
                    .OrderByDescending(t => t.CollectedAt)
                    .ThenByDescending(t => t.Id)
                    .Select(t => (DateTime?)t.CollectedAt)
                    .FirstOrDefault(),

                SessionName =
                    _context.PaymentTransactions
                        .Where(t => t.TeacherId == teacherId
                            && t.TeacherStudentId == ts.Id
                            && t.PaymentPeriod != null
                            && t.PaymentPeriod.PeriodStart >= monthStart
                            && t.PaymentPeriod.PeriodStart <= monthEnd)
                        .OrderByDescending(t => t.CollectedAt)
                        .ThenByDescending(t => t.Id)
                        .Select(t => t.SessionName)
                        .FirstOrDefault()
                    // Fallback (unpaid/prorated, no payment yet): the month's period session.
                    ?? _context.PaymentPeriods
                        .Where(p => p.TeacherId == teacherId
                            && p.TeacherStudentId == ts.Id
                            && p.PeriodStart >= monthStart && p.PeriodStart <= monthEnd)
                        .OrderBy(p => p.PeriodSequence)
                        .Select(p => p.SessionName)
                        .FirstOrDefault()
            })
            .AsNoTracking()
            .ToListAsync();

        return (items, totalCount, groupCollected, groupExpected, groupUnpaid);
    }

    /// <inheritdoc />
    public async Task<(IReadOnlyList<YearlyStudentRow> Items, int TotalCount)>
        GetYearlyCollectionsPagedAsync(
            long teacherId, DateTime yearStart, DateTime yearEnd, int page, int pageSize)
    {
        // Students with at least one period in the year.
        var studentIdsInYear = _context.PaymentPeriods
            .Where(p => p.TeacherId == teacherId && p.TeacherStudentId.HasValue
                && p.PeriodStart >= yearStart && p.PeriodStart <= yearEnd)
            .Select(p => p.TeacherStudentId!.Value)
            .Distinct();

        int totalCount = await _context.TeacherStudents
            .CountAsync(ts => ts.TeacherId == teacherId && studentIdsInYear.Contains(ts.Id));

        var pageStudents = await _context.TeacherStudents
            .Where(ts => ts.TeacherId == teacherId && studentIdsInYear.Contains(ts.Id))
            .OrderBy(ts => ts.StudentName)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(ts => new { ts.Id, ts.StudentName })
            .AsNoTracking()
            .ToListAsync();

        var pageIds = pageStudents.Select(s => s.Id).ToList();

        // Aggregate the page students' year periods per (student, calendar month). Count-based
        // paid/prorated flags (not All/Any) so the whole thing translates to a single GROUP BY.
        var monthAgg = await _context.PaymentPeriods
            .Where(p => p.TeacherId == teacherId && p.TeacherStudentId.HasValue
                && pageIds.Contains(p.TeacherStudentId!.Value)
                && p.PeriodStart >= yearStart && p.PeriodStart <= yearEnd)
            .GroupBy(p => new { StudentId = p.TeacherStudentId!.Value, Month = p.PeriodStart.Month })
            .Select(g => new
            {
                g.Key.StudentId,
                g.Key.Month,
                AmountDue = g.Sum(x => x.AmountDue),
                AmountPaid = g.Sum(x => x.AmountPaid),
                Periods = g.Count(),
                PaidCount = g.Sum(x => x.PaymentStatus == PaymentStatus.Paid ? 1 : 0),
                ProRatedCount = g.Sum(x => x.IsProRated ? 1 : 0)
            })
            .AsNoTracking()
            .ToListAsync();

        var cellsByStudent = monthAgg
            .GroupBy(m => m.StudentId)
            .ToDictionary(
                g => g.Key,
                g => g.OrderBy(m => m.Month).Select(m => new YearlyMonthCell
                {
                    Month = m.Month,
                    AmountDue = m.AmountDue,
                    AmountPaid = m.AmountPaid,
                    IsPaid = m.Periods == m.PaidCount,
                    IsProRated = m.ProRatedCount > 0
                }).ToList());

        var items = pageStudents.Select(s => new YearlyStudentRow
        {
            TeacherStudentId = s.Id,
            StudentName = s.StudentName,
            Months = cellsByStudent.TryGetValue(s.Id, out var cells) ? cells : new List<YearlyMonthCell>()
        }).ToList();

        return (items, totalCount);
    }

    /// <inheritdoc />
    public async Task<CollectLookupRow?> ResolveCollectLookupAsync(
        long teacherId, string? qr, string? code, string? name, DateTime throughMonthEnd)
    {
        var q = _context.TeacherStudents.Where(ts => ts.TeacherId == teacherId);

        // Resolution priority: QR/barcode → student code → name (first match).
        if (!string.IsNullOrWhiteSpace(qr))
        {
            string barcode = qr.Trim();
            q = q.Where(ts => ts.Barcode == barcode || ts.StudentCode == barcode);
        }
        else if (!string.IsNullOrWhiteSpace(code))
        {
            string c = code.Trim().ToUpper(); // codes stored uppercase (REQ-STU-CODE-003)
            q = q.Where(ts => ts.StudentCode == c);
        }
        else if (!string.IsNullOrWhiteSpace(name))
        {
            string n = name.Trim().ToLower();
            q = q.Where(ts => ts.StudentName.ToLower().Contains(n));
        }
        else
        {
            return null;
        }

        var student = await q
            .OrderBy(ts => ts.StudentName)
            .Select(ts => new
            {
                ts.Id,
                ts.StudentName,
                ts.StudentCode,
                Group = ts.Session != null ? ts.Session.SessionName : null,
                SessionAmount = ts.Session != null ? ts.Session.SessionAmount : (decimal?)null
            })
            .FirstOrDefaultAsync();

        if (student is null) return null;

        // AmountDue = the student's TOTAL arrears through the selected/current month (sum of every
        // unpaid month's remaining), not a single month. Excludes months in advance. IsUnpaid is
        // simply whether any such arrears exist.
        decimal overdueTotal = await _context.PaymentPeriods
            .Where(p => p.TeacherId == teacherId && p.TeacherStudentId == student.Id
                && p.PaymentStatus != PaymentStatus.Paid
                && p.PeriodStart <= throughMonthEnd)
            .SumAsync(p => (decimal?)(p.AmountDue - p.AmountPaid)) ?? 0m;

        return new CollectLookupRow
        {
            TeacherStudentId = student.Id,
            StudentName = student.StudentName,
            StudentCode = student.StudentCode,
            Group = student.Group,
            AmountDue = overdueTotal,
            IsUnpaid = overdueTotal > 0m
        };
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
    public async Task<IReadOnlyList<ActiveSessionCollectionSummaryRow>> GetActiveSessionsCollectionSummaryAsync(
        long teacherId)
    {
        var today = DateTime.UtcNow.Date;

        var sessions = await _context.Sessions
            .Where(s => s.TeacherId == teacherId && s.EndDate >= today)
            .OrderBy(s => s.StartTime)
            .Select(s => new
            {
                s.Id,
                s.SessionName,
                s.OccurrenceType,
                s.SelectedDays,
                s.MonthlyDayOfMonth,
                s.StartTime,
                TotalStudents = s.TeacherStudents.Count
            })
            .ToListAsync();

        if (sessions.Count == 0)
            return Array.Empty<ActiveSessionCollectionSummaryRow>();

        var sessionIds = sessions.Select(s => s.Id).ToList();

        var financials = await _context.PaymentPeriods
            .Where(p => p.TeacherId == teacherId
                && p.SessionId.HasValue && sessionIds.Contains(p.SessionId.Value))
            .GroupBy(p => p.SessionId!.Value)
            .Select(g => new
            {
                SessionId = g.Key,
                Expected = g.Sum(p => p.AmountDue),
                Collected = g.Sum(p => p.AmountPaid)
            })
            .ToDictionaryAsync(x => x.SessionId, x => x);

        // Distinct-student unpaid count per session (same pattern as
        // CountUnpaidStudentsBySessionAsync). PaidStudents = TotalStudents - unpaid.
        var unpaidCounts = await _context.PaymentPeriods
            .Where(p => p.TeacherId == teacherId
                && p.SessionId.HasValue && sessionIds.Contains(p.SessionId.Value)
                && p.TeacherStudentId.HasValue
                && p.PaymentStatus != PaymentStatus.Paid)
            .Select(p => new { SessionId = p.SessionId!.Value, p.TeacherStudentId })
            .Distinct()
            .GroupBy(p => p.SessionId)
            .Select(g => new { SessionId = g.Key, UnpaidCount = g.Count() })
            .ToDictionaryAsync(x => x.SessionId, x => x.UnpaidCount);

        return sessions.Select(s =>
        {
            financials.TryGetValue(s.Id, out var fin);
            unpaidCounts.TryGetValue(s.Id, out var unpaidCount);

            return new ActiveSessionCollectionSummaryRow
            {
                SessionId = s.Id,
                SessionName = s.SessionName,
                OccurrenceType = s.OccurrenceType,
                SelectedDays = s.SelectedDays,
                MonthlyDayOfMonth = s.MonthlyDayOfMonth,
                StartTime = s.StartTime,
                TotalStudents = s.TotalStudents,
                PaidStudents = Math.Max(0, s.TotalStudents - unpaidCount),
                ExpectedAmount = fin?.Expected ?? 0,
                CollectedAmount = fin?.Collected ?? 0
            };
        }).ToList();
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
    public async Task<long?> GetLatestCollectorUserIdForStudentSessionAsync(
        long teacherId, long teacherStudentId, long sessionId)
    {
        return await _context.PaymentTransactions
            .Where(t => t.TeacherId == teacherId
                     && t.TeacherStudentId == teacherStudentId
                     && t.SessionId == sessionId
                     && !t.IsDeleted)
            .OrderByDescending(t => t.Id)
            .Select(t => t.CollectedByUserId)
            .FirstOrDefaultAsync();
    }

    /// <inheritdoc />
    public async Task NullifySessionIdOnPaymentRecordsAsync(long sessionId)
    {
        // IgnoreQueryFilters so SOFT-DELETED records are nullified too. A refunded/deleted payment
        // transaction (IsDeleted=true) still points at the session via a NO-ACTION FK; if it isn't
        // nullified here, the session's hard delete fails with a 409 conflict.
        // Nullify on PaymentTransactions
        await _context.PaymentTransactions
            .IgnoreQueryFilters()
            .Where(t => t.SessionId == sessionId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(t => t.SessionId, (long?)null));

        // Nullify on PaymentPeriods
        await _context.PaymentPeriods
            .IgnoreQueryFilters()
            .Where(p => p.SessionId == sessionId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(p => p.SessionId, (long?)null));

        // Nullify on StudentDepartures
        await _context.StudentDepartures
            .IgnoreQueryFilters()
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