using Edvanz.Domain.Entities.ShareProp;
using Edvanz.Domain.Enums;
using System.ComponentModel.DataAnnotations;
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
    /// NoAction: Obligation removed when event is deleted.
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

    // ══════════════════════════════════════════════
    // EXEMPTION — "not taking it" (added 2026-09-14)
    // ══════════════════════════════════════════════

    /// <summary>
    /// The student is not buying this item (a gift, they already have one, or the tutor excused
    /// them). Excluded from expected revenue and from every "owes money" surface, but RECORDED
    /// rather than deleted, so the decision and its author survive.
    ///
    /// <para><b>ORTHOGONAL TO <see cref="PaymentStatus"/>, NOT A FIFTH VALUE OF IT.</b> That enum is
    /// shared with <c>PaymentPeriod</c> and <c>PaymentTransaction.PaymentTransactionStatus</c>, and
    /// <c>!= PaymentStatus.Paid</c> predicates are everywhere in the FEE module
    /// (<c>GetStudentPaymentStatusCountsAsync</c>, <c>GetPaymentInfoForAttendanceBatchAsync</c>,
    /// <c>GetSessionMonthCollectionAsync</c>, <c>GetUnpaidPeriodsThroughAsync</c>). Every one of them
    /// would silently sweep an <c>Exempt</c> value into "owes money". An exempt obligation is
    /// <c>Unpaid</c> AND <c>IsExempt</c>; "unpaid" means <c>!IsExempt &amp;&amp; PaymentStatus == Unpaid</c>.</para>
    ///
    /// <para>INVARIANT: exempting is BLOCKED when <see cref="AmountPaid"/> &gt; 0 (409 — refund
    /// first), which keeps <c>Σ AmountPaid == Σ non-deleted transactions</c> true in every state.</para>
    /// </summary>
    public bool IsExempt { get; set; } = false;

    /// <summary>UTC instant the exemption was applied.</summary>
    public DateTime? ExemptedAt { get; set; }

    /// <summary>Who exempted the student. Plain column, no FK.</summary>
    public long? ExemptedByUserId { get; set; }

    /// <summary>Optional free-text reason, surfaced to the tutor on the student row.</summary>
    public string? ExemptReason { get; set; }

    // ══════════════════════════════════════════════
    // PER-STUDENT PRICE OVERRIDE (added 2026-09-14)
    // ══════════════════════════════════════════════

    /// <summary>
    /// A human set this student's <see cref="AmountDue"/> by hand.
    ///
    /// <para><b>A FLAG, NOT A COMPARISON.</b> Deriving it as
    /// <c>AmountDue != PaymentEvent.EventAmount</c> is wrong the moment the item's amount is edited:
    /// an amount change re-prices only the UNPAID obligations, so a paid obligation legitimately
    /// differs from the current item amount without anyone having overridden it. The flag is also
    /// what lets the amount-edit path SKIP overridden students — the fix for the bug where the
    /// add-students path recomputed expected revenue as <c>EventAmount × count</c> and clobbered
    /// every custom price.</para>
    /// </summary>
    public bool IsCustomAmount { get; set; } = false;

    /// <summary>Who set the custom amount. Plain column, no FK.</summary>
    public long? CustomAmountSetByUserId { get; set; }

    /// <summary>UTC instant the custom amount was set.</summary>
    public DateTime? CustomAmountSetAt { get; set; }

    /// <summary>
    /// Optimistic-concurrency token. THE reason the collect path can run a bounded retry loop:
    /// two collectors taking money from the same student for the same item would otherwise both
    /// read <c>AmountPaid = 0</c> and the last write would silently lose one payment.
    /// Computed column — adding it repairs no data.
    /// </summary>
    [Timestamp]
    public byte[] RowVersion { get; set; } = null!;

    // Navigation property
    public ICollection<EventPaymentTransaction> EventPaymentTransactions { get; set; } = new List<EventPaymentTransaction>();
}