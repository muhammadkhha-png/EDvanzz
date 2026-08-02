using Edvanz.Domain.Enums;

namespace Edvanz.Domain.Interfaces;

// ════════════════════════════════════════════════════════════════════════════
// PAYMENT MODULE (MODULE 4) — UNPAID STUDENTS OVERVIEW PROJECTIONS
// ════════════════════════════════════════════════════════════════════════════
//
// Same convention as PaymentRepoProjections.cs: projections live in the Domain
// layer alongside the repo interface so the Application service maps them to
// client-facing DTOs without the repo knowing about Application DTO types.
//
// Kept in a dedicated file rather than appended to PaymentRepoProjections.cs so
// the change is additive and cannot conflict with edits to that file.
// ════════════════════════════════════════════════════════════════════════════

/// <summary>
/// Projection row for the Unpaid Students Overview (REQ-PAY-031/032/033), derived from
/// <c>PaymentPeriods</c> rather than the all-time <c>StudentPaymentCounter</c>.
///
/// Every figure here is judged THROUGH the caller's cutoff month
/// (<c>PeriodStart &lt;= throughMonthEnd</c>), per CLAUDE.md §7.4: periods are pre-generated
/// to the session end, so counter-derived arrears count months that are not yet owed.
/// </summary>
public sealed class UnpaidStudentRow
{
    public long TeacherStudentId { get; set; }

    /// <summary>Live student name from the TeacherStudent record.</summary>
    public string StudentName { get; set; } = null!;

    /// <summary>Live student code from the TeacherStudent record.</summary>
    public string StudentCode { get; set; } = null!;

    /// <summary>The student's CURRENT session assignment (null when unassigned) — display only.
    /// The rows themselves are scoped by the PERIOD's session, not by this field.</summary>
    public long? SessionId { get; set; }

    /// <summary>Name of the student's current session assignment. Null when unassigned.</summary>
    public string? SessionName { get; set; }

    /// <summary>
    /// Number of unpaid periods through the cutoff month. Because the collection engine settles
    /// oldest-first, these are always a contiguous tail — so this is simultaneously the total
    /// unpaid count and the CONSECUTIVE unpaid count (BR-PAY-006 / REQ-PAY-029).
    /// </summary>
    public int UnpaidPeriodCount { get; set; }

    /// <summary>Sum of <c>(AmountDue - AmountPaid)</c> over those periods.</summary>
    public decimal TotalOutstanding { get; set; }

    /// <summary>
    /// Date of the student's last payment, from <c>StudentPaymentCounter</c>. A historical fact,
    /// so it is not affected by the pre-generated-future-period problem.
    /// </summary>
    public DateTime? LastPaymentDate { get; set; }

    /// <summary>
    /// The individual unpaid periods behind <see cref="UnpaidPeriodCount"/>, earliest first.
    /// Dates only — display formatting/localization belongs to the Application layer.
    /// </summary>
    public IReadOnlyList<UnpaidPeriodRef> UnpaidPeriods { get; set; } = Array.Empty<UnpaidPeriodRef>();
}

/// <summary>
/// One unpaid billing period behind an <see cref="UnpaidStudentRow"/>. Monthly periods span a
/// calendar month; PerSession periods collapse to a single occurrence date
/// (<c>PeriodStart == PeriodEnd</c>).
/// </summary>
public sealed class UnpaidPeriodRef
{
    public PeriodType PeriodType { get; set; }
    public DateTime PeriodStart { get; set; }
    public DateTime PeriodEnd { get; set; }

    /// <summary>Still owed on this period: <c>AmountDue - AmountPaid</c>. Partial payments show a
    /// remainder smaller than the full due amount.</summary>
    public decimal AmountRemaining { get; set; }
}
