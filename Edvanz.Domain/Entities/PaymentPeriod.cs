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
    /// Current payment status derived from AmountDue vs AmountPaid.
    /// Maintained by the service layer for O(1) status lookup.
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