namespace Edvanz.Domain.Models;

/// <summary>
/// Which of the five populations an obligation belongs to. Projected as a typed byte via a SQL
/// <c>CASE</c> so the whole tracking tally is ONE <c>GROUP BY</c>.
///
/// <para><b>Never expressed as separate <c>Count(predicate)</c> aggregates.</b> Nine predicate
/// counts in one projection re-inline the row's correlated members once per reference, and a
/// predicate that folds to a compile-time constant becomes literal <c>COUNT(NULL)</c>, which SQL
/// Server rejects outright (BUG-16). A bucketed <c>GroupBy</c> cannot do either.</para>
/// </summary>
public enum ExtrasBucket : byte
{
    Unpaid = 0,
    PartiallyPaid = 1,
    Paid = 2,

    /// <summary>
    /// Not taking it. ORTHOGONAL to payment status — an exempt obligation is <c>Unpaid</c> AND
    /// <c>IsExempt</c> — but it occupies its own bucket here because the five populations must be
    /// mutually exclusive to sum to the header.
    /// </summary>
    Exempt = 3
}

/// <summary>
/// One cell of the tracking tally: a (session, group, bucket) triple with its count and money.
/// A NAMED type because the grouped projection is shared by the header, the by-session breakdown
/// and the by-group breakdown — all three are folded from THIS one result, so they cannot disagree.
/// </summary>
public sealed class ExtrasTallyRow
{
    /// <summary>
    /// The student's CURRENT session, not the scope the item targeted. A tutor asking "who in the
    /// 7 PM class hasn't paid" means today's class; keying on the scope would file a student who
    /// moved under their old one. This is also the same column the audience resolves through, which
    /// is what guarantees the breakdown rows sum to the header (§7.8).
    /// </summary>
    public long? SessionId { get; set; }

    public string? SessionName { get; set; }
    public long? SessionGroupId { get; set; }
    public string? SessionGroupName { get; set; }

    public ExtrasBucket Bucket { get; set; }
    public int StudentCount { get; set; }
    public decimal AmountDue { get; set; }
    public decimal AmountPaid { get; set; }
}

/// <summary>One collector's contribution to a single item — activity-driven, never roster-driven.</summary>
public sealed class ExtrasCollectorTallyRow
{
    public long? CollectedByUserId { get; set; }
    public decimal CollectedAmount { get; set; }
    public int TransactionCount { get; set; }
    public int StudentCount { get; set; }
}

/// <summary>
/// One row of an item's student roster. <c>LastPaymentAt</c> / <c>LastCollectedByUserId</c> are
/// typed scalar SUBQUERIES rather than denormalized columns: two more denormalized fields × four
/// writers (collect / refund / edit / sync) is exactly the drift this codebase keeps getting burned
/// by. The guard lives INSIDE each subquery's <c>Where</c> — never <c>cond ? subquery : null</c>,
/// which puts an untyped NULL in the SQL tree and fails at query-compile time (BUG-7).
/// </summary>
public sealed class ExtrasObligationRow
{
    public long ObligationId { get; set; }
    public long? TeacherStudentId { get; set; }
    public string? StudentName { get; set; }
    public string? StudentCode { get; set; }

    public long? SessionId { get; set; }
    public string? SessionName { get; set; }
    public long? SessionGroupId { get; set; }
    public string? SessionGroupName { get; set; }

    public decimal AmountDue { get; set; }
    public decimal AmountPaid { get; set; }
    public Enums.PaymentStatus PaymentStatus { get; set; }

    public bool IsExempt { get; set; }
    public DateTime? ExemptedAt { get; set; }
    public long? ExemptedByUserId { get; set; }
    public string? ExemptReason { get; set; }

    public bool IsCustomAmount { get; set; }
    public long? CustomAmountSetByUserId { get; set; }
    public DateTime? CustomAmountSetAt { get; set; }

    public DateTime? LastPaymentAt { get; set; }
    public long? LastCollectedByUserId { get; set; }
    public int TransactionsCount { get; set; }
}

/// <summary>
/// An item's page-level aggregates, folded from one grouped query keyed on
/// <c>PaymentEventId IN (@pageIds)</c> — bounded by page size, never N+1, and never read from the
/// cached <c>TotalStudents</c> / <c>TotalExpectedRevenue</c> / <c>TotalCollectedRevenue</c> columns.
///
/// <para>Those three columns are treated as a CACHE and no response depends on them. Three separate
/// write paths already clobbered them independently; deriving from the rows makes every ordering
/// identical and cannot drift.</para>
/// </summary>
public sealed class ExtrasItemTotals
{
    public long PaymentEventId { get; set; }
    public int TotalStudents { get; set; }
    public int PaidStudents { get; set; }
    public int PartiallyPaidStudents { get; set; }
    public int UnpaidStudents { get; set; }
    public int ExemptStudents { get; set; }
    public decimal ExpectedAmount { get; set; }
    public decimal CollectedAmount { get; set; }
    public decimal ExemptAmount { get; set; }
}

/// <summary>
/// One unpaid "Books &amp; fees" due for the attendance combined-collect sheet. Student-keyed,
/// because the sheet is opened for one student at the classroom door.
/// </summary>
public sealed class ExtrasDebtRow
{
    public long TeacherStudentId { get; set; }
    public long ObligationId { get; set; }
    public long PaymentEventId { get; set; }

    /// <summary>The item's LIVE name, so a rename shows immediately.</summary>
    public string ItemName { get; set; } = null!;

    public decimal AmountDue { get; set; }
    public decimal AmountPaid { get; set; }
    public DateTime ItemDate { get; set; }
}

/// <summary>
/// One targeting row of a "Books &amp; fees" item, with its target's LIVE name resolved — the raw
/// material for the audience summary on the item list and the edit form.
///
/// <para>Read from <c>PaymentEventScopes</c>, never from the legacy comma-joined
/// <c>PaymentEvent.TargetScopeIds</c> string: that column stores resolved STUDENT ids regardless
/// of the scope type, which is exactly why "by session / by group" was unanswerable before the
/// scope table existed.</para>
///
/// <para>Names are resolved live rather than denormalized. A session rename must show
/// immediately here, and unlike a history row (§7.10) this is a pointer at a session that still
/// exists — the scope row is deleted with the session.</para>
/// </summary>
public sealed class ExtrasScopeSummaryRow
{
    public long PaymentEventId { get; set; }

    /// <summary>Session | SessionGroup | AllStudents. Never IndividualStudents — individually
    /// targeted students are recorded by their obligations, not by a scope row.</summary>
    public byte ScopeType { get; set; }

    /// <summary>The session's or group's name; null for an <c>AllStudents</c> row, which has no
    /// target, and null for a target that has since been deleted.</summary>
    public string? TargetName { get; set; }
}
