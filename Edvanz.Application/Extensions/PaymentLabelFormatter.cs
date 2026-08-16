using System.Globalization;
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
    /// occurrence date for a PerSession one. The Monthly month name is localized to the request's
    /// UI culture (<see cref="CultureInfo.CurrentUICulture"/>, set per request from the caller's
    /// language) so Arabic clients see Arabic month names; the PerSession date stays a
    /// culture-invariant ISO date to keep stable, Western-digit output.
    /// </summary>
    public static string FormatUnpaidPeriodLabel(UnpaidPeriodRef period) =>
        period.PeriodType == PeriodType.Monthly
            ? period.PeriodStart.ToString("MMMM yyyy", CultureInfo.CurrentUICulture)
            : period.PeriodStart.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
}