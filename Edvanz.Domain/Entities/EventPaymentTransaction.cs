using Edvanz.Domain.Entities.ShareProp;
using Edvanz.Domain.Enums;
using System.ComponentModel.DataAnnotations.Schema;

namespace Edvanz.Domain.Entities;

/// <summary>
/// Records an individual event payment collection.
/// REQ-EVT-009: Collected using same methods as regular payments.
/// REQ-EVT-013: Associated with collecting user; included in wallet balance.
/// REQ-EVT-022: Retained even after event is deleted.
///
/// Denormalized for post-delete display. Follows PaymentTransaction patterns.
///
/// Multi-tenant isolation: TeacherId stored directly.
/// </summary>
public class EventPaymentTransaction : BaseEntity
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
    /// SET NULL when event is deleted — transaction survives.
    /// </summary>
    [ForeignKey(nameof(PaymentEvent))]
    public long? PaymentEventId { get; set; }
    public PaymentEvent? PaymentEvent { get; set; }

    /// <summary>
    /// Foreign key to the student's event obligation.
    /// SET NULL when obligation is cleaned up.
    /// </summary>
    [ForeignKey(nameof(EventStudentObligation))]
    public long? EventStudentObligationId { get; set; }
    public EventStudentObligation? EventStudentObligation { get; set; }

    /// <summary>
    /// Foreign key to the student.
    /// SET NULL on student permanent purge.
    /// </summary>
    [ForeignKey(nameof(TeacherStudent))]
    public long? TeacherStudentId { get; set; }
    public TeacherStudent? TeacherStudent { get; set; }

    // ══════════════════════════════════════════════
    // FINANCIAL DATA
    // ══════════════════════════════════════════════

    /// <summary>
    /// The amount collected in this transaction.
    /// REQ-EVT-011: May be less than amount due (partial payment).
    /// </summary>
    [Column(TypeName = "decimal(10,2)")]
    public decimal AmountPaid { get; set; }

    /// <summary>
    /// Which collection method was used.
    /// REQ-EVT-009: Same methods as regular Payment Module.
    /// </summary>
    public PaymentCollectionMethod PaymentMethod { get; set; }

    /// <summary>
    /// The user who collected this payment.
    /// REQ-EVT-013: Associated with collecting user.
    /// </summary>
    public long? CollectedByUserId { get; set; }

    // ══════════════════════════════════════════════
    // DENORMALIZED CONTEXT
    // ══════════════════════════════════════════════

    /// <summary>
    /// Denormalized: student name at collection time.
    /// </summary>
    public string? StudentName { get; set; }

    /// <summary>
    /// Denormalized: student code at collection time.
    /// </summary>
    public string? StudentCode { get; set; }

    /// <summary>
    /// Denormalized: event name at collection time.
    /// Survives event deletion.
    /// </summary>
    public string EventName { get; set; } = null!;

    // ══════════════════════════════════════════════
    // TIMESTAMPS
    // ══════════════════════════════════════════════

    /// <summary>
    /// Server UTC timestamp when the payment was recorded.
    /// REQ-EVT-NFR-002: Precision to the second.
    /// </summary>
    public DateTime CollectedAt { get; set; }

    // ══════════════════════════════════════════════
    // ONLINE PAYMENT FLAGS
    // ══════════════════════════════════════════════

    /// <summary>
    /// Whether this was an online payment.
    /// REQ-EVT-009: Online payment where applicable.
    /// </summary>
    public bool IsOnlinePayment { get; set; } = false;

    /// <summary>
    /// Online payment reference number.
    /// </summary>
    public string? OnlineTransactionRef { get; set; }
}