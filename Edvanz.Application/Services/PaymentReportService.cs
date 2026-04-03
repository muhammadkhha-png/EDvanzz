using Edvanz.Application.Dtos;
using Edvanz.Application.Dtos.Payment;
using Edvanz.Application.ServiceContract;
using Edvanz.Domain.Constants;
using Edvanz.Domain.Enums;
using Edvanz.Domain.Interfaces;
using Microsoft.Extensions.Localization;
using System.Net;

namespace Edvanz.Application.Services;

/// <summary>
/// Implements payment report generation (Module 4 reporting).
/// REQ-PAY-048 through REQ-PAY-065: Ten report types.
/// REQ-PAY-051: All operations complete within 5 seconds for 50K students.
///
/// Uses IPaymentRepo named methods for all queries — no raw expressions.
/// Uses IPaymentReportExportService for PDF/Excel file generation.
/// </summary>
public class PaymentReportService : IPaymentReportService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPaymentReportExportService _exportService;
    private readonly IStringLocalizer<Domain.Resources.Messages> _localizer;

    public PaymentReportService(
        IUnitOfWork unitOfWork,
        IPaymentReportExportService exportService,
        IStringLocalizer<Domain.Resources.Messages> localizer)
    {
        _unitOfWork = unitOfWork;
        _exportService = exportService;
        _localizer = localizer;
    }

    /// <inheritdoc />
    public async Task<Result<object>> GenerateReportAsync(
        long teacherId, PaymentReportRequestDto request)
    {
        // Validate teacher
        var teacher = await _unitOfWork.Users.GetActiveTeacherByIdAsync(teacherId);
        if (teacher is null)
            return Result<object>.Failure(
                _localizer, PaymentConstants.Messages.TeacherNotFound, HttpStatusCode.NotFound);

        // Default date range to current month
        var startDate = request.StartDate ?? new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1);
        var endDate = request.EndDate ?? startDate.AddMonths(1).AddDays(-1);

        object reportData = request.ReportType switch
        {
            PaymentReportType.SingleStudentHistory => await GenerateSingleStudentReportAsync(
                teacherId, request.StudentId ?? 0, startDate, endDate),
            PaymentReportType.SessionPayment => await GenerateSessionReportAsync(
                teacherId, request.SessionId ?? 0, startDate, endDate),
            PaymentReportType.AllSessionsSummary => await GenerateAllSessionsSummaryAsync(
                teacherId, startDate, endDate),
            PaymentReportType.SessionGroupPayment => await GenerateSessionGroupReportAsync(
                teacherId, request.SessionGroupId ?? 0, startDate, endDate),
            PaymentReportType.UnpaidStudents => await GenerateUnpaidStudentsReportAsync(
                teacherId, request),
            PaymentReportType.ConsecutiveNonPayment => await GenerateConsecutiveNonPaymentReportAsync(
                teacherId, request),
            PaymentReportType.CollectorPerformance => await GenerateCollectorPerformanceReportAsync(
                teacherId, startDate, endDate),
            PaymentReportType.AssistantWalletHistory => await GenerateWalletHistoryReportAsync(
                teacherId, request.AssistantId ?? 0),
            PaymentReportType.DailyCollection => await GenerateDailyCollectionReportAsync(
                teacherId, startDate),
            PaymentReportType.PeriodRevenue => await GeneratePeriodRevenueReportAsync(
                teacherId, startDate, endDate),
            _ => throw new ArgumentOutOfRangeException(nameof(request.ReportType))
        };

        return Result<object>.Success(reportData, _localizer, PaymentConstants.Messages.ReportGenerated);
    }

    /// <inheritdoc />
    public async Task<Result<byte[]>> ExportReportAsync(
        long teacherId, PaymentReportRequestDto request, string format)
    {
        if (format != "pdf" && format != "xlsx")
            return Result<byte[]>.Failure(
                _localizer, PaymentConstants.Messages.InvalidExportFormat, HttpStatusCode.BadRequest);

        var reportResult = await GenerateReportAsync(teacherId, request);
        if (!reportResult.IsSuccess)
            return Result<byte[]>.Failure(reportResult.Message!, reportResult.StatusCode);

        var teacher = await _unitOfWork.Users.GetActiveTeacherByIdAsync(teacherId);
        var header = new PaymentReportHeaderDto
        {
            TutorAccountName = teacher?.User?.FullName ?? "Unknown",
            ReportType = request.ReportType.ToString(),
            ReportTitle = GetReportTitle(request.ReportType),
            GeneratedAt = DateTime.UtcNow,
            PeriodStart = request.StartDate,
            PeriodEnd = request.EndDate,
            ActiveFilters = BuildFilterDescription(request)
        };

        var fileBytes = await _exportService.ExportPaymentReportAsync(header, reportResult.Data!, format);
        return Result<byte[]>.Success(fileBytes, _localizer, PaymentConstants.Messages.ExportCompleted);
    }

    // ══════════════════════════════════════════════
    // PRIVATE REPORT GENERATORS
    // ══════════════════════════════════════════════

    private async Task<object> GenerateSingleStudentReportAsync(
        long teacherId, long studentId, DateTime startDate, DateTime endDate)
    {
        var (items, total) = await _unitOfWork.PaymentsRepo
            .GetStudentPaymentHistoryPagedAsync(teacherId, studentId, startDate, endDate, 1, 50000);

        var periods = await _unitOfWork.PaymentsRepo
            .GetAllPaymentPeriodsByStudentAsync(teacherId, studentId);

        var counter = await _unitOfWork.PaymentsRepo
            .GetPaymentCounterAsync(teacherId, studentId);

        return new
        {
            StudentId = studentId,
            Transactions = items,
            Periods = periods,
            TotalPaid = counter?.TotalAmountPaid ?? 0,
            TotalOutstanding = counter?.TotalOutstanding ?? 0
        };
    }

    private async Task<object> GenerateSessionReportAsync(
        long teacherId, long sessionId, DateTime startDate, DateTime endDate)
    {
        var (items, total) = await _unitOfWork.PaymentsRepo
            .GetSessionTransactionsPagedAsync(teacherId, sessionId, startDate, endDate, 1, 50000);

        var (expected, collected, remaining) = await _unitOfWork.PaymentsRepo
            .GetDashboardAggregatesAsync(teacherId, sessionId, null, null, startDate, endDate);

        return new
        {
            SessionId = sessionId,
            Transactions = items,
            Expected = expected,
            Collected = collected,
            Remaining = remaining,
            TotalStudents = total
        };
    }

    private async Task<object> GenerateAllSessionsSummaryAsync(
        long teacherId, DateTime startDate, DateTime endDate)
    {
        var perSession = await _unitOfWork.PaymentsRepo
            .GetDashboardPerSessionAsync(teacherId, null, null, startDate, endDate);

        var (expected, collected, remaining) = await _unitOfWork.PaymentsRepo
            .GetDashboardAggregatesAsync(teacherId, null, null, null, startDate, endDate);

        return new
        {
            PerSession = perSession,
            GrandExpected = expected,
            GrandCollected = collected,
            GrandRemaining = remaining
        };
    }

    private async Task<object> GenerateSessionGroupReportAsync(
        long teacherId, long groupId, DateTime startDate, DateTime endDate)
    {
        var perSession = await _unitOfWork.PaymentsRepo
            .GetDashboardPerSessionAsync(teacherId, groupId, null, startDate, endDate);

        return new { GroupId = groupId, PerSession = perSession };
    }

    private async Task<object> GenerateUnpaidStudentsReportAsync(
        long teacherId, PaymentReportRequestDto request)
    {
        var (items, total) = await _unitOfWork.PaymentsRepo.GetUnpaidStudentsPagedAsync(
            teacherId, request.SessionId, request.SessionGroupId,
            request.PaymentType, request.MinConsecutiveUnpaid,
            null, 1, 50000);

        return new { UnpaidStudents = items, Total = total };
    }

    private async Task<object> GenerateConsecutiveNonPaymentReportAsync(
        long teacherId, PaymentReportRequestDto request)
    {
        var (items, total) = await _unitOfWork.PaymentsRepo.GetUnpaidStudentsPagedAsync(
            teacherId, request.SessionId, request.SessionGroupId,
            request.PaymentType,
            request.MinConsecutiveUnpaid ?? PaymentConstants.DefaultConsecutiveUnpaidThreshold,
            null, 1, 50000);

        return new { ConsecutiveNonPayment = items, Total = total };
    }

    private async Task<object> GenerateCollectorPerformanceReportAsync(
        long teacherId, DateTime startDate, DateTime endDate)
    {
        var collectors = await _unitOfWork.PaymentsRepo
            .GetDashboardPerCollectorAsync(teacherId, startDate, endDate);

        return new { Collectors = collectors };
    }

    private async Task<object> GenerateWalletHistoryReportAsync(
        long teacherId, long assistantId)
    {
        var wallet = await _unitOfWork.PaymentsRepo
            .GetAssistantWalletAsync(teacherId, assistantId);

        var resetLogs = await _unitOfWork.PaymentsRepo
            .GetWalletResetLogsAsync(teacherId, assistantId);

        return new { Wallet = wallet, ResetLogs = resetLogs };
    }

    private async Task<object> GenerateDailyCollectionReportAsync(
        long teacherId, DateTime date)
    {
        var (items, total) = await _unitOfWork.PaymentsRepo
            .GetTransactionsByDateRangePagedAsync(teacherId, date.Date, date.Date.AddDays(1),
                null, null, 1, 50000);

        return new { Date = date.Date, Transactions = items, Total = total };
    }

    private async Task<object> GeneratePeriodRevenueReportAsync(
        long teacherId, DateTime startDate, DateTime endDate)
    {
        var (expected, collected, remaining) = await _unitOfWork.PaymentsRepo
            .GetDashboardAggregatesAsync(teacherId, null, null, null, startDate, endDate);

        var perSession = await _unitOfWork.PaymentsRepo
            .GetDashboardPerSessionAsync(teacherId, null, null, startDate, endDate);

        var perCollector = await _unitOfWork.PaymentsRepo
            .GetDashboardPerCollectorAsync(teacherId, startDate, endDate);

        return new
        {
            PeriodStart = startDate,
            PeriodEnd = endDate,
            Expected = expected,
            Collected = collected,
            Remaining = remaining,
            CollectionRate = expected > 0 ? Math.Round(collected / expected * 100, 1) : 0,
            PerSession = perSession,
            PerCollector = perCollector
        };
    }

    private static string GetReportTitle(PaymentReportType type) => type switch
    {
        PaymentReportType.SingleStudentHistory => "Single Student Payment History Report",
        PaymentReportType.SessionPayment => "Session Payment Report",
        PaymentReportType.AllSessionsSummary => "All Sessions Payment Summary Report",
        PaymentReportType.SessionGroupPayment => "Session Group Payment Report",
        PaymentReportType.UnpaidStudents => "Unpaid Students Report",
        PaymentReportType.ConsecutiveNonPayment => "Consecutive Non-Payment Report",
        PaymentReportType.CollectorPerformance => "Collector Performance Report",
        PaymentReportType.AssistantWalletHistory => "Assistant Wallet History Report",
        PaymentReportType.DailyCollection => "Daily Collection Report",
        PaymentReportType.PeriodRevenue => "Period Revenue Report",
        _ => "Payment Report"
    };

    private static string BuildFilterDescription(PaymentReportRequestDto request)
    {
        var filters = new List<string>();
        if (request.SessionId.HasValue) filters.Add($"Session: {request.SessionId}");
        if (request.SessionGroupId.HasValue) filters.Add($"Group: {request.SessionGroupId}");
        if (request.PaymentType.HasValue) filters.Add($"Type: {request.PaymentType}");
        if (request.MinConsecutiveUnpaid.HasValue) filters.Add($"Min Consecutive: {request.MinConsecutiveUnpaid}");
        return filters.Count > 0 ? string.Join(", ", filters) : "None";
    }
}