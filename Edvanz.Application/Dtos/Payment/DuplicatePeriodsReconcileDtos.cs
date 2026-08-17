namespace Edvanz.Application.Dtos.Payment;

// ════════════════════════════════════════════════════════════════════════════
// ONE-TIME CLEANUP: RECONCILE DUPLICATE PAYMENT PERIODS
// ════════════════════════════════════════════════════════════════════════════
//
// Output of PaymentService.ReconcileDuplicatePeriodsAsync. Repairs students who ended up with a
// DUPLICATE period ladder for the same session (root cause: a non-idempotent assign — fixed in
// OnStudentAssignedToSessionAsync — regenerated a full parallel ladder, only skipping PAID months,
// so the still-unpaid months were duplicated). The junk twin (an empty Unpaid period with no cash
// and no transaction/allocation pointing at it) mislabels a paid student as unpaid and desyncs the
// collected-by-session card from the roster.
//
// A "duplicate group" = periods with the same (TeacherStudentId, SessionId, period MONTH). Within a
// group ONE period is KEPT — a money/meaning period (paid, partial, forgiven, carried, or referenced
// by a transaction/allocation) if present, else the lowest-sequence empty one — and the OTHER empty,
// UNREFERENCED Unpaid twins are deleted. A twin that holds cash or a reference is NEVER deleted; it is
// surfaced under Conflicts for manual review. In dryRun=true NOTHING is written.

/// <summary>Top-level report for the reconcile-duplicate-periods cleanup.</summary>
public sealed class DuplicatePeriodsReconcileReport
{
    /// <summary>True when the run only PREVIEWED changes (wrote nothing).</summary>
    public bool DryRun { get; set; }

    /// <summary>Teacher the run was scoped to, or null for every teacher.</summary>
    public long? TeacherId { get; set; }

    /// <summary>Students scanned that had at least one duplicate group.</summary>
    public int StudentsAffected { get; set; }

    /// <summary>Total redundant duplicate periods deleted (or that WOULD be deleted).</summary>
    public int PeriodsDeleted { get; set; }

    /// <summary>Sum of AmountDue over the deleted junk periods (all zero-cash by construction).</summary>
    public decimal DeletedAmountDue { get; set; }

    /// <summary>Duplicate groups whose extra twin held cash or a transaction/allocation and so was
    /// LEFT UNTOUCHED for manual review (should be rare / none).</summary>
    public int Conflicts { get; set; }

    /// <summary>Per-student breakdown.</summary>
    public List<DuplicatePeriodsStudentItem> Students { get; set; } = new();
}

/// <summary>Per-student duplicate-period cleanup breakdown.</summary>
public sealed class DuplicatePeriodsStudentItem
{
    public long TeacherId { get; set; }
    public long TeacherStudentId { get; set; }
    public string? StudentName { get; set; }
    public string? StudentCode { get; set; }

    /// <summary>Number of duplicate groups found for this student.</summary>
    public int DuplicateGroups { get; set; }

    /// <summary>Redundant empty twins deleted for this student.</summary>
    public int PeriodsDeleted { get; set; }

    /// <summary>Sum of AmountDue over this student's deleted twins.</summary>
    public decimal DeletedAmountDue { get; set; }

    /// <summary>Month labels (e.g. "August 2026") of the deleted twins, tagged with their session.</summary>
    public List<string> DeletedMonths { get; set; } = new();

    /// <summary>Month labels of duplicate groups whose extra twin held cash/reference and was left
    /// alone (manual review needed).</summary>
    public List<string> ConflictMonths { get; set; } = new();
}
