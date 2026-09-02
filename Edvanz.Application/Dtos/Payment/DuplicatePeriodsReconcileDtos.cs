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

// ════════════════════════════════════════════════════════════════════════════
// ONE-TIME CLEANUP: RECONCILE ORPHANED (SESSION-LESS) PAYMENT PERIODS
// ════════════════════════════════════════════════════════════════════════════
//
// Output of PaymentService.ReconcileOrphanedPeriodsAsync. Applies the session-delete money lifecycle
// to LEGACY orphans left by the OLD delete (which just nulled SessionId, leaving unpaid periods —
// monthly OR per-session — to linger as inflated obligations, e.g. student 134A's Aug×4 @35).
// Per student: future-unpaid orphans are VOIDED and unpaid arrears-through-current-month are collapsed
// into ONE pending monthly carry-forward debt per month (which re-prices to the next session on
// reassignment). Paid orphans are kept as history. dryRun=true previews and writes nothing.

/// <summary>Top-level report for the reconcile-orphaned-periods cleanup.</summary>
public sealed class OrphanedPeriodsReconcileReport
{
    public bool DryRun { get; set; }
    public long? TeacherId { get; set; }
    /// <summary>Students with at least one orphaned unpaid period.</summary>
    public int StudentsAffected { get; set; }
    /// <summary>Future-unpaid orphan periods voided (or that WOULD be).</summary>
    public int PeriodsVoided { get; set; }
    /// <summary>Arrears orphan periods consolidated (collapsed into pending months).</summary>
    public int ArrearsConsolidated { get; set; }
    /// <summary>Pending monthly carry-forward debts created.</summary>
    public int PendingMonthsCreated { get; set; }
    public List<OrphanedPeriodsStudentItem> Students { get; set; } = new();
}

/// <summary>Per-student orphaned-period cleanup breakdown.</summary>
public sealed class OrphanedPeriodsStudentItem
{
    public long TeacherId { get; set; }
    public long TeacherStudentId { get; set; }
    public string? StudentName { get; set; }
    public string? StudentCode { get; set; }
    public int PeriodsVoided { get; set; }
    public int ArrearsConsolidated { get; set; }
    public int PendingMonthsCreated { get; set; }
    /// <summary>Owed total across the pending carry-forward months created (old amount, pre-reprice).</summary>
    public decimal PendingOwed { get; set; }
}

// ════════════════════════════════════════════════════════════════════════════
// REMEDIATION: RE-PRORATE NEVER-PAID FIRST-MONTH CARRIED ANCHORS
// ════════════════════════════════════════════════════════════════════════════
//
// Output of PaymentService.ReprorateCarriedAnchorsAsync. Repairs students whose genuine prorated FIRST
// month was WIPED to full price when they were moved / reassigned between sessions before paying anything
// (root cause fixed going-forward in ApplyCarryOverPlanAsync + the DB2a fold-in). For each AFFECTED
// student the carried first-month period is re-priced to round(sessionOrCustom × first-attendance
// fraction), its IsProRated / ProRatedFraction / IsProrationAnchorMonth restored, and its
// StudentPaymentCounter resynced from records. A candidate that does not qualify (proration disabled,
// already prorated, any cash paid, no first-attendance in the anchor month, or a full-price tier) is
// reported under Skipped with a reason and left untouched. dryRun=true previews and writes NOTHING.

/// <summary>Top-level report for the never-paid first-month carried-anchor re-proration remediation.</summary>
public sealed class CarriedAnchorReprorationReport
{
    /// <summary>True when the run only PREVIEWED changes (wrote nothing).</summary>
    public bool DryRun { get; set; }

    /// <summary>Teacher the run was scoped to, or null for every teacher.</summary>
    public long? TeacherId { get; set; }

    /// <summary>Students whose carried first-month anchor was re-prorated (or that WOULD be).</summary>
    public int StudentsAffected { get; set; }

    /// <summary>Total AmountDue reduction across the re-prorated anchors (sum of old − new).</summary>
    public decimal TotalAmountReduced { get; set; }

    /// <summary>Candidate owners scanned but SKIPPED (did not qualify) — see each item's Reason.</summary>
    public int CandidatesSkipped { get; set; }

    /// <summary>Per-student breakdown of the anchors re-prorated.</summary>
    public List<CarriedAnchorStudentItem> Students { get; set; } = new();

    /// <summary>Per-candidate breakdown of the owners skipped, with the reason.</summary>
    public List<CarriedAnchorSkippedItem> Skipped { get; set; } = new();
}

/// <summary>Per-student re-proration breakdown (one carried first-month anchor repaired).</summary>
public sealed class CarriedAnchorStudentItem
{
    public long TeacherId { get; set; }
    public long TeacherStudentId { get; set; }
    public string? StudentName { get; set; }
    public string? StudentCode { get; set; }

    /// <summary>The carried first-month PaymentPeriod re-prorated.</summary>
    public long PeriodId { get; set; }

    /// <summary>Month label of the anchor (e.g. "August 2026").</summary>
    public string? MonthLabel { get; set; }

    /// <summary>AmountDue before the fix (the wiped full price).</summary>
    public decimal OldAmountDue { get; set; }

    /// <summary>AmountDue after the fix (round(base × fraction)).</summary>
    public decimal NewAmountDue { get; set; }

    /// <summary>The restored proration fraction (from the first-attendance day's tier).</summary>
    public decimal ProRatedFraction { get; set; }

    /// <summary>The first-attendance date that anchored the fraction.</summary>
    public DateTime FirstAttendanceDate { get; set; }
}

/// <summary>A candidate owner scanned but not re-prorated, with the reason.</summary>
public sealed class CarriedAnchorSkippedItem
{
    public long TeacherId { get; set; }
    public long TeacherStudentId { get; set; }
    public string? StudentCode { get; set; }

    /// <summary>Why the candidate did not qualify (e.g. "ProrationDisabled", "AlreadyProrated",
    /// "HasPaidPeriods", "NoFirstAttendanceInAnchorMonth", "FullPriceTier", "NotEarliestPeriod").</summary>
    public string Reason { get; set; } = string.Empty;
}
