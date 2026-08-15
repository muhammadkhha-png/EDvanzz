using Edvanz.Domain.Enums;
using Edvanz.Domain.Interfaces;

namespace Edvanz.Application.Extensions;

/// <summary>
/// Shared display-label formatting for unpaid billing periods. Extracted from
/// <c>PaymentService</c> (originally a private static method there) so the Attendance module's
/// payment-info enrichment (<c>ShowPaymentInfoOnAttendanceScreen</c>) can reuse the exact same
/// label rules as the Unpaid Students Overview instead of duplicating them.
/// </summary>
public static class PaymentLabelFormatter
{
    /// <summary>
    /// Display label for one unpaid period: the calendar month for a Monthly obligation, the
    /// occurrence date for a PerSession one.
    /// </summary>
    public static string FormatUnpaidPeriodLabel(UnpaidPeriodRef period) =>
        period.PeriodType == PeriodType.Monthly
            ? period.PeriodStart.ToString("MMMM yyyy")
            : period.PeriodStart.ToString("yyyy-MM-dd");
}