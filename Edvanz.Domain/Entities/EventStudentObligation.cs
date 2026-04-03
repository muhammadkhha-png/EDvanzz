using Edvanz.Domain.Entities.ShareProp;
using Edvanz.Domain.Enums;
using System.ComponentModel.DataAnnotations.Schema;

namespace Edvanz.Domain.Entities;

/// <summary>
/// Junction table: which students owe for which event.
/// Created at event creation time for all students in the target scope.
/// BR-EVT-001: Only students in scope at creation time receive the obligation.
/// BR-EVT-004: Each event obligation is tracked independently.
///
/// REQ-EVT-010: Tracks per-student payment status (Unpaid, PartiallyPaid, Paid).
/// REQ-EVT-015: Enables the paid/unpaid student lists in Event Payment Tracking.
///
/// Multi-tenant isolation: TeacherId stored directly.
/// </summary>
public class EventStudentObligation : BaseEntity
{
    /// <summary>
    /// Foreign key to the owning Teacher.
    /// REQ-EVT-NFR-001: All event data scoped to the tutor's account.
    /// </summary>
    [ForeignKey(nameof(Teacher))]
    public long TeacherId { get; set; }
    public Teacher Teacher { get; set; } = null!;

    /// <summary>
    /// Foreign key to the payment event.
    /// CASCADE: Obligation removed when event is deleted.
    /// </summary>
    [ForeignKey(nameof(PaymentEvent))]
    public long PaymentEventId { get; set; }
    public PaymentEvent PaymentEvent { get; set; } = null!;

    /// <summary>
    /// Foreign key to the student who owes for this event.
    /// SET NULL on student permanent purge. Denormalized fields survive.
    /// </summary>
    [ForeignKey(nameof(TeacherStudent))]
    public long? TeacherStudentId { get; set; }
    public TeacherStudent? TeacherStudent { get; set; }

    // ══════════════════════════════════════════════
    // DENORMALIZED STUDENT CONTEXT
    // ══════════════════════════════════════════════

    /// <summary>
    /// Denormalized: student name at obligation creation time.
    /// </summary>
    public string? StudentName { get; set; }

    /// <summary>
    /// Denormalized: student code at obligation creation time.
    /// </summary>
    public string? StudentCode { get; set; }

    // ══════════════════════════════════════════════
    // FINANCIAL DATA
    // ══════════════════════════════════════════════

    /// <summary>
    /// The amount this student owes for the event.
    /// REQ-EVT-002: Typically the event amount, but may differ per student.
    /// </summary>
    [Column(TypeName = "decimal(10,2)")]
    public decimal AmountDue { get; set; }

    /// <summary>
    /// Total amount paid by this student for this event.
    /// Updated on each payment collection.
    /// </summary>
    [Column(TypeName = "decimal(10,2)")]
    public decimal AmountPaid { get; set; } = 0;

    /// <summary>
    /// Current payment status for this obligation.
    /// REQ-EVT-010: Unpaid, PartiallyPaid, or Paid.
    /// </summary>
    public PaymentStatus PaymentStatus { get; set; } = PaymentStatus.Unpaid;

    // Navigation property
    public ICollection<EventPaymentTransaction> EventPaymentTransactions { get; set; } = new List<EventPaymentTransaction>();
}