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
    public async Task<Result<CollectionsByMonthResponse>> GetCollectionsByMonthAsync(
        long teacherId, int month, int year, int page, int limit)
    {
        if (month < 1 || month > 12)
            return Result<CollectionsByMonthResponse>.Failure(
                _localizer, PaymentConstants.Messages.PaymentInvalidMonthInteger,
                HttpStatusCode.UnprocessableEntity);
        if (year < 2000 || year > 2100)
            return Result<CollectionsByMonthResponse>.Failure(
                _localizer, PaymentConstants.Messages.PaymentInvalidYear, HttpStatusCode.UnprocessableEntity);

        (page, limit) = NormalizePaging(page, limit);

        var startDate = new DateTime(year, month, 1);
        var endDate = startDate.AddMonths(1).AddDays(-1);

        var (items, totalCount) = await _unitOfWork.PaymentsRepo
            .GetTransactionsByDateRangePagedAsync(
                teacherId, startDate, endDate,
                sessionId: null, collectedByUserId: null,
                page: page, pageSize: limit);

        int baseIndex = (page - 1) * limit;
        var rows = new List<CollectionRow>(items.Count);
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
                SessionName = string.IsNullOrEmpty(tx.SessionName) ? null : tx.SessionName,
                CollectedAt = tx.CollectedAt
            });
        }

        var response = new CollectionsByMonthResponse
        {
            Month = month,
            Year = year,
            MonthLabel = startDate.ToString("MMMM yyyy", CultureInfo.InvariantCulture),
            Page = page,
            Limit = limit,
            TotalItems = totalCount,
            TotalPages = totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)limit),
            Items = rows
        };

        return Result<CollectionsByMonthResponse>.Success(
            response, _localizer, PaymentConstants.Messages.Success);
    }

    /// <inheritdoc />
    public async Task<Result<AssistantWalletScreenResponse>> GetAssistantWalletScreenAsync(
        long teacherId, long assistantId, int page, int limit)
    {
        // Tenant-scoped lookup: a wallet belonging to another teacher's assistant returns null → 404.
        var wallet = await _unitOfWork.PaymentsRepo.GetAssistantWalletAsync(teacherId, assistantId);
        if (wallet is null)
            return Result<AssistantWalletScreenResponse>.Failure(
                _localizer, PaymentConstants.Messages.WalletNotFound, HttpStatusCode.NotFound);

        (page, limit) = NormalizePaging(page, limit);

        // The assistant log is scoped to the current local month: the collections they took this
        // month. "Total cash collected" (below) is the same window, net of refunds.
        var localToday = _timeZoneService.GetTeacherLocalDate(teacherId);
        var walletMonthStart = new DateTime(localToday.Year, localToday.Month, 1);
        var monthEndExclusive = walletMonthStart.AddMonths(1);

        // The log mixes collections and refunds for the month in one chronological list. Collections
        // are positive; a refund taken back from this collector is a NEGATIVE-amount entry dated
        // when it was refunded. Merged and paged in-memory (a single collector's month is bounded).
        var monthTxns = await _unitOfWork.PaymentsRepo
            .GetCollectorTransactionsInRangeAsync(teacherId, wallet.AssistantUserId, walletMonthStart, monthEndExclusive);
        var monthRefunds = await _unitOfWork.PaymentsRepo
            .GetCollectorRefundsInRangeAsync(teacherId, wallet.AssistantUserId, walletMonthStart, monthEndExclusive);

        // "Total cash collected" = money in (collections this month) minus money out (refunds this
        // month). A same-month collect-then-refund nets to zero; a refund of an earlier month's
        // collection shows this month as negative net.
        decimal monthCashCollected =
            monthTxns.Sum(t => t.AmountPaid) - monthRefunds.Sum(r => r.RefundAmount);

        var merged = new List<AssistantWalletCollectionItemDto>(monthTxns.Count + monthRefunds.Count);
        merged.AddRange(monthTxns.Select(tx => new AssistantWalletCollectionItemDto
        {
            Id = tx.Id.ToString(CultureInfo.InvariantCulture),
            StudentId = tx.TeacherStudentId?.ToString(CultureInfo.InvariantCulture),
            StudentName = tx.StudentName,
            StudentCode = tx.StudentCode,
            SessionName = string.IsNullOrEmpty(tx.SessionName) ? null : tx.SessionName,
            Amount = tx.AmountPaid,
            CollectedAt = tx.CollectedAt
        }));
        merged.AddRange(monthRefunds.Select(r => new AssistantWalletCollectionItemDto
        {
            Id = $"refund-{r.Id.ToString(CultureInfo.InvariantCulture)}",
            StudentId = r.StudentId?.ToString(CultureInfo.InvariantCulture),
            StudentName = r.StudentName,
            StudentCode = r.StudentCode,
            SessionName = string.IsNullOrEmpty(r.SessionName) ? null : r.SessionName,
            Amount = -r.RefundAmount, // negative → refund
            CollectedAt = r.RefundedAt
        }));

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
                TotalCashCollected = monthCashCollected,
                WalletBalance = wallet.CurrentBalance,
                CollectionsCount = wallet.TransactionCount,
                LastActivityAt = wallet.LastCollectionAt
            },
            Collections = new AssistantWalletCollectionsDto
            {
                Total = total,
                Page = page,
                Limit = limit,
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
                AvatarUrl = null,
                Amount = r.Amount,
                Assignment = r.IsAssigned ? "assigned" : "unassigned",
                Status = r.IsUnpaid ? "unpaid" : "paid",
                UnpaidMonths = r.UnpaidMonths
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
        long teacherId, string? month, string? status, int page, int limit)
    {
        // PAY-2: default to the teacher's current local month when omitted (only 422 on malformed).
        if (!TryResolveMonth(teacherId, month, out int year, out int mon))
            return Result<StudentsByStatusResponse>.Failure(
                _localizer, PaymentConstants.Messages.PaymentInvalidMonthFormat,
                HttpStatusCode.UnprocessableEntity);

        status = string.IsNullOrWhiteSpace(status) ? null : status.Trim().ToLowerInvariant();
        if (status != "paid" && status != "partial" && status != "prorated" && status != "unpaid")
            return Result<StudentsByStatusResponse>.Failure(
                _localizer, PaymentConstants.Messages.PaymentInvalidStatusFilter,
                HttpStatusCode.UnprocessableEntity);
        (page, limit) = NormalizePaging(page, limit);
        var monthStart = new DateTime(year, mon, 1);
        var monthEnd = monthStart.AddMonths(1).AddDays(-1);

        var (rows, total, groupCollected, groupExpected, groupUnpaid) = await _unitOfWork.PaymentsRepo
            .GetStudentsByPaymentStatusPagedAsync(teacherId, status, monthStart, monthEnd, page, limit);

        var students = new List<StudentByStatusDto>(rows.Count);
        foreach (var r in rows)
        {
            students.Add(new StudentByStatusDto
            {
                Id = r.TeacherStudentId.ToString(CultureInfo.InvariantCulture),
                Name = r.StudentName,
                AvatarUrl = null,
                Status = status,
                AmountPerMonth = r.AmountPerMonth,
                AmountPaid = r.AmountPaid,
                AmountDue = r.AmountDue,
                UnpaidAmount = r.UnpaidAmount,
                UnpaidMonths = r.UnpaidMonths,
                StudentCode = r.StudentCode,
                SessionName = r.SessionName,
                PaidOn = r.PaidOn
            });
        }

        var response = new StudentsByStatusResponse
        {
            Month = $"{year:D4}-{mon:D2}",
            MonthLabel = monthStart.ToString("MMMM yyyy", CultureInfo.InvariantCulture),
            Status = status,
            TotalCollected = groupCollected,
            MonthAmount = groupExpected,
            // Ambiguous top-level "default fee": no single value exists (per-session/per-student).
            // The per-student amountPerMonth is authoritative; confirm intended meaning with FE.
            AmountPerMonth = 0m,
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
        long teacherId, int year, int page, int limit)
    {
        if (year < 2000 || year > 2100)
            return Result<YearlyCollectionsResponse>.Failure(
                _localizer, PaymentConstants.Messages.PaymentInvalidYear, HttpStatusCode.UnprocessableEntity);

        (page, limit) = NormalizePaging(page, limit);
        var yearStart = new DateTime(year, 1, 1);
        var yearEnd = new DateTime(year, 12, 31);

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
            Year = year,
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
        decimal collected = await repo.GetCashCollectedInRangeAsync(
            teacherId, null, monthStart, monthStart.AddMonths(1));
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

        var assistants = collectors.Select(c => new TrackingAssistantDto
        {
            Id = c.UserId.ToString(CultureInfo.InvariantCulture),
            Name = names.TryGetValue(c.UserId, out var nm) ? nm : c.UserName,
            AvatarUrl = null,
            Role = assistantUserIds.Contains(c.UserId) ? "Assistant" : "Teacher",
            TransactionCount = c.TransactionCount,
            CollectedAmount = c.Collected
        }).ToList();

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
                ProgressPercent = pct
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
