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

    /// <summary>Clamps paging to sane bounds (page ≥ 1; 1 ≤ limit ≤ 100, default 20).</summary>
    private static (int page, int limit) NormalizePaging(int page, int limit)
    {
        if (page < 1) page = 1;
        if (limit < 1) limit = 20;
        else if (limit > 100) limit = 100;
        return (page, limit);
    }
}
