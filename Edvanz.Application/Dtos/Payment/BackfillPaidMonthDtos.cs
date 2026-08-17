namespace Edvanz.Application.Dtos.Payment;

// ════════════════════════════════════════════════════════════════════════════
// ADMIN ONE-OFF: BACKFILL A PAID MONTH BY MOVING AN ADVANCE PAYMENT
// ════════════════════════════════════════════════════════════════════════════
//
// Output of PaymentService.BackfillPaidMonthAsync. In dryRun=true mode NOTHING is written;
// the report describes exactly what WOULD change (created month + amount, the advance month
// un-settled, allocations moved, transactions repointed). In dryRun=false mode the same move
// is applied inside one transaction and the report describes what DID change. The numbers are
// identical for the two modes on the same data.

/// <summary>Report for the backfill-paid-month admin op (move an advance payment back a month).</summary>
public sealed class BackfillPaidMonthReport
{
    /// <summary>True when the run only PREVIEWED the change (wrote nothing).</summary>
    public bool DryRun { get; set; }

    // ── Student / tenant context (resolved from the source period) ──
    public long TeacherId { get; set; }
    public long TeacherStudentId { get; set; }
    public string? StudentName { get; set; }
    public string? StudentCode { get; set; }
    public long? SessionId { get; set; }
    public string? SessionName { get; set; }

    // ── The target month that becomes Paid (created new) ──
    /// <summary>The backfilled month, "YYYY-MM".</summary>
    public string TargetMonth { get; set; } = null!;
    /// <summary>Friendly label, e.g. "July 2026".</summary>
    public string TargetMonthLabel { get; set; } = null!;
    /// <summary>AmountDue of the created period = the student's monthly rate (CustomPaymentAmount ?? session amount).</summary>
    public decimal TargetAmountDue { get; set; }
    /// <summary>AmountPaid of the created period = the advance month's AmountPaid.</summary>
    public decimal TargetAmountPaid { get; set; }
    /// <summary>PeriodSequence assigned to the created period (sorts before the earliest existing period).</summary>
    public int TargetPeriodSequence { get; set; }
    /// <summary>Id of the created period. 0 on a dry-run (nothing written).</summary>
    public long TargetPeriodId { get; set; }

    // ── The advance month that is un-settled back to Unpaid ──
    /// <summary>The advance month whose payment is moved, "YYYY-MM".</summary>
    public string FromAdvanceMonth { get; set; } = null!;
    /// <summary>Friendly label, e.g. "September 2026".</summary>
    public string FromAdvanceMonthLabel { get; set; } = null!;
    public long FromAdvancePeriodId { get; set; }
    /// <summary>The advance month's AmountPaid BEFORE the move (goes to 0).</summary>
    public decimal FromAdvancePreviousAmountPaid { get; set; }

    // ── Ledger movement ──
    /// <summary>Number of PaymentTransactionAllocation rows repointed from the advance period to the target period.</summary>
    public int AllocationsMoved { get; set; }
    /// <summary>Sum of the moved allocations' AmountApplied (equals the advance month's AmountPaid).</summary>
    public decimal AllocationsMovedAmount { get; set; }
    /// <summary>Number of PaymentTransaction rows whose denormalized PaymentPeriodId was repointed to the target period.</summary>
    public int TransactionsRepointed { get; set; }

    // ── Invariant check (money must not appear or vanish) ──
    /// <summary>Sum of AmountPaid across ALL of the student's periods BEFORE the move.</summary>
    public decimal TotalPaidBefore { get; set; }
    /// <summary>Sum of AmountPaid across ALL of the student's periods AFTER the move — must equal <see cref="TotalPaidBefore"/>.</summary>
    public decimal TotalPaidAfter { get; set; }
}
