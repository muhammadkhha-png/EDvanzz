using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Threading.Tasks;
using Edvanz.Application.Dtos;
using Edvanz.Application.Dtos.Payment;
using Edvanz.Application.ServiceContract;
using Edvanz.Domain.Constants;
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
    private readonly IUnitOfWork _unitOfWork;
    private readonly IStringLocalizer<Domain.Resources.Messages> _localizer;

    public PaymentScreenService(
        IUnitOfWork unitOfWork,
        IStringLocalizer<Domain.Resources.Messages> localizer)
    {
        _unitOfWork = unitOfWork;
        _localizer = localizer;
    }

    /// <inheritdoc />
    public async Task<Result<CollectionsByMonthResponse>> GetCollectionsByMonthAsync(
        long teacherId, int month, int year, int page, int limit)
    {
        // NOTE(localization): validation messages are plain strings for now; they are
        // converted to Messages_en/ar.resx keys in the Phase-1 localization pass.
        if (month < 1 || month > 12)
            return Result<CollectionsByMonthResponse>.Failure(
                "Invalid month; expected an integer 1-12.", HttpStatusCode.UnprocessableEntity);
        if (year < 2000 || year > 2100)
            return Result<CollectionsByMonthResponse>.Failure(
                "Invalid year.", HttpStatusCode.UnprocessableEntity);

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
                "Assistant wallet not found.", HttpStatusCode.NotFound);

        (page, limit) = NormalizePaging(page, limit);

        var (txns, total) = await _unitOfWork.PaymentsRepo
            .GetCollectorTransactionsPagedAsync(
                teacherId, wallet.AssistantUserId,
                startDate: null, endDate: null,
                page: page, pageSize: limit);

        var items = new List<AssistantWalletCollectionItemDto>(txns.Count);
        foreach (var tx in txns)
        {
            items.Add(new AssistantWalletCollectionItemDto
            {
                Id = tx.Id.ToString(CultureInfo.InvariantCulture),
                StudentId = tx.TeacherStudentId?.ToString(CultureInfo.InvariantCulture),
                StudentName = tx.StudentName,
                StudentCode = tx.StudentCode,
                SessionName = string.IsNullOrEmpty(tx.SessionName) ? null : tx.SessionName,
                Amount = tx.AmountPaid,
                CollectedAt = tx.CollectedAt
            });
        }

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
                TotalCashCollected = wallet.TotalCollected,
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
                "Invalid filter; expected all | assigned | unassigned.", HttpStatusCode.UnprocessableEntity);

        (page, limit) = NormalizePaging(page, limit);

        var (rows, total, cAll, cAssigned, cUnassigned) = await _unitOfWork.PaymentsRepo
            .GetCollectStudentsPagedAsync(teacherId, filter, search, page, limit);

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
        if (!TryParseYearMonth(month, out int year, out int mon))
            return Result<StudentsByStatusResponse>.Failure(
                "Invalid month; expected YYYY-MM.", HttpStatusCode.UnprocessableEntity);

        status = string.IsNullOrWhiteSpace(status) ? null : status.Trim().ToLowerInvariant();
        if (status != "paid" && status != "prorated" && status != "unpaid")
            return Result<StudentsByStatusResponse>.Failure(
                "Invalid status; expected paid | prorated | unpaid.", HttpStatusCode.UnprocessableEntity);

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
                UnpaidMonths = r.UnpaidMonths
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
