using Edvanz.Domain.Entities.ShareProp;
using Edvanz.Domain.Enums;
using System.ComponentModel.DataAnnotations.Schema;

namespace Edvanz.Domain.Entities;

/// <summary>
/// Records the full context of a student departure including pro-rated calculations.
/// REQ-PAY-066: Student Departure feature for formal departure recording.
/// REQ-PAY-068/069/070: Pro-rated obligation calculation and outcome.
/// REQ-PAY-072: Departure summary displayed before confirmation.
/// REQ-PAY-073: Departure event permanently recorded in payment history.
/// REQ-PAY-075: Tutor override logged alongside original calculated amount.
///
/// Denormalized for post-delete/purge display. Never deleted.
///
/// Multi-tenant isolation: TeacherId stored directly.
/// </summary>
public class StudentDeparture : BaseEntity
{
    /// <summary>
    /// Foreign key to the owning Teacher.
    /// REQ-PAY-NFR-001: All payment data scoped to individual tutor account.
    /// </summary>
    [ForeignKey(nameof(Teacher))]
    public long TeacherId { get; set; }
    public Teacher Teacher { get; set; } = null!;

    /// <summary>
    /// Foreign key to the departing student.
    /// SET NULL on permanent purge. Denormalized fields preserve display data.
    /// </summary>
    [ForeignKey(nameof(TeacherStudent))]
    public long? TeacherStudentId { get; set; }
    public TeacherStudent? TeacherStudent { get; set; }

    /// <summary>
    /// The session the student was assigned to at departure time.
    /// Nullable: survives session hard-delete.
    /// </summary>
    public long? SessionId { get; set; }

    [ForeignKey(nameof(SessionId))]
    public Session? Session { get; set; }

    // ══════════════════════════════════════════════
    // DENORMALIZED CONTEXT (REQ-PAY-072)
    // ══════════════════════════════════════════════

    /// <summary>
    /// Denormalized: session name at departure time.
    /// </summary>
    /// <remarks>
    /// THIS IS THE NAME — read it directly. It is denormalized so the surfaces that show a class
    /// name cost no join, on paths that run per mark and per page.
    ///
    /// It is written when the student left, and rewritten by
    /// <see cref="Edvanz.Domain.Interfaces.ISessionRepo.PropagateSessionNameAsync"/> whenever the
    /// session is renamed, so it never goes stale while the session exists. It exists at all
    /// because sessions are HARD-deleted (BR-ATT-005): on that delete <c>SessionId</c> is NULLed
    /// and nothing writes this row again, so the last-known name is what survives.
    ///
    /// Do NOT reintroduce a live lookup through <c>Session.SessionName</c>. That was tried and
    /// removed: it cost a query per mark on the attendance path and a correlated subquery per row
    /// on the payment tabs, every day, to compensate for an event that happens a handful of times
    /// in a session's life. Enforced by scripts/check-session-name-propagation.sh; CLAUDE.md §7.10.
    /// </remarks>
    public string SessionNameAtDeparture { get; set; } = null!;

    /// <summary>
    /// Denormalized: student name at departure time.
    /// </summary>
    public string? StudentName { get; set; }

    /// <summary>
    /// Denormalized: student code at departure time.
    /// </summary>
    public string? StudentCode { get; set; }

    // ══════════════════════════════════════════════
    // DEPARTURE CALCULATION (REQ-PAY-068-074)
    // ══════════════════════════════════════════════

    /// <summary>
    /// Payment status at the time of departure (Paid, PartiallyPaid, Unpaid).
    /// REQ-PAY-072: Displayed on departure summary.
    /// </summary>
    public PaymentStatus PaymentStatusAtDeparture { get; set; }

    /// <summary>
    /// Total occurrences scheduled in the current payment period.
    /// REQ-PAY-074: Based on session occurrence count, not calendar days.
    /// </summary>
    public int TotalOccurrencesInPeriod { get; set; }

    /// <summary>
    /// Number of occurrences the student actually attended.
    /// BR-PAY-007: Unrecorded occurrences excluded from calculation.
    /// </summary>
    public int AttendedOccurrences { get; set; }

    /// <summary>
    /// Full period payment amount before pro-rating.
    /// REQ-PAY-072: Displayed in departure summary.
    /// </summary>
    [Column(TypeName = "decimal(10,2)")]
    public decimal FullPeriodAmount { get; set; }

    /// <summary>
    /// System-calculated pro-rated amount.
    /// REQ-PAY-068: (Attended ÷ Total) × Full Amount.
    /// </summary>
    [Column(TypeName = "decimal(10,2)")]
    public decimal ProRatedAmount { get; set; }

    /// <summary>
    /// Final amount applied (may differ from ProRatedAmount if tutor overrides).
    /// REQ-PAY-075: Custom refund/charge amount.
    /// </summary>
    [Column(TypeName = "decimal(10,2)")]
    public decimal FinalAmount { get; set; }

    /// <summary>
    /// Whether the tutor overrode the system-calculated amount.
    /// REQ-PAY-075: Override logged for transparency.
    /// </summary>
    public bool IsTutorOverride { get; set; } = false;

    /// <summary>
    /// The original system-calculated amount before override.
    /// REQ-PAY-075: Stored alongside override for transparency.
    /// </summary>
    [Column(TypeName = "decimal(10,2)")]
    public decimal OriginalCalculatedAmount { get; set; }

    /// <summary>
    /// The financial outcome: RefundDue, AmountOwed, or NoObligation.
    /// REQ-PAY-069/070/071: Determines what action follows departure.
    /// </summary>
    public DepartureOutcome DepartureOutcome { get; set; }

    /// <summary>
    /// The user who confirmed the departure.
    /// REQ-PAY-066: Departure initiated from student profile or session detail.
    /// </summary>
    public long? ConfirmedByUserId { get; set; }

    /// <summary>
    /// UTC timestamp when the departure was confirmed.
    /// REQ-PAY-073: Date recorded in student's payment history.
    /// </summary>
    public DateTime DepartedAt { get; set; }

    // ══════════════════════════════════════════════
    // REFUND ATTRIBUTION (surface the refund on the collections ledger + per-collector totals)
    // ══════════════════════════════════════════════

    /// <summary>
    /// The collector (an assistant OR the tutor) whose collected cash was returned on a RefundDue
    /// departure — the latest collector for this student+session. Null when nobody collected (never
    /// a refund) or for historical rows recorded before this was captured. Attributes the refund as
    /// a negative against that collector's collected total and ledger.
    /// </summary>
    public long? CollectedByUserId { get; set; }

    /// <summary>
    /// First day of the anchored payment period the refund applies to — the "departed month" shown
    /// on the negative collections-ledger line. Null for AmountOwed/NoObligation or historical rows.
    /// </summary>
    public DateTime? RefundPeriodStart { get; set; }

    // ══════════════════════════════════════════════
    // DEPARTURE STORY (the card explains the settled figure, REQ-PAY-072/075)
    // ══════════════════════════════════════════════

    /// <summary>
    /// First day of the month the whole calculation was anchored on — by construction the LAST month
    /// the student actually paid for (see GetDepartureSummaryAsync), or the teacher-local current
    /// month when they never paid in this session.
    ///
    /// Distinct from <see cref="RefundPeriodStart"/>, which is stamped ONLY on a non-zero refund and
    /// therefore vanishes exactly when the tutor waives the refund — the case the departure card most
    /// needs to explain. Always stamped. Null only on rows written before this shipped.
    /// </summary>
    public DateTime? AnchorPeriodStart { get; set; }

    /// <summary>
    /// Cash the student had actually paid for the anchored month at the moment of departure.
    ///
    /// Must be SNAPSHOTTED: <c>ReverseDeparturePeriodAsync</c> decrements the period's AmountPaid as
    /// part of the refund, so this figure is unreconstructable afterwards. Null on historical rows.
    /// </summary>
    [Column(TypeName = "decimal(10,2)")]
    public decimal? PaidAmountAtDeparture { get; set; }

    // ══════════════════════════════════════════════
    // AMOUNT CORRECTION (REQ-PAY-075 — fixing a figure settled by mistake, 2026-09-15)
    // ══════════════════════════════════════════════

    /// <summary>
    /// <see cref="FinalAmount"/> as it stood BEFORE the most recent correction, so the departed-students
    /// card can say "was 300, now 190" and the tutor can see what changed. Null while a departure has
    /// never been corrected. Only the LATEST correction is kept here — the full trail lives on
    /// <c>PaymentEditLogs</c>, which is where money history belongs.
    /// </summary>
    [Column(TypeName = "decimal(10,2)")]
    public decimal? AmountBeforeEdit { get; set; }

    /// <summary>The tutor who corrected the figure. Corrections are tutor-only (BR-PAY-002).</summary>
    public long? AmountEditedByUserId { get; set; }

    /// <summary>UTC instant of the most recent correction. Null when never corrected.</summary>
    public DateTime? AmountEditedAt { get; set; }

    /// <summary>
    /// Why the figure was changed, in the tutor's own words. REQUIRED by the service on every
    /// correction: a settled amount silently becoming a different settled amount is exactly the kind
    /// of money change that has to carry a reason.
    /// </summary>
    public string? AmountEditNote { get; set; }
}