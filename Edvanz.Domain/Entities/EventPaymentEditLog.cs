using Edvanz.Domain.Entities.ShareProp;
using Edvanz.Domain.Enums;
using System.ComponentModel.DataAnnotations.Schema;

namespace Edvanz.Domain.Entities;

/// <summary>
/// Audit trail for every change to a "Books &amp; fees" obligation or collected payment: refunds,
/// amount corrections, exemptions, and roster add/remove. Also the SOURCE of the negative (refund)
/// rows in the collections ledger — the same way <see cref="PaymentEditLog"/> is for fees.
///
/// WHY NOT <see cref="PaymentEditLog"/>: <c>PaymentRepo.GetCollectorRefundsInRangeAsync</c> derives
/// the fee refund ledger from that table with <c>IgnoreQueryFilters()</c>, and several other readers
/// (the per-transaction edit history, the proration-decision reader keyed on
/// <c>PaymentPeriodId != null</c>, the admin wallet-recompute tool) query it too. Adding extras rows
/// there would require each of them to PROVE it excludes them — the same "a shared identifier is
/// dangerous" argument that keeps <c>Exempt</c> out of the shared <see cref="PaymentStatus"/> enum.
/// A separate table is additive and free.
///
/// EVERY SUBJECT REFERENCE IS A BARE NULLABLE LONG WITH NO FK (the
/// <see cref="PaymentEditLog.PaymentPeriodId"/> precedent), and the student/item names are
/// denormalized, so a refund row still renders after the transaction is deleted, the item is
/// deleted, or the student is purged.
/// </summary>
public class EventPaymentEditLog : BaseEntity
{
    /// <summary>Denormalized tenant. FK to Teacher with NoAction.</summary>
    [ForeignKey(nameof(Teacher))]
    public long TeacherId { get; set; }
    public Teacher Teacher { get; set; } = null!;

    // ── Subject references: plain columns, NO foreign keys (the log must outlive its subject) ──

    public long? EventPaymentTransactionId { get; set; }
    public long? PaymentEventId { get; set; }
    public long? EventStudentObligationId { get; set; }
    public long? TeacherStudentId { get; set; }

    // ── Denormalized labels, so a ledger row renders after a purge ──

    public string? StudentName { get; set; }
    public string? StudentCode { get; set; }
    public string? EventName { get; set; }

    public EventPaymentEditAction EditAction { get; set; }

    [Column(TypeName = "decimal(10,2)")]
    public decimal PreviousAmount { get; set; }

    [Column(TypeName = "decimal(10,2)")]
    public decimal NewAmount { get; set; }

    /// <summary>
    /// The collector whose wallet this money comes OUT of. Encoding the attribution rule at WRITE
    /// time (rather than re-deriving it in a two-branch CASE at read time, as the fee refund
    /// derivation must) keeps the ledger query a plain equality.
    ///
    /// For extras every case is a CORRECTION, so this is the ORIGINAL collector
    /// (<c>EventPaymentTransaction.CollectedByUserId</c>) whose figure it corrects — not the person
    /// who performed the correction. A future physical-payout kind would be one more branch here,
    /// at write time only.
    /// </summary>
    public long? ChargedToUserId { get; set; }

    /// <summary>
    /// The reversed cash's ORIGINAL collection instant (UTC), copied off the transaction. Lets
    /// <c>AdjustCollectorWalletAsync</c> apply its reset-aware rule — cash already handed over must
    /// not push <c>CurrentBalance</c> falsely negative — without re-reading a transaction that may
    /// by then be deleted or purged.
    /// </summary>
    public DateTime? CollectedAt { get; set; }

    /// <summary>Who performed the change. Plain column, no FK (BR-PAY-002 audit).</summary>
    public long? EditedByUserId { get; set; }

    public DateTime EditedAt { get; set; }

    public string? EditReason { get; set; }
}
