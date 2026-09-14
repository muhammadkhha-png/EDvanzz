using Edvanz.Domain.Entities.ShareProp;
using Edvanz.Domain.Enums;
using System.ComponentModel.DataAnnotations;
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

    // ══════════════════════════════════════════════
    // PARITY WITH PaymentTransaction (added 2026-09-14)
    // ══════════════════════════════════════════════

    /// <summary>
    /// The collector's free-text note for this payment — how a partial or unusual amount is
    /// explained. Mirrors <c>PaymentTransaction.CollectionNote</c>.
    /// </summary>
    public string? CollectionNote { get; set; }

    /// <summary>
    /// True when the payment was taken while the device was offline and replayed later.
    /// Mirrors <c>PaymentTransaction.IsOfflineRecord</c>.
    /// </summary>
    public bool IsOfflineRecord { get; set; } = false;

    /// <summary>The device that recorded an offline payment.</summary>
    public string? OfflineDeviceId { get; set; }

    /// <summary>
    /// Client-generated idempotency key, under a filtered unique index per teacher — the PERMANENT
    /// exactly-once guarantee for an offline replay. (The Redis <c>Idempotency-Key</c> layer expires
    /// after 24h; this index never does.)
    ///
    /// <para>For the combined attendance sheet this is NOT a fresh uuid: it is derived from the
    /// single outbox op id as <c>"{opId}:x{obligationId}"</c>, with the fee leg taking
    /// <c>"{opId}:f"</c>. That is what makes a blind resend of a PARTIALLY committed sheet
    /// re-acknowledge the legs that landed and record only the missing ones — and why the transport
    /// needs no reconcile step for that op type. Never generate per-leg ids client-side.</para>
    /// </summary>
    public string? ClientEntryId { get; set; }

    /// <summary>Offline sync state. Mirrors <c>PaymentTransaction.SyncStatus</c>.</summary>
    public PaymentSyncStatus SyncStatus { get; set; } = PaymentSyncStatus.NotApplicable;

    // ══════════════════════════════════════════════
    // SOFT DELETE — refunds (added 2026-09-14)
    // ══════════════════════════════════════════════

    /// <summary>
    /// Soft-delete flag for a refunded/corrected payment. Mirrors
    /// <c>PaymentTransaction.IsDeleted</c>, and carries a global query filter for the same reason:
    /// so a forgotten predicate can never leak refunded money into a total. The negative ledger row
    /// comes from <see cref="EventPaymentEditLog"/>, not from this row.
    /// </summary>
    public bool IsDeleted { get; set; } = false;

    /// <summary>UTC instant of the refund.</summary>
    public DateTime? DeletedAt { get; set; }

    /// <summary>
    /// Optimistic-concurrency token, so a refund and an amount-edit racing on the same collected
    /// payment cannot silently overwrite each other. Computed column — adding it repairs no data.
    /// </summary>
    [Timestamp]
    public byte[] RowVersion { get; set; } = null!;
}
