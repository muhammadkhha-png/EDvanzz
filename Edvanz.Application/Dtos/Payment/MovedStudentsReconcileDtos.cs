namespace Edvanz.Application.Dtos.Payment;

// ════════════════════════════════════════════════════════════════════════════
// ONE-TIME CLEANUP: RECONCILE MOVED STUDENTS (stranded arrears carry-over)
// ════════════════════════════════════════════════════════════════════════════
//
// Output of PaymentService.ReconcileMovedStudentsAsync. In dryRun=true mode NOTHING is
// written; the report describes exactly what WOULD change. In dryRun=false mode the same
// carry-over is applied and the report describes what DID change. The numbers are identical
// for the two modes on the same data because both are built from the same CarryOverPlan.

/// <summary>Top-level report for the reconcile-moved-students cleanup.</summary>
public sealed class MovedStudentsReconcileReport
{
    /// <summary>True when the run only PREVIEWED changes (wrote nothing).</summary>
    public bool DryRun { get; set; }

    /// <summary>Number of students whose stranded arrears were (or would be) carried over.</summary>
    public int StudentsAffected { get; set; }

    /// <summary>Number of students found but skipped (see <see cref="Skipped"/> for the reason).</summary>
    public int StudentsSkipped { get; set; }

    /// <summary>Sum of the full unpaid balances MOVED into students' current sessions.</summary>
    public decimal TotalMovedAmount { get; set; }

    /// <summary>Sum of unpaid FUTURE balances CANCELLED from old sessions.</summary>
    public decimal TotalCancelledAmount { get; set; }

    /// <summary>Sum of the paid parts SETTLED in old sessions as history (partial months).</summary>
    public decimal TotalSettledAmount { get; set; }

    /// <summary>Sum of the partial REMAINDERS re-billed in students' current sessions.</summary>
    public decimal TotalRemainderBilledAmount { get; set; }

    /// <summary>Per-student breakdown of the carry-over.</summary>
    public List<MovedStudentReconcileItem> Students { get; set; } = new();

    /// <summary>Students found but not reconciled (e.g. PerSession billing — out of scope).</summary>
    public List<MovedStudentReconcileSkip> Skipped { get; set; } = new();
}

/// <summary>Per-student carry-over breakdown, aggregated across every stranded old session.</summary>
public sealed class MovedStudentReconcileItem
{
    public long TeacherId { get; set; }
    public long TeacherStudentId { get; set; }
    public string? StudentName { get; set; }
    public string? StudentCode { get; set; }
    public long CurrentSessionId { get; set; }
    public string? CurrentSessionName { get; set; }

    /// <summary>Fully-unpaid due periods moved to the current session.</summary>
    public int PeriodsMoved { get; set; }

    /// <summary>Unpaid future periods cancelled from old sessions.</summary>
    public int PeriodsCancelled { get; set; }

    /// <summary>Partially-paid periods settled in the old session + remainder re-billed.</summary>
    public int PartialsSettled { get; set; }

    /// <summary>
    /// Stranded periods LEFT in place because the current session already bills that month
    /// (overlap guard — never double-bill). Surfaced for manual review; not carried over.
    /// </summary>
    public int OverlapSkipped { get; set; }

    public decimal MovedAmount { get; set; }
    public decimal CancelledAmount { get; set; }
    public decimal SettledAmount { get; set; }
    public decimal RemainderBilledAmount { get; set; }

    /// <summary>One entry per stranded OLD session the student had arrears in.</summary>
    public List<MovedStudentReconcileDetail> Details { get; set; } = new();
}

/// <summary>What happens to the arrears the student had stranded in one specific old session.</summary>
public sealed class MovedStudentReconcileDetail
{
    public long FromSessionId { get; set; }
    public string? FromSessionName { get; set; }

    /// <summary>Month labels (e.g. "March 2026") of the fully-unpaid periods moved to the current session.</summary>
    public List<string> MovedMonths { get; set; } = new();

    /// <summary>Month labels of the unpaid future periods cancelled.</summary>
    public List<string> CancelledMonths { get; set; } = new();

    /// <summary>Month labels of the partially-paid periods settled + remainder re-billed.</summary>
    public List<string> SettledMonths { get; set; } = new();

    /// <summary>Month labels left in place because the current session already bills them (overlap guard).</summary>
    public List<string> OverlapSkippedMonths { get; set; } = new();

    public decimal MovedAmount { get; set; }
    public decimal CancelledAmount { get; set; }
    public decimal SettledAmount { get; set; }
    public decimal RemainderBilledAmount { get; set; }
}

/// <summary>A candidate student that was found but not reconciled.</summary>
public sealed class MovedStudentReconcileSkip
{
    public long TeacherId { get; set; }
    public long TeacherStudentId { get; set; }
    public string? StudentName { get; set; }

    /// <summary>Stable reason code: "PerSessionNotHandled", "CurrentSessionNotFound", "NoStrandedPeriods".</summary>
    public string Reason { get; set; } = null!;
}
