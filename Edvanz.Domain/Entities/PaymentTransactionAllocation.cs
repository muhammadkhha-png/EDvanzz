using Edvanz.Domain.Entities.ShareProp;
using System.ComponentModel.DataAnnotations.Schema;

namespace Edvanz.Domain.Entities;

/// <summary>
/// Per-period settlement ledger for a <see cref="PaymentTransaction"/>.
/// One row per <see cref="PaymentPeriod"/> that a single cash event settled, recording exactly how
/// much of that transaction's cash was applied to each period.
///
/// WHY THIS EXISTS (BUG PAY-1 / the reverse side of the §7.4 cascade engine):
/// A monthly collection fills the oldest unpaid month first and cascades forward, so one
/// <see cref="PaymentTransaction"/> can settle several <see cref="PaymentPeriod"/>s while storing a
/// single <c>PaymentPeriodId</c> (the first one touched). Without a per-period ledger, a refund/edit
/// could only reverse that one period — leaving the later months reading <c>Paid</c> with no backing
/// cash. This ledger lets <c>DeletePaymentAsync</c>/<c>EditPaymentAsync</c>/batch-revert reverse the
/// EXACT set of periods (and amounts) the payment settled.
///
/// LIFECYCLE: created on collect (one per settled period); reduced/removed when the transaction is
/// refunded or edited down; extended when a transaction is edited up (cascade forward). Legacy
/// transactions collected before this ledger existed have no rows — reversal falls back to the
/// single denormalized <c>PaymentTransaction.PaymentPeriodId</c>.
///
/// Cash-total reversal (counters/wallets) is driven by the transaction amount, NOT this ledger — the
/// ledger governs period-state accuracy only, so a purged period never distorts the cash counters.
///
/// Multi-tenant isolation: TeacherId stored directly for tenant-scoped safety (mirrors the sibling
/// payment entities).
/// </summary>
public class PaymentTransactionAllocation : BaseEntity
{
    // ══════════════════════════════════════════════
    // TENANT ISOLATION
    // ══════════════════════════════════════════════

    /// <summary>
    /// Foreign key to the owning Teacher. Denormalized for tenant-scoped safety.
    /// REQ-PAY-NFR-001: All payment data scoped to individual tutor account.
    /// </summary>
    public long TeacherId { get; set; }

    // ══════════════════════════════════════════════
    // LEDGER LINKS
    // ══════════════════════════════════════════════

    /// <summary>
    /// The transaction whose cash this allocation belongs to.
    /// FK is NoAction: transactions are only ever soft-deleted, and reversal removes the allocation
    /// rows explicitly — so the cascade path is never exercised.
    /// </summary>
    public long PaymentTransactionId { get; set; }
    public PaymentTransaction PaymentTransaction { get; set; } = null!;

    /// <summary>
    /// The period this slice of cash settled.
    /// FK is CASCADE: when a period is hard-deleted on student purge (see
    /// <c>NullifyStudentReferencesOnPaymentRecordsAsync</c>), its allocations go with it — a slice
    /// of a deleted obligation is meaningless. Nullable only to satisfy the relational model.
    /// </summary>
    public long? PaymentPeriodId { get; set; }
    public PaymentPeriod? PaymentPeriod { get; set; }

    // ══════════════════════════════════════════════
    // FINANCIAL DATA
    // ══════════════════════════════════════════════

    /// <summary>
    /// The portion of the transaction's cash applied to <see cref="PaymentPeriod"/>.
    /// Sum of a transaction's allocations equals its <c>AmountPaid</c> while it is active.
    /// </summary>
    [Column(TypeName = "decimal(10,2)")]
    public decimal AmountApplied { get; set; }
}
