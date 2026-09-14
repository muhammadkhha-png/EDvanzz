using Edvanz.Domain.Entities.ShareProp;
using Edvanz.Domain.Enums;
using System.ComponentModel.DataAnnotations.Schema;

namespace Edvanz.Domain.Entities;

/// <summary>
/// Represents a one-time payment event created by the tutor.
/// REQ-EVT-001: Independent of regular session payment cycle.
/// REQ-EVT-002: Event Name, Amount, Target Scope, and Date are mandatory.
/// REQ-EVT-008: No recurrence, no monthly cycle — fully standalone.
/// REQ-EVT-022: Hard delete; collected payments survive deletion.
/// BR-EVT-002: Event payments independent of regular payment obligations.
///
/// Multi-tenant isolation: TeacherId stored directly for tenant-scoped indexes.
/// </summary>
public class PaymentEvent : BaseEntity
{
    /// <summary>
    /// Foreign key to the owning Teacher.
    /// REQ-EVT-NFR-001: All event data scoped exclusively to the tutor's account.
    /// </summary>
    [ForeignKey(nameof(Teacher))]
    public long TeacherId { get; set; }
    public Teacher Teacher { get; set; } = null!;

    /// <summary>
    /// Descriptive name for the event (e.g., "Book Purchase — Term 1").
    /// REQ-EVT-002: Mandatory field.
    /// </summary>
    public string EventName { get; set; } = null!;

    /// <summary>
    /// Fixed amount to be collected from each targeted student.
    /// REQ-EVT-002: Mandatory field.
    /// </summary>
    [Column(TypeName = "decimal(10,2)")]
    public decimal EventAmount { get; set; }

    /// <summary>
    /// How the target scope was defined.
    /// REQ-EVT-002/003/004/005/006/007: Individual, Session, Group, or All.
    /// </summary>
    public EventTargetScopeType TargetScopeType { get; set; }

    /// <summary>
    /// Comma-separated IDs of the target scope entities.
    /// For IndividualStudents: student IDs. For Session: session ID.
    /// For SessionGroup: group ID. For AllStudents: null/empty.
    /// Stored as compact string to avoid N+1 joins on event list queries.
    /// </summary>
    public string? TargetScopeIds { get; set; }

    /// <summary>
    /// The date the event was created or becomes active.
    /// REQ-EVT-002: Mandatory field.
    /// </summary>
    [Column(TypeName = "date")]
    public DateTime EventDate { get; set; }

    /// <summary>
    /// Optional free-text notes or description for the event.
    /// REQ-EVT-002: "Notes is an optional free-text field for any additional description or context."
    /// </summary>
    public string? Notes { get; set; }

    // ══════════════════════════════════════════════
    // AGGREGATED COUNTERS (maintained by service layer)
    // ══════════════════════════════════════════════

    /// <summary>
    /// Total number of students the event applies to.
    /// REQ-EVT-014: Displayed in Event Payment Tracking View.
    /// </summary>
    public int TotalStudents { get; set; }

    /// <summary>
    /// Total expected revenue (TotalStudents × EventAmount).
    /// REQ-EVT-014: Pre-calculated for dashboard performance.
    /// </summary>
    [Column(TypeName = "decimal(14,2)")]
    public decimal TotalExpectedRevenue { get; set; }

    /// <summary>
    /// Total collected revenue so far.
    /// REQ-EVT-014: Updated on each payment collection.
    /// </summary>
    [Column(TypeName = "decimal(14,2)")]
    public decimal TotalCollectedRevenue { get; set; } = 0;

    // ══════════════════════════════════════════════
    // SOFT DELETE (REQ-EVT-022)
    // ══════════════════════════════════════════════

    /// <summary>
    /// Soft-delete flag. REQ-EVT-022: Hard delete with no recovery.
    /// Collected payments retained via EventPaymentTransactions.
    /// </summary>
    public bool IsDeleted { get; set; } = false;

    /// <summary>
    /// Timestamp of deletion.
    /// </summary>
    public DateTime? DeletedAt { get; set; }

    // ══════════════════════════════════════════════
    // BOOKS & FEES BEHAVIOUR (added 2026-09-14)
    // ══════════════════════════════════════════════

    /// <summary>
    /// When true, a student later assigned to one of this item's targeted sessions/groups gets an
    /// obligation automatically. When false the item's screen surfaces a "N new students in these
    /// classes · add them?" banner instead, so the drift is visible rather than silent.
    /// Per-item by design — a مذكرة for a class and a one-off رحلة want different answers.
    /// Materialized by <c>MaterializeAutoIncludeAsync</c> once per assign CALL (never per student).
    /// </summary>
    public bool AutoIncludeNewStudents { get; set; } = false;

    /// <summary>
    /// When true, this item's unpaid dues appear on the combined collect sheet while taking
    /// attendance (subject to the teacher-level <c>TeacherConfiguration
    /// .ShowExtrasOnAttendanceScreen</c> switch, which can turn the whole behaviour off).
    /// <para>DB default is <c>false</c> on purpose: items that already existed before this feature
    /// must not start interrupting attendance the moment it deploys. The create path always sends
    /// <c>true</c>, so new items behave as designed.</para>
    /// </summary>
    public bool CollectDuringAttendance { get; set; } = false;

    /// <summary>
    /// Closed = no further collection, but refunds and history still work. This is the escape hatch
    /// for an item that cannot be DELETED because money was collected against it (deleting would
    /// either orphan the cash or destroy the record of it).
    /// </summary>
    public bool IsClosed { get; set; } = false;

    /// <summary>Teacher-local instant the item was closed, in UTC.</summary>
    public DateTime? ClosedAt { get; set; }

    /// <summary>Who soft-deleted the item. Plain column, no FK.</summary>
    public long? DeletedByUserId { get; set; }

    // Navigation properties
    public ICollection<EventStudentObligation> StudentObligations { get; set; } = new List<EventStudentObligation>();
    public ICollection<EventPaymentTransaction> PaymentTransactions { get; set; } = new List<EventPaymentTransaction>();

    /// <summary>The original targeting rules (Session | SessionGroup | AllStudents). See
    /// <see cref="PaymentEventScope"/> for why individually-targeted students are NOT here.</summary>
    public ICollection<PaymentEventScope> Scopes { get; set; } = new List<PaymentEventScope>();
}