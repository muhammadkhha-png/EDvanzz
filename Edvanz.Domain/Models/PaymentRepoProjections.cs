namespace Edvanz.Domain.Interfaces;

// ════════════════════════════════════════════════════════════════════════════
// PAYMENT MODULE (MODULE 4) — REPOSITORY PROJECTION TYPES
// ════════════════════════════════════════════════════════════════════════════
//
// Query projections returned by IPaymentRepo. Same convention as
// VideoRepoProjections.cs: projections live in the Domain layer alongside the
// repo interface so the Application service maps them to client-facing DTOs
// without the repo needing to know about the Application layer's DTO types.
// ════════════════════════════════════════════════════════════════════════════

/// <summary>
/// Projection row for the "Collected by Sessions" summary — one row per
/// currently active session (<c>EndDate &gt;= today</c>). Combines the
/// session's schedule fields with payment aggregates so the service layer can
/// build the display label and progress percentage without a second query.
/// </summary>
public sealed class ActiveSessionCollectionSummaryRow
{
    public long SessionId { get; set; }
    public string SessionName { get; set; } = null!;
    public Enums.OccurrenceType OccurrenceType { get; set; }
    public string? SelectedDays { get; set; }
    public byte? MonthlyDayOfMonth { get; set; }
    public TimeSpan StartTime { get; set; }
    public int TotalStudents { get; set; }
    public int PaidStudents { get; set; }
    public decimal ExpectedAmount { get; set; }
    public decimal CollectedAmount { get; set; }
}

/// <summary>
/// Projection row for the CollectPayment student list (api/v1 screens). One row per
/// student with the payment-status fields the screen needs, LEFT-joined to the
/// student's <c>StudentPaymentCounter</c> (a student with no counter → paid, 0 unpaid).
/// </summary>
public sealed class CollectStudentRow
{
    public long TeacherStudentId { get; set; }
    public string StudentName { get; set; } = null!;
    public string StudentCode { get; set; } = null!;
    public bool IsAssigned { get; set; }
    public decimal Amount { get; set; }
    public bool IsUnpaid { get; set; }
    public int UnpaidMonths { get; set; }
}

/// <summary>
/// Projection row for the PaymentTracking "students by status" list (api/v1 screens).
/// One row per student in the requested status group, with that month's paid/due amounts
/// and the student's outstanding balance/unpaid-month count from their counter.
/// </summary>
public sealed class StudentByStatusRow
{
    public long TeacherStudentId { get; set; }
    public string StudentName { get; set; } = null!;
    public decimal AmountPerMonth { get; set; }
    public decimal AmountPaid { get; set; }
    public decimal AmountDue { get; set; }
    public decimal UnpaidAmount { get; set; }
    public int UnpaidMonths { get; set; }

    /// <summary>Live student code from the TeacherStudent record.</summary>
    public string? StudentCode { get; set; }

    /// <summary>
    /// The session the student actually paid on this month (from the latest paying
    /// transaction). Falls back to the month's period session name when no payment
    /// exists, so unpaid/prorated rows still show a session.
    /// </summary>
    public string? SessionName { get; set; }

    /// <summary>
    /// Collection date of the student's latest paying transaction for the month
    /// (<c>PaymentTransaction.CollectedAt</c>). Null when nothing was paid.
    /// </summary>
    public DateTime? PaidOn { get; set; }
}

/// <summary>
/// Projection for the SessionPaymentCollectedByYear matrix (api/v1 screens). One row per
/// student with their per-month cells for the selected year (months with no period are
/// simply absent from <see cref="Months"/>).
/// </summary>
public sealed class YearlyStudentRow
{
    public long TeacherStudentId { get; set; }
    public string StudentName { get; set; } = null!;
    public List<YearlyMonthCell> Months { get; set; } = new();
}

/// <summary>One (student, month) cell of the yearly matrix, aggregated across the student's
/// periods in that calendar month.</summary>
public sealed class YearlyMonthCell
{
    public int Month { get; set; }        // 1-12
    public decimal AmountDue { get; set; }
    public decimal AmountPaid { get; set; }
    public bool IsPaid { get; set; }      // every period that month is fully Paid
    public bool IsProRated { get; set; }  // any period that month is prorated
}

/// <summary>
/// Projection for the CollectPaymentSession QR/code/name lookup (api/v1 screens): the resolved
/// student plus the amount they should pay and their current paid/unpaid state.
/// </summary>
public sealed class CollectLookupRow
{
    public long TeacherStudentId { get; set; }
    public string StudentName { get; set; } = null!;
    public string StudentCode { get; set; } = null!;
    public string? Group { get; set; }
    public decimal AmountDue { get; set; }
    public bool IsUnpaid { get; set; }
}
