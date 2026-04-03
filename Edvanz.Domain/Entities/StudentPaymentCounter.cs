using Edvanz.Domain.Entities.ShareProp;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Edvanz.Domain.Entities;

/// <summary>
/// Materialized aggregate payment counters per student per teacher.
/// Mirrors the StudentAbsenceCounter pattern for O(1) lookups during payment collection.
///
/// REQ-PAY-029: Consecutive non-payment counter (resets on payment).
/// REQ-PAY-016: Custom payment amount per student (BR-PAY-003).
/// REQ-PAY-031: Total outstanding amount for unpaid overview.
/// REQ-PAY-028: Unpaid student count badge per session.
///
/// PERFORMANCE RATIONALE:
/// At 50K concurrent users, REQ-PAY-045 requires sub-1-second payment collection.
/// Scanning PaymentPeriods to count consecutive unpaid periods per barcode scan would be O(N).
/// A single-row counter table makes it O(1).
///
/// COUNTER UPDATE RULES:
/// - Payment collected: ConsecutiveUnpaid = recalc, TotalAmountPaid += amount, TotalOutstanding -= amount
/// - Payment deleted/reversed: TotalAmountPaid -= amount, TotalOutstanding += amount, recalc consecutive
/// - Period generated: TotalOutstanding += amountDue
/// </summary>
public class StudentPaymentCounter : BaseEntity
{
    /// <summary>
    /// Foreign key to the owning Teacher. Stored directly for tenant-scoped index performance.
    /// REQ-PAY-NFR-001: All payment data scoped to individual tutor account.
    /// </summary>
    [ForeignKey(nameof(Teacher))]
    public long TeacherId { get; set; }
    public Teacher Teacher { get; set; } = null!;

    /// <summary>
    /// Foreign key to the student record.
    /// NO ACTION on delete: cleaned up via application logic on permanent purge.
    /// One counter per student per teacher (unique constraint enforced in DbContext).
    /// </summary>
    [ForeignKey(nameof(TeacherStudent))]
    public long TeacherStudentId { get; set; }
    public TeacherStudent TeacherStudent { get; set; } = null!;

    /// <summary>
    /// Rolling counter: number of consecutive unpaid periods.
    /// REQ-PAY-029: Flags students with 2+ consecutive unpaid.
    /// BR-PAY-006: Resets on any payment. Preserves full historical record.
    /// </summary>
    public int ConsecutiveUnpaid { get; set; } = 0;

    /// <summary>
    /// Total number of periods where the student has not paid.
    /// REQ-PAY-031: Used for unpaid overview.
    /// </summary>
    public int TotalUnpaidPeriods { get; set; } = 0;

    /// <summary>
    /// Total number of periods where the student has fully paid.
    /// Used for payment rate calculations in reports.
    /// </summary>
    public int TotalPaidPeriods { get; set; } = 0;

    /// <summary>
    /// Cumulative total amount paid by this student across all sessions.
    /// REQ-PAY-052: Total cumulative amount in student history report.
    /// </summary>
    [Column(TypeName = "decimal(12,2)")]
    public decimal TotalAmountPaid { get; set; } = 0;

    /// <summary>
    /// Current total outstanding amount across all unpaid periods.
    /// REQ-PAY-NFR-008: Live total outstanding for unpaid students view.
    /// </summary>
    [Column(TypeName = "decimal(12,2)")]
    public decimal TotalOutstanding { get; set; } = 0;

    /// <summary>
    /// Custom payment amount overriding the session default for this student.
    /// REQ-PAY-016: Stored per student, used for all future collections.
    /// BR-PAY-003: Not affected by session amount changes.
    /// Null means "use session default".
    /// </summary>
    [Column(TypeName = "decimal(10,2)")]
    public decimal? CustomPaymentAmount { get; set; }

    /// <summary>
    /// Date of the student's last payment.
    /// REQ-PAY-052: Displayed in student payment history.
    /// </summary>
    public DateTime? LastPaymentDate { get; set; }

    /// <summary>
    /// Session name where the last payment was collected.
    /// Used for display context in payment views.
    /// </summary>
    public string? LastPaymentSessionName { get; set; }

    /// <summary>
    /// Optimistic concurrency token. Prevents lost updates when multiple
    /// concurrent requests modify the same counter (e.g., two assistants
    /// collecting payments simultaneously at different barcode stations).
    /// </summary>
    [Timestamp]
    public byte[] RowVersion { get; set; } = null!;
}