using System.Globalization;
using Edvanz.Application.Dtos;
using Edvanz.Application.Dtos.Center;
using Edvanz.Application.IservicesContract;
using Edvanz.Application.ServiceContract;
using Edvanz.Domain.Interfaces;
using Edvanz.Domain.Resources;
using Microsoft.Extensions.Localization;
using System.Net;

namespace Edvanz.Application.Services;

/// <summary>
/// Builds the center revenue-share report. Reuses the payment module's existing aggregates — real
/// collection (net of refunds) via <c>GetCashCollectedInRangeAsync</c> and period-based expected via
/// <c>GetDashboardAggregatesAsync</c> — and applies each teacher's effective share % (override ??
/// center default) to BOTH, per teacher and as center totals. Reporting only (no money movement).
/// </summary>
public class CenterRevenueService : ICenterRevenueService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IStringLocalizer<Messages> _localizer;
    private readonly ITimeZoneService _timeZone;

    public CenterRevenueService(IUnitOfWork unitOfWork, IStringLocalizer<Messages> localizer, ITimeZoneService timeZone)
    {
        _unitOfWork = unitOfWork;
        _localizer = localizer;
        _timeZone = timeZone;
    }

    /// <inheritdoc />
    public async Task<Result<CenterRevenueReportDto>> GetRevenueAsync(long centerId, string? month)
    {
        var center = await _unitOfWork.Centers.GetCenterByIdAsync(centerId);
        if (center == null)
            return Result<CenterRevenueReportDto>.Failure(_localizer, "CenterNotFound", HttpStatusCode.NotFound);

        // Resolve the month in the center's local (Africa/Cairo) time, then convert the month window to
        // UTC for the transaction/period range queries (matches the payment module's month scoping).
        var (year, mon) = ResolveMonth(month);
        var localMonthStart = new DateTime(year, mon, 1, 0, 0, 0, DateTimeKind.Unspecified);
        var startUtc = _timeZone.ConvertLocalToUtc(localMonthStart);
        var endUtc = _timeZone.ConvertLocalToUtc(localMonthStart.AddMonths(1));

        var teachers = await _unitOfWork.Centers.GetTeachersByCenterAsync(centerId);

        var report = new CenterRevenueReportDto
        {
            Month = $"{year:D4}-{mon:D2}",
            DefaultSharePercent = center.DefaultRevenueSharePercent
        };

        foreach (var t in teachers)
        {
            var collected = await _unitOfWork.PaymentsRepo.GetCashCollectedInRangeAsync(t.Id, null, startUtc, endUtc);
            var (expected, _, _) = await _unitOfWork.PaymentsRepo.GetDashboardAggregatesAsync(
                t.Id, null, null, null, startUtc, endUtc);

            var pct = t.RevenueSharePercentOverride ?? center.DefaultRevenueSharePercent;
            var cutOnCollected = Math.Round(collected * pct / 100m, 2);
            var cutOnExpected = Math.Round(expected * pct / 100m, 2);

            report.Teachers.Add(new CenterRevenueRowDto
            {
                TeacherId = t.Id,
                TeacherName = t.User?.FullName ?? string.Empty,
                TeacherCode = t.TeacherCode,
                PlanType = t.CenterPlanType,
                SharePercent = pct,
                Collected = collected,
                Expected = expected,
                CutOnCollected = cutOnCollected,
                CutOnExpected = cutOnExpected
            });

            report.TotalCollected += collected;
            report.TotalExpected += expected;
            report.TotalCutOnCollected += cutOnCollected;
            report.TotalCutOnExpected += cutOnExpected;
        }

        return Result<CenterRevenueReportDto>.Success(report, _localizer, "Success");
    }

    /// <summary>Parses "YYYY-MM"; falls back to the center's current local month on null/invalid.</summary>
    private (int year, int month) ResolveMonth(string? month)
    {
        if (!string.IsNullOrWhiteSpace(month) &&
            DateTime.TryParseExact(month.Trim(), "yyyy-MM", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var parsed))
        {
            return (parsed.Year, parsed.Month);
        }

        var localNow = _timeZone.ConvertUtcToLocal(DateTime.UtcNow);
        return (localNow.Year, localNow.Month);
    }
}
