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
