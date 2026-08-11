using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using Edvanz.Application.Dtos;
using Edvanz.Application.Dtos.Payment;
using Edvanz.Application.ServiceContract;
using Edvanz.Domain.Constants;
using Edvanz.Domain.Enums;
using Edvanz.Domain.Interfaces;
using Microsoft.Extensions.Localization;

namespace Edvanz.Application.Services;

/// <summary>
/// Implements the screen-oriented payment endpoints (api/v1/*). Reuse-first: delegates to
/// existing <see cref="IPaymentRepo"/> named methods (and, for money movement in Phase 2,
/// <c>IPaymentService</c>) — no payment mutation logic is duplicated here. All reads are
/// tenant-scoped by the <c>teacherId</c> the controller resolves from the JWT.
/// </summary>
public class PaymentScreenService : IPaymentScreenService
{
    private static readonly JsonSerializerOptions IdempotencyJson = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly IUnitOfWork _unitOfWork;
    private readonly IStringLocalizer<Domain.Resources.Messages> _localizer;
    private readonly IPaymentService _paymentService;
    private readonly IIdempotencyService _idempotency;
    private readonly ITimeZoneService _timeZoneService;

    public PaymentScreenService(
        IUnitOfWork unitOfWork,
        IStringLocalizer<Domain.Resources.Messages> localizer,
        IPaymentService paymentService,
        IIdempotencyService idempotency,
        ITimeZoneService timeZoneService)
    {
        _unitOfWork = unitOfWork;
        _localizer = localizer;
        _paymentService = paymentService;
        _idempotency = idempotency;
        _timeZoneService = timeZoneService;
    }

    /// <summary>
    /// Last day of the teacher's current local (Africa/Cairo) month — the default
    /// "through this month" boundary for screens that carry no explicit month.
    /// </summary>
    private DateTime CurrentMonthEnd(long teacherId)
    {
        var today = _timeZoneService.GetTeacherLocalDate(teacherId);
        var monthStart = new DateTime(today.Year, today.Month, 1);
        return monthStart.AddMonths(1).AddDays(-1);
    }

    /// <inheritdoc />
    /// <inheritdoc />
    public async Task<Result<List<WalletResetLogDto>>> GetWalletWithdrawalHistoryAsync(
        long teacherId, long assistantId)
    {
        // Every withdrawal/reset the teacher took from this assistant's wallet — the "receipt"
        // trail (amount + when + who) that answers "where did the money I took go?". The cash was
        // already counted as revenue at collection time; this ledger records the hand-over.
        var logs = await _unitOfWork.PaymentsRepo
            .GetWalletResetLogsAsync(teacherId, assistantId);

        var dtos = logs
            .OrderByDescending(l => l.ResetAt)
            .Select(l => new WalletResetLogDto
            {
                Id = l.Id,
                AssistantName = l.AssistantName,
                AmountReset = l.AmountReset,
                ResetAt = l.ResetAt,
                ResetByUserId = l.ResetByUserId
            })
            .ToList();

        return Result<List<WalletResetLogDto>>.Success(
            dtos, _localizer, PaymentConstants.Messages.Success);
    }

    public async Task<Result<CollectionsByMonthResponse>> GetCollectionsByMonthAsync(
        long teacherId, string? month, int? year, int page, int limit,
        long? collectedByUserId = null)
    {
        // DASH-1: accept the SAME unified "YYYY-MM" month selector tracking/students take, while
        // still honouring the legacy ?month=<int 1-12>&year=<int> form; default to the teacher's
        // current local (Africa/Cairo) month/year when nothing is supplied (no more screen-load 422).
        var errorKey = ResolveCollectionsMonthYear(
            teacherId, month, year, out int resolvedYear, out int resolvedMonth);
        if (errorKey is not null)
            return Result<CollectionsByMonthResponse>.Failure(
                _localizer, errorKey, HttpStatusCode.UnprocessableEntity);

        (page, limit) = NormalizePaging(page, limit);

        var startDate = new DateTime(resolvedYear, resolvedMonth, 1);
        var endDate = startDate.AddMonths(1).AddDays(-1);
        var endExclusive = startDate.AddMonths(1);

        var (items, totalCount) = await _unitOfWork.PaymentsRepo
            .GetTransactionsByDateRangePagedAsync(
                teacherId, startDate, endDate,
                sessionId: null, collectedByUserId: collectedByUserId,
                page: page, pageSize: limit);

        // Student-departure refunds confirmed this month are money RETURNED to the student, so they
        // appear as negative-amount ledger lines carrying the departed month they apply to. They are
        // few (departures are rare), so the whole month's set is surfaced ONCE on page 1 (prepended,
        // most visible) and counted into the totals; later pages simply page the collections.
        // Skipped for a collector-scoped view (collectedByUserId set): a refund isn't "collected by"
        // anyone, so it doesn't belong in one collector's own-collections list.
        var refunds = collectedByUserId is null
            ? await _unitOfWork.PaymentsRepo
                .GetDepartureRefundsByDateRangeAsync(teacherId, startDate, endExclusive)
            : (IReadOnlyList<DepartureRefundRow>)Array.Empty<DepartureRefundRow>();
        int refundCount = refunds.Count;

        int baseIndex = (page - 1) * limit;
        var rows = new List<CollectionRow>(items.Count + (page == 1 ? refundCount : 0));

        if (page == 1)
        {
            foreach (var r in refunds)
            {
                rows.Add(new CollectionRow
                {
                    Id = $"departure-refund-{r.Id.ToString(CultureInfo.InvariantCulture)}",
                    // Refund lines are rendered with a distinct marker, not a ledger ordinal.
                    Index = 0,
                    StudentId = r.StudentId?.ToString(CultureInfo.InvariantCulture),
                    StudentName = r.StudentName,
                    Amount = -r.RefundAmount,
                    Status = "refund",
                    IsRefund = true,
                    SessionName = r.SessionName,
                    RefundedForMonthLabel = r.RefundPeriodStart.HasValue
                        ? r.RefundPeriodStart.Value.ToString("MMMM yyyy", CultureInfo.InvariantCulture)
                        : null,
                    CollectedAt = r.DepartedAt
                });
            }
        }

        for (int i = 0; i < items.Count; i++)
        {
            var tx = items[i];
            rows.Add(new CollectionRow
            {
                Id = tx.Id.ToString(CultureInfo.InvariantCulture),
                Index = baseIndex + i + 1,
                StudentId = tx.TeacherStudentId?.ToString(CultureInfo.InvariantCulture),
                StudentName = tx.StudentName,
                Amount = tx.AmountPaid,
                Status = "collected",
                // Live session name; the transaction's copy is a stale-on-rename snapshot.
                SessionName = ResolveSessionName(tx.Session?.SessionName, tx.SessionName),
                CollectedAt = tx.CollectedAt
            });
        }

        // Pagination follows the collections; refunds live entirely on page 1. Guarantee at least one
        // page when a month has ONLY refunds so page 1 still renders.
        int transactionPages = totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)limit);
        var response = new CollectionsByMonthResponse
        {
            Month = resolvedMonth,
            Year = resolvedYear,
            MonthLabel = startDate.ToString("MMMM yyyy", CultureInfo.InvariantCulture),
            Page = page,
            Limit = limit,
            TotalItems = totalCount + refundCount,
            TotalPages = transactionPages == 0 ? (refundCount > 0 ? 1 : 0) : transactionPages,
            Items = rows
        };

        return Result<CollectionsByMonthResponse>.Success(
            response, _localizer, PaymentConstants.Messages.Success);
    }

    /// <inheritdoc />
    public async Task<Result<AssistantWalletScreenResponse>> GetAssistantWalletScreenAsync(
        long teacherId, long assistantId, int page, int limit,
        long? restrictToAssistantUserId = null, string? search = null)
    {
        // Tenant-scoped lookup: a wallet belonging to another teacher's assistant returns null → 404.
        // TODO(assistant-dashboard): interim own-scoping. When an assistant calls, resolve THEIR OWN
        // wallet by user id and ignore the requested assistantId so they can never open a peer's
        // wallet. The dedicated assistant dashboard is to be built end-to-end by frontend + backend.
        var wallet = restrictToAssistantUserId is long ownUserId
            ? await _unitOfWork.PaymentsRepo.GetAssistantWalletByUserIdAsync(teacherId, ownUserId)
            : await _unitOfWork.PaymentsRepo.GetAssistantWalletAsync(teacherId, assistantId);
        if (wallet is null)
            return Result<AssistantWalletScreenResponse>.Failure(
                _localizer, PaymentConstants.Messages.WalletNotFound, HttpStatusCode.NotFound);

        (page, limit) = NormalizePaging(page, limit);

        DateTime nowUtc = DateTime.UtcNow;

        // WINDOWING (2026-08-06): the old rule scoped the list to "since the last WalletResetLog".
        // But a PARTIAL withdrawal ALSO writes a WalletResetLog, so any withdrawal reset the window
        // and emptied the list (and TotalCashCollected) even while a real balance was still held.
        // We now reconstruct the held balance from the collector's full signed event stream —
        // collections (+), refunds (−) and wallet hand-overs/withdrawals (−) — and anchor the window
        // at the LAST moment that running balance returned to exactly 0 (a full hand-over). Every
        // event after that point is precisely what still constitutes CurrentBalance, so the card and
        // the list stay consistent after a partial withdrawal, and the ledger reads:
        //   collections − refunds − withdrawals == held balance.
        // Bounded per collector in practice; a SQL-side windowed query is a documented follow-up if a
        // single collector's un-zeroed history ever grows past a few hundred rows.
        var allTxns = await _unitOfWork.PaymentsRepo
            .GetCollectorTransactionsInRangeAsync(teacherId, wallet.AssistantUserId, DateTime.MinValue, nowUtc);
        var allRefunds = await _unitOfWork.PaymentsRepo
            .GetCollectorRefundsInRangeAsync(teacherId, wallet.AssistantUserId, DateTime.MinValue, nowUtc);
        // Wallet hand-overs (full reset OR partial withdrawal) are money OUT of the held balance and
        // belong in the same ledger as negative lines, keyed on the wallet's AssistantId.
        var allResets = await _unitOfWork.PaymentsRepo
            .GetWalletResetLogsAsync(teacherId, wallet.AssistantId);

        // NOTE: student-departure refunds are ALREADY in allRefunds — the departure-confirm path
        // reverses the period via ReverseDeparturePeriodAsync, which writes a PaymentEditLog(Reversed)
        // that GetCollectorRefundsInRangeAsync reads. Do NOT also read GetDepartureRefundsByDateRangeAsync
        // here or every departure refund is double-counted (breaks the collections − refunds − withdrawals
        // == CurrentBalance reconciliation).

        // One signed ledger. Amount carries the sign; Kind drives client rendering. CollectedAt is
        // the ordering instant. Both money reads stay on the UTC CollectedAt/EditedAt columns, and
        // ResetAt is UTC too, so the merged stream is instant-consistent (no local/UTC boundary).
        var ledger = new List<AssistantWalletCollectionItemDto>(
            allTxns.Count + allRefunds.Count + allResets.Count);
        ledger.AddRange(allTxns.Select(tx => new AssistantWalletCollectionItemDto
        {
            Id = tx.Id.ToString(CultureInfo.InvariantCulture),
            StudentId = tx.TeacherStudentId?.ToString(CultureInfo.InvariantCulture),
            StudentName = tx.StudentName,
            StudentCode = tx.StudentCode,
            // Live session name (eager-loaded); the transaction's own copy is a collection-time
            // snapshot that goes stale on rename, and is only the fallback for a deleted session.
            SessionName = ResolveSessionName(tx.Session?.SessionName, tx.SessionName),
            Amount = tx.AmountPaid,
            CollectedAt = tx.CollectedAt,
            Kind = "collection"
        }));
        ledger.AddRange(allRefunds.Select(r => new AssistantWalletCollectionItemDto
        {
            Id = $"refund-{r.Id.ToString(CultureInfo.InvariantCulture)}",
            StudentId = r.StudentId?.ToString(CultureInfo.InvariantCulture),
            StudentName = r.StudentName,
            StudentCode = r.StudentCode,
            SessionName = string.IsNullOrEmpty(r.SessionName) ? null : r.SessionName,
            Amount = -r.RefundAmount, // negative → refund taken back from this collector
            CollectedAt = r.RefundedAt,
            Kind = "refund"
        }));
        ledger.AddRange(allResets.Select(w => new AssistantWalletCollectionItemDto
        {
            Id = $"withdrawal-{w.Id.ToString(CultureInfo.InvariantCulture)}",
            StudentId = null,
            StudentName = null,
            StudentCode = null,
            SessionName = null,
            Amount = -w.AmountReset, // negative → cash handed over to the tutor
            CollectedAt = w.ResetAt,
            Kind = "withdrawal"
        }));

        // Chronological order; on a same-instant tie, apply money-IN before money-OUT so a
        // collect-then-handover at the same timestamp nets to the right running balance.
        ledger.Sort((a, b) =>
        {
            int byTime = a.CollectedAt.CompareTo(b.CollectedAt);
            return byTime != 0 ? byTime : b.Amount.CompareTo(a.Amount);
        });

        // Anchor: the running held balance's zero-crossings (each a full hand-over). We track the last
        // TWO so we can fall back to the just-closed period when the current one is empty.
        decimal running = 0m;
        int lastZeroIdx = -1;
        int prevZeroIdx = -1;
        for (int i = 0; i < ledger.Count; i++)
        {
            running += ledger[i].Amount;
            if (running == 0m) { prevZeroIdx = lastZeroIdx; lastZeroIdx = i; }
        }

        // Normally the window is everything strictly AFTER the last full hand-over (the current holding
        // period). BUGFIX (2026-08-11): a FULL withdrawal ("take everything") drives the running balance
        // to exactly 0 at the withdrawal event, so it becomes lastZeroIdx and Skip(lastZeroIdx + 1)
        // returns nothing — the drill-in went blank right after a hand-over, hiding both the students
        // the collector collected from AND the withdrawal/refund negative lines. When the current
        // holding period is empty (the most recent event fully zeroed the balance), fall back to the
        // period that JUST CLOSED: from the previous full hand-over up to AND INCLUDING this one, so
        // those collections and the closing withdrawal stay visible. That period nets to 0
        // (collections − refunds − withdrawal), consistent with WalletBalance = CurrentBalance = 0.
        DateTime? sinceAt;
        List<AssistantWalletCollectionItemDto> windowed;
        bool currentPeriodEmpty = lastZeroIdx == ledger.Count - 1; // last event zeroed the balance
        if (currentPeriodEmpty && lastZeroIdx >= 0)
        {
            windowed = ledger.Skip(prevZeroIdx + 1).Take(lastZeroIdx - prevZeroIdx).ToList();
            sinceAt = prevZeroIdx >= 0 ? ledger[prevZeroIdx].CollectedAt : (DateTime?)null;
        }
        else
        {
            windowed = ledger.Skip(lastZeroIdx + 1).ToList();
            sinceAt = lastZeroIdx >= 0 ? ledger[lastZeroIdx].CollectedAt : (DateTime?)null;
        }

        // Gross collected / refunded within the window, reported separately (not netted) so the card
        // explains the list: collected X, refunded Y; withdrawals appear as their own negative lines.
        decimal periodCollected = windowed
            .Where(i => i.Kind == "collection").Sum(i => i.Amount);
        decimal periodRefunded = windowed
            .Where(i => i.Kind == "refund").Sum(i => -i.Amount);

        // Filter the LISTED rows by student name OR studentCode (case-insensitive). Applied AFTER the
        // window + totals are computed, so the card stays authoritative — only the list is narrowed.
        // A search naturally drops withdrawal lines (they carry no student).
        var merged = windowed;
        if (!string.IsNullOrWhiteSpace(search))
        {
            string searchLower = search.Trim().ToLowerInvariant();
            merged = merged.Where(m =>
                (m.StudentName is not null && m.StudentName.ToLowerInvariant().Contains(searchLower))
                || (m.StudentCode is not null && m.StudentCode.ToLowerInvariant().Contains(searchLower)))
                .ToList();
        }

        int total = merged.Count;
        var items = merged
            .OrderByDescending(i => i.CollectedAt)
            .Skip((page - 1) * limit)
            .Take(limit)
            .ToList();

        var response = new AssistantWalletScreenResponse
        {
            Assistant = new AssistantWalletAssistantDto
            {
                Id = assistantId.ToString(CultureInfo.InvariantCulture),
                Name = wallet.Assistant?.User?.FullName,
                Role = "Assistant",
                AvatarUrl = null,
                TransactionCount = wallet.TransactionCount
            },
            Wallet = new AssistantWalletInfoDto
            {
                TotalCashCollected = periodCollected,
                TotalRefunded = periodRefunded,
                WalletBalance = wallet.CurrentBalance,
                TotalCollectedAllTime = wallet.TotalCollected,
                CollectionsCount = wallet.TransactionCount,
                LastActivityAt = wallet.LastCollectionAt
            },
            Collections = new AssistantWalletCollectionsDto
            {
                Total = total,
                Page = page,
                Limit = limit,
                SinceAt = sinceAt,
                Items = items
            }
        };

        return Result<AssistantWalletScreenResponse>.Success(
            response, _localizer, PaymentConstants.Messages.Success);
    }

    /// <inheritdoc />
    public async Task<Result<CollectStudentsResponse>> GetCollectStudentsAsync(
        long teacherId, string? filter, string? search, int page, int limit)
    {
        filter = string.IsNullOrWhiteSpace(filter) ? "all" : filter.Trim().ToLowerInvariant();
        if (filter != "all" && filter != "assigned" && filter != "unassigned")
            return Result<CollectStudentsResponse>.Failure(
                _localizer, PaymentConstants.Messages.PaymentInvalidCollectFilter,
                HttpStatusCode.UnprocessableEntity);

        (page, limit) = NormalizePaging(page, limit);

        var (rows, total, cAll, cAssigned, cUnassigned) = await _unitOfWork.PaymentsRepo
            .GetCollectStudentsPagedAsync(teacherId, filter, search, page, limit, CurrentMonthEnd(teacherId));

        var students = new List<CollectStudentDto>(rows.Count);
        foreach (var r in rows)
        {
            students.Add(new CollectStudentDto
            {
                Id = r.TeacherStudentId.ToString(CultureInfo.InvariantCulture),
                Name = r.StudentName,
                StudentCode = r.StudentCode,
                AvatarUrl = null,
                Amount = r.Amount,
                Assignment = r.IsAssigned ? "assigned" : "unassigned",
                Status = r.IsUnpaid ? "unpaid" : "paid",
                UnpaidMonths = r.UnpaidMonths,
                IsExempt = r.Amount == 0m
            });
        }

        var response = new CollectStudentsResponse
        {
            Counts = new CollectStudentsCountsDto { All = cAll, Assigned = cAssigned, Unassigned = cUnassigned },
            Page = page,
            Limit = limit,
            TotalItems = total,
            TotalPages = total == 0 ? 0 : (int)Math.Ceiling(total / (double)limit),
            Students = students
        };

        return Result<CollectStudentsResponse>.Success(
            response, _localizer, PaymentConstants.Messages.Success);
    }

    /// <inheritdoc />
    public async Task<Result<StudentsByStatusResponse>> GetStudentsByStatusAsync(
        long teacherId, string? month, string? status, int page, int limit,
        long? sessionId = null, string? search = null)
    {
        // PAY-2: default to the teacher's current local month when omitted (only 422 on malformed).
        if (!TryResolveMonth(teacherId, month, out int year, out int mon))
            return Result<StudentsByStatusResponse>.Failure(
                _localizer, PaymentConstants.Messages.PaymentInvalidMonthFormat,
                HttpStatusCode.UnprocessableEntity);

        // B1: status is now OPTIONAL. Null → the whole (session-scoped) roster, each student
        // carrying its own status. A SUPPLIED status still behaves exactly as before (422 on a
        // value that is neither paid|partial|prorated|unpaid).
        status = string.IsNullOrWhiteSpace(status) ? null : status.Trim().ToLowerInvariant();
        if (status is not null
            && status != "paid" && status != "partial" && status != "prorated" && status != "unpaid")
            return Result<StudentsByStatusResponse>.Failure(
                _localizer, PaymentConstants.Messages.PaymentInvalidStatusFilter,
                HttpStatusCode.UnprocessableEntity);
        (page, limit) = NormalizePaging(page, limit);
        var monthStart = new DateTime(year, mon, 1);
        var monthEnd = monthStart.AddMonths(1).AddDays(-1);

        var (rows, total, groupCollected, groupExpected, groupUnpaid, groupExpectedRate) = await _unitOfWork.PaymentsRepo
            .GetStudentsByPaymentStatusPagedAsync(
                teacherId, status, monthStart, monthEnd, page, limit, sessionId, search);

        var students = new List<StudentByStatusDto>(rows.Count);
        foreach (var r in rows)
        {
            students.Add(new StudentByStatusDto
            {
                Id = r.TeacherStudentId.ToString(CultureInfo.InvariantCulture),
                Name = r.StudentName,
                AvatarUrl = null,
                // With a filter every row matches the requested status; without one each row
                // carries its own repo-computed status (falls back to "unpaid" defensively).
                Status = status ?? r.Status ?? "unpaid",
                AmountPerMonth = r.AmountPerMonth,
                AmountPaid = r.AmountPaid,
                AmountDue = r.AmountDue,
                UnpaidAmount = r.UnpaidAmount,
                UnpaidMonths = r.UnpaidMonths,
                StudentCode = r.StudentCode,
                IsExempt = r.AmountPerMonth == 0m,
                SessionName = r.SessionName,
                PaidOn = r.PaidOn
            });
        }

        var response = new StudentsByStatusResponse
        {
            Month = $"{year:D4}-{mon:D2}",
            MonthLabel = monthStart.ToString("MMMM yyyy", CultureInfo.InvariantCulture),
            // Top-level status echoes the filter; "all" when the roster is mixed (no filter).
            Status = status ?? "all",
            TotalCollected = groupCollected,
            MonthAmount = groupExpected,
            // Ambiguous top-level "default fee": no single value exists (per-session/per-student).
            // The per-student amountPerMonth is authoritative; confirm intended meaning with FE.
            AmountPerMonth = 0m,
            // Full-set expected revenue for this scope (Σ per-student custom-or-session rate). The
            // session-detail screen renders this directly instead of session-default × student-count,
            // so a per-student custom amount is reflected in the expected total.
            ExpectedAmount = groupExpectedRate,
            TotalUnpaidAmount = status == "paid" ? 0m : groupUnpaid,
            Total = total,
            Page = page,
            Limit = limit,
            Students = students
        };

        return Result<StudentsByStatusResponse>.Success(
            response, _localizer, PaymentConstants.Messages.Success);
    }

    /// <inheritdoc />
    public async Task<Result<YearlyCollectionsResponse>> GetYearlyCollectionsAsync(
        long teacherId, string? month, int? year, int page, int limit)
    {
        // DASH-1: a yearly view only needs the YEAR — derive it from the unified "YYYY-MM" selector
        // (take its year component) OR the legacy ?year=<int>, defaulting to the teacher's current
        // local year when omitted. The month component (if any) is genuinely irrelevant here and is
        // neither used nor validated — a malformed month must not 422 a year-only screen.
        var errorKey = ResolveCollectionsYear(teacherId, month, year, out int resolvedYear);
        if (errorKey is not null)
            return Result<YearlyCollectionsResponse>.Failure(
                _localizer, errorKey, HttpStatusCode.UnprocessableEntity);

        (page, limit) = NormalizePaging(page, limit);
        var yearStart = new DateTime(resolvedYear, 1, 1);
        var yearEnd = new DateTime(resolvedYear, 12, 31);

        var (rows, total) = await _unitOfWork.PaymentsRepo
            .GetYearlyCollectionsPagedAsync(teacherId, yearStart, yearEnd, page, limit);

        int baseIndex = (page - 1) * limit;
        var items = new List<YearlyStudentDto>(rows.Count);
        for (int i = 0; i < rows.Count; i++)
        {
            var r = rows[i];
            var months = r.Months.Select(m => new YearlyMonthDto
            {
                Month = m.Month,
                Status = m.IsPaid ? "paid" : (m.IsProRated ? "prorated" : "unpaid"),
                Amount = m.AmountPaid
            }).ToList();

            int paidMonths = months.Count(m => m.Status == "paid");
            int unpaidMonths = months.Count(m => m.Status == "unpaid");
            decimal totalCollected = months.Sum(m => m.Amount);

            items.Add(new YearlyStudentDto
            {
                Id = r.TeacherStudentId.ToString(CultureInfo.InvariantCulture),
                Index = baseIndex + i + 1,
                StudentName = r.StudentName,
                Summary = new YearlyStudentSummaryDto
                {
                    PaidMonths = paidMonths,
                    UnpaidMonths = unpaidMonths,
                    TotalCollected = totalCollected,
                    Label = $"Paid {paidMonths} months, unpaid {unpaidMonths} months"
                },
                Months = months
            });
        }

        var response = new YearlyCollectionsResponse
        {
            Year = resolvedYear,
            Page = page,
            Limit = limit,
            TotalItems = total,
            TotalPages = total == 0 ? 0 : (int)Math.Ceiling(total / (double)limit),
            Items = items
        };

        return Result<YearlyCollectionsResponse>.Success(
            response, _localizer, PaymentConstants.Messages.Success);
    }

    /// <inheritdoc />
    public async Task<Result<CollectLookupResponse>> ResolveLookupAsync(
        long teacherId, string? qr, string? code, string? name)
    {
        if (string.IsNullOrWhiteSpace(qr) && string.IsNullOrWhiteSpace(code) && string.IsNullOrWhiteSpace(name))
            return Result<CollectLookupResponse>.Failure(
                _localizer, PaymentConstants.Messages.PaymentLookupCriteriaRequired,
                HttpStatusCode.UnprocessableEntity);

        var row = await _unitOfWork.PaymentsRepo.ResolveCollectLookupAsync(
            teacherId, qr, code, name, CurrentMonthEnd(teacherId));
        if (row is null)
            return Result<CollectLookupResponse>.Failure(
                _localizer, PaymentConstants.Messages.PaymentLookupStudentNotFound, HttpStatusCode.NotFound);

        var response = new CollectLookupResponse
        {
            Student = new CollectLookupStudentDto
            {
                Id = row.TeacherStudentId.ToString(CultureInfo.InvariantCulture),
                Name = row.StudentName,
                Code = row.StudentCode,
                Group = row.Group,
                AvatarUrl = null
            },
            AmountDue = row.AmountDue,
            PaymentStatus = row.IsUnpaid ? "unpaid" : "paid"
        };

        return Result<CollectLookupResponse>.Success(
            response, _localizer, PaymentConstants.Messages.Success);
    }

    /// <inheritdoc />
    public async Task<Result<TrackingResponse>> GetTrackingAsync(long teacherId, string? month)
    {
        // PAY-2: the main tracking-screen load carries no month by default → use the teacher's
        // current local month (only 422 when a month is provided but malformed).
        if (!TryResolveMonth(teacherId, month, out int year, out int mon))
            return Result<TrackingResponse>.Failure(
                _localizer, PaymentConstants.Messages.PaymentInvalidMonthFormat,
                HttpStatusCode.UnprocessableEntity);

        var monthStart = new DateTime(year, mon, 1);
        var monthEnd = monthStart.AddMonths(1).AddDays(-1);
        var repo = _unitOfWork.PaymentsRepo;

        // Expected = this month's obligation (period dues). Collected = ACTUAL cash physically
        // collected this calendar month (can exceed expected when students pay arrears or one
        // month ahead), attributed to the month it was taken — per the agreed dashboard rule.
        var (expected, _, _) =
            await repo.GetDashboardAggregatesAsync(teacherId, null, null, null, monthStart, monthEnd);
        decimal grossCollected = await repo.GetCashCollectedInRangeAsync(
            teacherId, null, monthStart, monthStart.AddMonths(1));
        // Net out departure refunds taken this month (by DepartedAt) so the headline
        // reconciles with the per-collector cards and the collections ledger, which are
        // already net of refunds. Without this the headline overstates cash by the full
        // refund total and contradicts the numbers directly below it.
        var monthRefunds = await repo.GetDepartureRefundsByDateRangeAsync(
            teacherId, monthStart, monthStart.AddMonths(1));
        decimal collected = grossCollected - monthRefunds.Sum(r => r.RefundAmount);
        decimal remaining = expected - collected;
        var (paidCount, proratedCount, unpaidCount) = await repo.GetStudentPaymentStatusCountsAsync(teacherId, monthEnd);
        var perSession = await repo.GetDashboardPerSessionAsync(teacherId, null, null, monthStart, monthEnd);
        var activeMeta = await repo.GetActiveSessionsCollectionSummaryAsync(teacherId);
        var collectors = await repo.GetDashboardPerCollectorAsync(teacherId, monthStart, monthEnd);
        // TotalStudents = students currently assigned to a session (the ones a teacher expects
        // to collect from), not every student on the account.
        int totalStudents = await repo.CountAssignedStudentsAsync(teacherId);

        var metaBySession = activeMeta.ToDictionary(m => m.SessionId);
        // Month-scoped per-session cash + distinct paying students, and current assigned totals.
        var monthColl = (await repo.GetSessionMonthCollectionAsync(teacherId, monthStart, monthStart.AddMonths(1)))
            .ToDictionary(x => x.SessionId);
        var assignedBySession = (await repo.GetAssignedStudentCountsPerSessionAsync(teacherId))
            .ToDictionary(x => x.SessionId, x => x.TotalStudents);

        // Enrich collectors with real names (repo returns null) + role (assistant vs teacher).
        var collectorUserIds = collectors.Select(c => c.UserId).Distinct().ToList();
        var names = collectorUserIds.Count > 0
            ? await _unitOfWork.Users.GetUserFullNamesByUserIdsAsync(collectorUserIds)
            : new Dictionary<long, string>();
        var assistantUserIds = (await _unitOfWork.AssistantRepo.GetUserIdsByTeacherAccountIdAsync(teacherId)).ToHashSet();

        // All assistant wallets (every assistant on the account, even those who collected nothing
        // this month), keyed by user id — the source for "show ALL assistants" + each one's held
        // balance, and the AssistantId the wallet drill-in route actually matches on (the collector
        // row's user id is NOT a valid wallet id, which is why tapping an assistant used to 404).
        var wallets = await _unitOfWork.PaymentsRepo.GetAllAssistantWalletsAsync(teacherId);
        var collectorByUser = collectors
            .GroupBy(c => c.UserId)
            .ToDictionary(g => g.Key, g => g.First());

        // Teacher row(s): collectors who are NOT assistants. The teacher has no wallet, but their
        // card is still shown (and the app links it to the teacher's own collections list).
        var teacherRows = collectors
            .Where(c => !assistantUserIds.Contains(c.UserId))
            .Select(c => new TrackingAssistantDto
            {
                Id = c.UserId.ToString(CultureInfo.InvariantCulture),
                Name = names.TryGetValue(c.UserId, out var nm) ? nm : c.UserName,
                AvatarUrl = null,
                Role = "Teacher",
                TransactionCount = c.TransactionCount,
                CollectedAmount = c.Collected,
                AssistantId = null,
                WalletBalance = 0m
            });

        // One row per assistant (from wallets = the full roster), overlaying this month's collection
        // totals where present. AssistantId is the wallet-navigation id; WalletBalance is cash held.
        var assistantRows = wallets
            .Select(w =>
            {
                // Value-tuple: a missing key yields a default (all-zero) tuple, so month totals
                // fall back to 0 for an assistant who didn't collect this month.
                collectorByUser.TryGetValue(w.AssistantUserId, out var c);
                return new TrackingAssistantDto
                {
                    Id = w.AssistantUserId.ToString(CultureInfo.InvariantCulture),
                    Name = w.Assistant?.User?.FullName
                        ?? (names.TryGetValue(w.AssistantUserId, out var an) ? an : null),
                    AvatarUrl = null,
                    Role = "Assistant",
                    TransactionCount = c.TransactionCount,
                    CollectedAmount = c.Collected,
                    AssistantId = w.AssistantId.ToString(CultureInfo.InvariantCulture),
                    WalletBalance = w.CurrentBalance
                };
            })
            // Most relevant first: collected this month, then those still holding cash, then name.
            .OrderByDescending(a => a.CollectedAmount)
            .ThenByDescending(a => a.WalletBalance)
            .ThenBy(a => a.Name);

        var assistants = teacherRows.Concat(assistantRows).ToList();

        // Sessions: everything month-scoped. CollectedAmount = actual cash into the session this
        // month; StudentsCollected = distinct students who paid into it this month; StudentsTotal =
        // students currently assigned to it. Schedule (day/time) still comes from the session meta.
        var sessions = perSession.Select(s =>
        {
            metaBySession.TryGetValue(s.SessionId, out var meta);
            monthColl.TryGetValue(s.SessionId, out var mc);
            decimal cash = mc.CashCollected;
            int paidStudents = mc.PaidStudents;
            int totalStudents = assignedBySession.TryGetValue(s.SessionId, out var tot) ? tot : 0;
            decimal pct = s.Expected > 0 ? Math.Round(cash / s.Expected * 100m, 2) : 0m;
            return new TrackingSessionDto
            {
                Id = s.SessionId.ToString(CultureInfo.InvariantCulture),
                Title = s.SessionName,
                Day = meta?.SelectedDays,
                Time = meta != null ? FormatTime(meta.StartTime) : null,
                Grade = null,
                CollectedAmount = cash,
                StudentsCollected = paidStudents,
                StudentsTotal = totalStudents,
                ProgressPercent = pct,
                GroupId = s.SessionGroupId?.ToString(CultureInfo.InvariantCulture),
                GroupName = s.SessionGroupName
            };
        }).ToList();

        int sessionsTotal = sessions.Count;
        int sessionsCollected = sessions.Count(s => s.CollectedAmount > 0m);
        decimal progressPercent = expected > 0 ? Math.Round(collected / expected * 100m, 2) : 0m;

        var response = new TrackingResponse
        {
            Month = $"{year:D4}-{mon:D2}",
            MonthLabel = monthStart.ToString("MMMM yyyy", CultureInfo.InvariantCulture),
            Summary = new TrackingSummaryDto
            {
                ExpectedRevenue = expected,
                TotalStudents = totalStudents,
                TotalSessions = sessionsTotal,
                CollectedAmount = collected,
                RemainingAmount = remaining,
                SessionsCollected = sessionsCollected,
                SessionsTotal = sessionsTotal,
                ProgressPercent = progressPercent
            },
            StatusBreakdown = new TrackingStatusBreakdownDto
            {
                Paid = paidCount,
                Prorated = proratedCount,
                Unpaid = unpaidCount
            },
            CollectedByAssistant = new TrackingByAssistantDto
            {
                TotalCollected = collectors.Sum(c => c.Collected),
                Assistants = assistants
            },
            CollectedBySessions = new TrackingBySessionsDto { Sessions = sessions }
        };

        return Result<TrackingResponse>.Success(response, _localizer, PaymentConstants.Messages.Success);
    }

    /// <inheritdoc />
    public async Task<Result<MarkPaidResponse>> MarkPaidAsync(
        long teacherId, long actingUserId, List<long> studentIds, string? idempotencyKey)
    {
        if (studentIds is null || studentIds.Count == 0)
            return Result<MarkPaidResponse>.Failure(
                _localizer, PaymentConstants.Messages.PaymentNoStudentsSelected,
                HttpStatusCode.UnprocessableEntity);

        // Idempotent replay: return the stored result instead of re-collecting.
        var stored = await _idempotency.GetStoredResultAsync("mark-paid", teacherId, idempotencyKey);
        if (stored is not null)
            return Result<MarkPaidResponse>.Success(
                JsonSerializer.Deserialize<MarkPaidResponse>(stored, IdempotencyJson)!,
                _localizer, PaymentConstants.Messages.Success);

        var results = new List<MarkPaidResultDto>();
        int markedPaid = 0;

        foreach (var studentId in studentIds.Distinct())
        {
            string idStr = studentId.ToString(CultureInfo.InvariantCulture);

            var student = await _unitOfWork.Students.GetActiveByIdAndTeacherAsync(studentId, teacherId);
            if (student is null)
            {
                results.Add(new MarkPaidResultDto { StudentId = idStr, Status = "failed", Reason = "Student not found." });
                continue;
            }
            if (student.SessionId is null)
            {
                results.Add(new MarkPaidResultDto { StudentId = idStr, Status = "failed", Reason = "Student is not assigned to a session." });
                continue;
            }

            // Amount = the student's TOTAL arrears through the current month (all overdue months,
            // not just the earliest). The collection engine then cascades it across those months,
            // oldest first, clearing each. Already paid → no-op success.
            decimal amount = await _unitOfWork.PaymentsRepo
                .GetOverdueTotalThroughAsync(teacherId, studentId, student.SessionId, CurrentMonthEnd(teacherId));
            if (amount <= 0m)
            {
                results.Add(new MarkPaidResultDto { StudentId = idStr, Status = "paid", Reason = "Already paid." });
                markedPaid++;
                continue;
            }

            // Route through the single proven collection path (its own transaction per student).
            var collect = await _paymentService.CollectPaymentAsync(new CollectPaymentDto
            {
                TeacherId = teacherId,
                TeacherStudentId = studentId,
                SessionId = student.SessionId.Value,
                Amount = amount,
                PaymentMethod = PaymentCollectionMethod.ManualCode,
                CollectedByUserId = actingUserId,
                DuplicateConfirmed = false,
                AlreadyPaidConfirmed = false
            });

            if (collect.IsSuccess && collect.Data?.Transaction is not null)
            {
                results.Add(new MarkPaidResultDto { StudentId = idStr, Status = "paid", Reason = null });
                markedPaid++;
            }
            else if (collect.Data?.IsAlreadyPaid == true)
            {
                results.Add(new MarkPaidResultDto { StudentId = idStr, Status = "paid", Reason = "Already paid." });
                markedPaid++;
            }
            else if (collect.Data?.IsSameDayDuplicate == true)
            {
                results.Add(new MarkPaidResultDto { StudentId = idStr, Status = "failed", Reason = "Already collected today." });
            }
            else
            {
                results.Add(new MarkPaidResultDto { StudentId = idStr, Status = "failed", Reason = collect.Message });
            }
        }

        var response = new MarkPaidResponse { MarkedPaidCount = markedPaid, Results = results };
        await _idempotency.StoreResultAsync("mark-paid", teacherId, idempotencyKey,
            JsonSerializer.Serialize(response, IdempotencyJson));
        return Result<MarkPaidResponse>.Success(response, _localizer, PaymentConstants.Messages.Success);
    }

    /// <inheritdoc />
    public async Task<Result<SubmitCollectionResponse>> SubmitCollectionAsync(
        long teacherId, long actingUserId, string? month, long? classSessionId,
        List<SubmitCollectionItem> students, string? idempotencyKey)
    {
        if (students is null || students.Count == 0)
            return Result<SubmitCollectionResponse>.Failure(
                _localizer, PaymentConstants.Messages.PaymentSubmitBatchEmpty, HttpStatusCode.Conflict);

        var stored = await _idempotency.GetStoredResultAsync("submit", teacherId, idempotencyKey);
        if (stored is not null)
            return Result<SubmitCollectionResponse>.Success(
                JsonSerializer.Deserialize<SubmitCollectionResponse>(stored, IdempotencyJson)!,
                _localizer, PaymentConstants.Messages.Success);

        var results = new List<SubmitCollectionResultDto>(students.Count);
        int submitted = 0;
        decimal totalCollected = 0m;
        // PAY-6: a studentId repeated in the batch must NOT be collected twice — mark repeats failed
        // (mark-paid dedupes with .Distinct(); submit carries per-item amounts, so report per item).
        var seen = new HashSet<long>();

        foreach (var item in students)
        {
            string idStr = item.StudentId.ToString(CultureInfo.InvariantCulture);

            if (!seen.Add(item.StudentId))
            {
                results.Add(new SubmitCollectionResultDto { StudentId = idStr, Status = "failed", Reason = "Duplicate student in this batch." });
                continue;
            }

            if (item.Amount <= 0m)
            {
                results.Add(new SubmitCollectionResultDto { StudentId = idStr, Status = "failed", Reason = "Invalid amount." });
                continue;
            }

            var student = await _unitOfWork.Students.GetActiveByIdAndTeacherAsync(item.StudentId, teacherId);
            if (student is null)
            {
                results.Add(new SubmitCollectionResultDto { StudentId = idStr, Status = "failed", Reason = "Student not found." });
                continue;
            }

            long? sessionId = classSessionId ?? student.SessionId;
            if (sessionId is null)
            {
                results.Add(new SubmitCollectionResultDto { StudentId = idStr, Status = "failed", Reason = "No session for this student." });
                continue;
            }

            // Explicit submit after client-side review → confirm duplicates/already-paid (like batch-collect).
            var collect = await _paymentService.CollectPaymentAsync(new CollectPaymentDto
            {
                TeacherId = teacherId,
                TeacherStudentId = item.StudentId,
                SessionId = sessionId.Value,
                Amount = item.Amount,
                PaymentMethod = PaymentCollectionMethod.BarcodeScan,
                CollectedByUserId = actingUserId,
                DuplicateConfirmed = true,
                AlreadyPaidConfirmed = true
            });

            if (collect.IsSuccess && collect.Data?.Transaction is not null)
            {
                results.Add(new SubmitCollectionResultDto { StudentId = idStr, Status = "committed", Reason = null });
                submitted++;
                totalCollected += collect.Data.Transaction.AmountPaid;
            }
            else
            {
                results.Add(new SubmitCollectionResultDto { StudentId = idStr, Status = "failed", Reason = collect.Message });
            }
        }

        var response = new SubmitCollectionResponse
        {
            SubmittedCount = submitted,
            TotalCollected = totalCollected,
            Results = results
        };
        await _idempotency.StoreResultAsync("submit", teacherId, idempotencyKey,
            JsonSerializer.Serialize(response, IdempotencyJson));
        return Result<SubmitCollectionResponse>.Success(response, _localizer, PaymentConstants.Messages.Success);
    }

    /// <inheritdoc />
    public async Task<Result<WalletWithdrawResponse>> WithdrawAsync(
        long teacherId, long assistantId, decimal? amount, long actingUserId, string? idempotencyKey)
    {
        var stored = await _idempotency.GetStoredResultAsync("withdraw", teacherId, idempotencyKey);
        if (stored is not null)
            return Result<WalletWithdrawResponse>.Success(
                JsonSerializer.Deserialize<WalletWithdrawResponse>(stored, IdempotencyJson)!,
                _localizer, PaymentConstants.Messages.Success);

        var result = await _paymentService.WithdrawFromWalletAsync(teacherId, assistantId, amount, actingUserId);
        if (!result.IsSuccess || result.Data is null)
            // PAY-5: propagate the underlying localized message + stable code (now set by
            // WithdrawFromWalletAsync) instead of dropping them behind a hardcoded English string.
            return Result<WalletWithdrawResponse>.Failure(result);

        var r = result.Data;
        var response = new WalletWithdrawResponse
        {
            WithdrawalId = r.WithdrawalId.ToString(CultureInfo.InvariantCulture),
            Status = "completed",
            Amount = r.Amount,
            WalletBalanceAfter = r.WalletBalanceAfter,
            RequestedAt = r.RequestedAt
        };
        // Only successful withdrawals are cached (a failed attempt can be retried freely).
        await _idempotency.StoreResultAsync("withdraw", teacherId, idempotencyKey,
            JsonSerializer.Serialize(response, IdempotencyJson));
        return Result<WalletWithdrawResponse>.Success(response, _localizer, PaymentConstants.Messages.Success);
    }

    /// <summary>Formats a session start time as e.g. "5:00 PM".</summary>
    private static string FormatTime(TimeSpan t) =>
        new DateTime(1, 1, 1).Add(t).ToString("h:mm tt", CultureInfo.InvariantCulture);

    /// <summary>
    /// The session name to DISPLAY: the live <c>Session.SessionName</c> when the session still
    /// exists, else the denormalized snapshot stored on the payment row. Payment rows copy the name
    /// at write time, so a session renamed afterwards kept showing its OLD name on the payment
    /// screens (and rows written either side of a rename listed one session under two names). The
    /// snapshot is retained purely so a DELETED session still reads sensibly in the money history.
    /// </summary>
    private static string? ResolveSessionName(string? liveName, string? snapshotName)
    {
        if (!string.IsNullOrEmpty(liveName))
            return liveName;
        return string.IsNullOrEmpty(snapshotName) ? null : snapshotName;
    }

    /// <summary>
    /// Resolves the "YYYY-MM" month selector, DEFAULTING to the teacher's current local
    /// (Africa/Cairo) month when it is omitted (PAY-2) — matching the documented "screens with no
    /// explicit month use the teacher's current local month" contract (§7.4). Returns false ONLY
    /// when a value is provided but malformed/out of range.
    /// </summary>
    private bool TryResolveMonth(long teacherId, string? month, out int year, out int mon)
    {
        if (string.IsNullOrWhiteSpace(month))
        {
            var today = _timeZoneService.GetTeacherLocalDate(teacherId);
            year = today.Year;
            mon = today.Month;
            return true;
        }
        return TryParseYearMonth(month, out year, out mon);
    }

    /// <summary>
    /// DASH-1: unifies the month selector for the collections screens (<c>collections</c> and
    /// <c>collections/yearly</c>) so the SAME value that drives <c>tracking</c>/<c>students</c> works
    /// here too. Resolution order:
    /// <list type="number">
    ///   <item>A unified <c>"YYYY-MM"</c> <paramref name="month"/> (reuses <see cref="TryParseYearMonth"/>)
    ///         is self-contained — it supplies BOTH year and month and takes precedence over a
    ///         separate <paramref name="year"/>; malformed → <c>PaymentInvalidMonthFormat</c>.</item>
    ///   <item>Otherwise the LEGACY form: a separate <paramref name="year"/> (validated when supplied
    ///         → <c>PaymentInvalidYear</c>) and an integer <paramref name="month"/> string 1-12
    ///         (validated when supplied → <c>PaymentInvalidMonthInteger</c>).</item>
    ///   <item>Anything omitted defaults to the teacher's current local (Africa/Cairo) month/year.</item>
    /// </list>
    /// Returns <c>null</c> on success (with <paramref name="resolvedYear"/>/<paramref name="resolvedMonth"/>
    /// set); otherwise the stable localization key to fail the request with (HTTP 422).
    /// </summary>
    private string? ResolveCollectionsMonthYear(
        long teacherId, string? month, int? year, out int resolvedYear, out int resolvedMonth)
    {
        var today = _timeZoneService.GetTeacherLocalDate(teacherId);
        resolvedYear = today.Year;
        resolvedMonth = today.Month;

        // (1) Unified "YYYY-MM" selector — self-contained, wins over a separate ?year=.
        if (!string.IsNullOrWhiteSpace(month) && month.Contains('-'))
        {
            if (!TryParseYearMonth(month, out resolvedYear, out resolvedMonth))
                return PaymentConstants.Messages.PaymentInvalidMonthFormat;
            return null;
        }

        // (2a) Legacy ?year=<int> — validate only when supplied.
        if (year.HasValue)
        {
            if (year.Value < 2000 || year.Value > 2100)
                return PaymentConstants.Messages.PaymentInvalidYear;
            resolvedYear = year.Value;
        }

        // (2b) Legacy ?month=<int 1-12> — validate only when supplied.
        if (!string.IsNullOrWhiteSpace(month))
        {
            if (!int.TryParse(month.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out resolvedMonth)
                || resolvedMonth < 1 || resolvedMonth > 12)
                return PaymentConstants.Messages.PaymentInvalidMonthInteger;
        }

        return null;
    }

    /// <summary>
    /// DASH-1: resolves ONLY the year for the yearly-collections view. A unified <c>"YYYY-MM"</c>
    /// <paramref name="month"/> contributes just its YEAR component — its month is NOT validated
    /// (a year-only screen must never 422 on a malformed month); otherwise the legacy
    /// <c>?year=&lt;int&gt;</c> is used (a legacy integer <c>?month=</c> is ignored); otherwise the
    /// teacher's current local (Africa/Cairo) year. Returns <c>null</c> on success (with
    /// <paramref name="resolvedYear"/> set), else the stable localization key to fail with (HTTP 422).
    /// </summary>
    private string? ResolveCollectionsYear(long teacherId, string? month, int? year, out int resolvedYear)
    {
        resolvedYear = _timeZoneService.GetTeacherLocalDate(teacherId).Year;

        // (1) Unified "YYYY-MM" — take only its YEAR; the month component is ignored (not validated).
        if (!string.IsNullOrWhiteSpace(month) && month.Contains('-'))
        {
            var yearPart = month.Trim().Split('-')[0];
            if (!int.TryParse(yearPart, NumberStyles.Integer, CultureInfo.InvariantCulture, out resolvedYear)
                || resolvedYear < 2000 || resolvedYear > 2100)
                return PaymentConstants.Messages.PaymentInvalidMonthFormat;
            return null;
        }

        // (2) Legacy ?year=<int> — validated only when supplied. A legacy integer ?month= is
        // irrelevant to a yearly view and is intentionally ignored.
        if (year.HasValue)
        {
            if (year.Value < 2000 || year.Value > 2100)
                return PaymentConstants.Messages.PaymentInvalidYear;
            resolvedYear = year.Value;
        }

        return null;
    }

    /// <summary>Parses a "YYYY-MM" month selector; false when malformed or out of range.</summary>
    private static bool TryParseYearMonth(string? value, out int year, out int month)
    {
        year = 0; month = 0;
        if (string.IsNullOrWhiteSpace(value)) return false;
        var parts = value.Trim().Split('-');
        if (parts.Length != 2) return false;
        if (!int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out year)) return false;
        if (!int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out month)) return false;
        return year >= 2000 && year <= 2100 && month >= 1 && month <= 12;
    }

    /// <summary>Clamps paging to sane bounds (page ≥ 1; 1 ≤ limit ≤ 100, default 20).</summary>
    private static (int page, int limit) NormalizePaging(int page, int limit)
    {
        if (page < 1) page = 1;
        if (limit < 1) limit = 20;
        else if (limit > 100) limit = 100;
        return (page, limit);
    }
}
