using Edvanz.Domain.Entities.ShareProp;
using Edvanz.Domain.Enums;
using System.ComponentModel.DataAnnotations.Schema;

namespace Edvanz.Domain.Entities;

/// <summary>
/// Materialized payment obligation timeline. One row per student per billing period.
/// For Monthly sessions: one row per calendar month from assignment date to session end.
/// For PerSession sessions: one row per session occurrence from assignment date.
///
/// BR-PAY-001: "Find earliest unpaid period" is the hot-path query — PeriodSequence
///             enables an indexed ORDER BY for O(1) lookup.
/// REQ-PAY-021/022: First period may be pro-rated based on join date.
/// REQ-PAY-086/090: Carried-forward balances from session transfers stored as separate periods.
///
/// Multi-tenant isolation: TeacherId stored directly for tenant-scoped indexes.
/// </summary>
public class PaymentPeriod : BaseEntity
{
    // ══════════════════════════════════════════════
    // TENANT ISOLATION
    // ══════════════════════════════════════════════

    /// <summary>
    /// Foreign key to the owning Teacher.
    /// REQ-PAY-NFR-001: All payment data scoped to individual tutor account.
    /// </summary>
    [ForeignKey(nameof(Teacher))]
    public long TeacherId { get; set; }
    public Teacher Teacher { get; set; } = null!;

    // ══════════════════════════════════════════════
    // SESSION REFERENCE
    // ══════════════════════════════════════════════

    /// <summary>
    /// Foreign key to the session this period belongs to.
    /// Nullable: set to null by application logic before session hard-delete.
    /// </summary>
    public long? SessionId { get; set; }

    [ForeignKey(nameof(SessionId))]
    public Session? Session { get; set; }

    // ══════════════════════════════════════════════
    // STUDENT REFERENCE
    // ══════════════════════════════════════════════

    /// <summary>
    /// Foreign key to the student record.
    /// SET NULL on student permanent purge. Denormalized fields preserve display data.
    /// </summary>
    [ForeignKey(nameof(TeacherStudent))]
    public long? TeacherStudentId { get; set; }
    public TeacherStudent? TeacherStudent { get; set; }

    /// <summary>
    /// Foreign key to the student's session assignment.
    /// Links period to the specific assignment period.
    /// </summary>
    [ForeignKey(nameof(StudentSessionAssignment))]
    public long? StudentSessionAssignmentId { get; set; }
    public StudentSessionAssignment? StudentSessionAssignment { get; set; }

    // ══════════════════════════════════════════════
    // PERIOD DEFINITION
    // ══════════════════════════════════════════════

    /// <summary>
    /// Type of this payment period: Monthly or PerSession.
    /// Mirrors the session's PaymentType at period generation time.
    /// </summary>
    public PeriodType PeriodType { get; set; }

    /// <summary>
    /// Start date of this payment period.
    /// Monthly: first day of the month. PerSession: occurrence date.
    /// </summary>
    [Column(TypeName = "date")]
    public DateTime PeriodStart { get; set; }

    /// <summary>
    /// End date of this payment period.
    /// Monthly: last day of the month. PerSession: same as PeriodStart.
    /// </summary>
    [Column(TypeName = "date")]
    public DateTime PeriodEnd { get; set; }

    // ══════════════════════════════════════════════
    // FINANCIAL DATA
    // ══════════════════════════════════════════════

    /// <summary>
    /// The amount due for this period.
    /// REQ-PAY-015/016: Session default or custom student amount.
    /// REQ-PAY-021: May be pro-rated for first period.
    /// </summary>
    [Column(TypeName = "decimal(10,2)")]
    public decimal AmountDue { get; set; }

    /// <summary>
    /// Total amount paid so far for this period (sum of transactions).
    /// Updated atomically when payments are applied.
    /// </summary>
    [Column(TypeName = "decimal(10,2)")]
    public decimal AmountPaid { get; set; } = 0;

    /// <summary>
    /// The amount of this period the teacher has FORGIVEN (waived) — NOT cash. Forgiving reduces what
    /// the student owes without any wallet/transaction/collector effect. The live outstanding for a
    /// period is therefore <c>AmountDue − AmountPaid − ForgivenAmount</c>, and a period is settled when
    /// <c>AmountPaid + ForgivenAmount &gt;= AmountDue</c>. Nullable (treated as 0) — additive column,
    /// backfilled to 0 for every pre-existing row. Written only by the forgive/reverse flow; every
    /// arrears/outstanding query subtracts it (see IPaymentRepo outstanding helpers).
    /// </summary>
    [Column(TypeName = "decimal(10,2)")]
    public decimal? ForgivenAmount { get; set; }

    /// <summary>
    /// Current payment status derived from AmountDue vs AmountPaid (+ ForgivenAmount).
    /// Maintained by the service layer for O(1) status lookup. A fully forgiven/paid period is
    /// <c>Paid</c> so it drops out of every <c>PaymentStatus != Paid</c> arrears query.
    /// </summary>
    public PaymentStatus PaymentStatus { get; set; } = PaymentStatus.Unpaid;

    // ══════════════════════════════════════════════
    // PRO-RATING (REQ-PAY-021/022)
    // ══════════════════════════════════════════════

    /// <summary>
    /// Whether this period has a pro-rated amount.
    /// REQ-PAY-022: Only applies to the student's first payment period.
    /// BR-PAY-005: Only for Monthly sessions, never for PerSession.
    /// </summary>
    public bool IsProRated { get; set; } = false;

    /// <summary>
    /// The pro-rate fraction applied (e.g., 0.6667 for two-thirds).
    /// REQ-PAY-021: Based on configurable tier boundaries.
    /// </summary>
    [Column(TypeName = "decimal(5,4)")]
    public decimal ProRatedFraction { get; set; } = 1.0m;

    /// <summary>
    /// True for the ONE month that is eligible for attendance-anchored proration — the first month
    /// of a genuinely NEW enrollment (never a transfer/move, never a carried-forward or per-session
    /// period). Set at generation; consumed by the first-attendance re-proration and the settings
    /// reconcile so they know which single period to re-price (and NEVER a transferred student's).
    /// Independent of <see cref="IsProRated"/> (a day-1–10 new joiner is anchor-eligible yet not
    /// prorated until they attend mid-month). Additive column, default false.
    /// </summary>
    public bool IsProrationAnchorMonth { get; set; } = false;

    /// <summary>
    /// True when a HUMAN (teacher or assistant) explicitly set this anchor month's joining amount
    /// (REQ-PAY-021/022, teacher-decided proration 2026-09-02) via the per-student proration endpoint.
    /// A manual amount is STICKY: the auto re-proration paths (first-attendance re-anchor, settings
    /// reconcile) and every price-change reprice SKIP a period with this flag, so a later monthly-price
    /// change never clobbers a number a person chose. Cleared when the override is removed (reverts to
    /// the method's auto suggestion). Additive column, <c>bit NOT NULL default 0</c>.
    /// </summary>
    public bool IsProrationManual { get; set; } = false;

    // ══════════════════════════════════════════════
    // ORDERING
    // ══════════════════════════════════════════════

    /// <summary>
    /// Sequential order of this period for the student in this session.
    /// BR-PAY-001: Enables indexed "find earliest unpaid" query.
    /// Starts at 1 and increments for each period.
    /// </summary>
    public int PeriodSequence { get; set; }

    // ══════════════════════════════════════════════
    // SESSION TRANSFER TRACKING (REQ-PAY-086/090)
    // ══════════════════════════════════════════════

    /// <summary>
    /// Whether this period was carried forward from a previous session during transfer.
    /// REQ-PAY-090: Carried-forward balances are separate line items.
    /// </summary>
    public bool IsCarriedForward { get; set; } = false;

    /// <summary>
    /// Name of the original session this balance was carried from.
    /// REQ-PAY-090: Clearly labeled with originating session name.
    /// </summary>
    public string? OriginSessionName { get; set; }

    /// <summary>
    /// The session this billing obligation was MOVED FROM when the student was reassigned to a new
    /// session (A → B). Set on a period whose unpaid/partial balance was carried over to the new
    /// session so the money is trackable back to where it was originally owed. Null for a normal
    /// (non-moved) period. Plain denormalized id — NO FK (survives the source session's hard-delete;
    /// see §4.1: a plain nullable long avoids the FK/OnDelete conflict entirely).
    /// </summary>
    public long? MovedFromSessionId { get; set; }

    /// <summary>
    /// Denormalized snapshot of the source session's name at move time. Pairs with
    /// <see cref="MovedFromSessionId"/> so the frontend can render "moved from &lt;name&gt;" without a
    /// join, and it survives the source session's hard-delete. Null for a normal (non-moved) period.
    /// </summary>
    public string? MovedFromSessionName { get; set; }

    // ══════════════════════════════════════════════
    // DENORMALIZED CONTEXT
    // ══════════════════════════════════════════════

    /// <summary>
    /// Denormalized: snapshot of session name at period generation time.
    /// Survives session hard-deletion.
    /// </summary>
    public string SessionName { get; set; } = null!;

    /// <summary>
    /// Denormalized: snapshot of student name at period generation time.
    /// Survives student permanent purge.
    /// </summary>
    public string? StudentName { get; set; }

    /// <summary>
    /// Denormalized: snapshot of student code at period generation time.
    /// Survives student permanent purge.
    /// </summary>
    public string? StudentCode { get; set; }

    // Navigation property
    public ICollection<PaymentTransaction> PaymentTransactions { get; set; } = new List<PaymentTransaction>();
}