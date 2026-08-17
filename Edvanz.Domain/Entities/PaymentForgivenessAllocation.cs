using Edvanz.Domain.Entities.ShareProp;
using System.ComponentModel.DataAnnotations.Schema;

namespace Edvanz.Domain.Entities;

/// <summary>
/// Per-period ledger for a <see cref="PaymentForgiveness"/>. One row per <see cref="PaymentPeriod"/>
/// the forgiveness reduced, recording how much of the waiver landed on that period. Mirrors
/// <see cref="PaymentTransactionAllocation"/> (the cash-side ledger) so a reversal can restore the
/// EXACT per-period <see cref="PaymentPeriod.ForgivenAmount"/> it removed.
/// </summary>
public class PaymentForgivenessAllocation : BaseEntity
{
    /// <summary>Owning teacher. Denormalized for tenant-scoped safety (mirrors the sibling entities).</summary>
    public long TeacherId { get; set; }

    /// <summary>The forgiveness this slice belongs to.</summary>
    public long PaymentForgivenessId { get; set; }
    public PaymentForgiveness PaymentForgiveness { get; set; } = null!;

    /// <summary>The period this slice waived. CASCADE with the period (hard-deleted on student purge).
    /// Nullable only to satisfy the relational model.</summary>
    public long? PaymentPeriodId { get; set; }
    public PaymentPeriod? PaymentPeriod { get; set; }

    /// <summary>The portion of the forgiveness applied to <see cref="PaymentPeriod"/>.</summary>
    [Column(TypeName = "decimal(10,2)")]
    public decimal AmountForgiven { get; set; }
}
