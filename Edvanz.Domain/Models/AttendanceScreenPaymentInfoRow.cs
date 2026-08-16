namespace Edvanz.Domain.Interfaces;

// â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
// ATTENDANCE MODULE (MODULE 3) Ã— PAYMENT MODULE (MODULE 4) â€” ATTENDANCE-SCREEN
// PAYMENT ENRICHMENT PROJECTION
// â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
//
// Same convention as PaymentRepoProjections.cs / UnpaidStudentRow.cs: projections
// live in the Domain layer alongside the repo interface so the Application
// service maps them to client-facing DTOs without the repo knowing about
// Application DTO types.
//
// Kept in a dedicated file rather than appended to PaymentRepoProjections.cs so
// the change is additive and cannot conflict with edits to that file.
// â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

/// <summary>
/// Per-student payment/debt snapshot (query projection) for the Attendance student-list
/// screen's optional payment enrichment (<c>ShowPaymentInfoOnAttendanceScreen</c>). Reuses
/// <see cref="UnpaidPeriodRef"/> and the same CLAUDE.md Â§7.4 arrears rules as
/// <see cref="UnpaidStudentRow"/> â€” see
/// <see cref="IPaymentRepo.GetPaymentInfoForAttendanceBatchAsync"/> for scope details.
/// </summary>
public sealed class AttendanceScreenPaymentInfoRow
{
    public long TeacherStudentId { get; set; }

    /// <summary>
    /// True when a Monthly <c>PaymentPeriod</c> starting in the calendar month immediately
    /// before the teacher's current local month exists and is not fully paid.
    /// </summary>
    public bool HasUnpaidLastMonth { get; set; }

    /// <summary>
    /// True when a Monthly <c>PaymentPeriod</c> starting in the teacher's current local month
    /// exists and is not fully paid.
    /// </summary>
    public bool HasUnpaidCurrentMonth { get; set; }

    /// <summary>
    /// Count of unpaid periods through the current month cutoff. Contiguous tail (BR-PAY-006),
    /// same semantics as <see cref="UnpaidStudentRow.UnpaidPeriodCount"/>.
    /// </summary>
    public int UnpaidMonthsCount { get; set; }

    /// <summary>Sum of <c>(AmountDue - AmountPaid)</c> over those periods.</summary>
    public decimal UnpaidAmount { get; set; }

    /// <summary>
    /// The individual unpaid periods behind <see cref="UnpaidMonthsCount"/>, earliest first.
    /// Dates only â€” display formatting/localization belongs to the Application layer
    /// (<c>PaymentLabelFormatter.FormatUnpaidPeriodLabel</c>).
    /// </summary>
    public IReadOnlyList<UnpaidPeriodRef> UnpaidPeriods { get; set; } = Array.Empty<UnpaidPeriodRef>();
}