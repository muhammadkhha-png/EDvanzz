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

    // Navigation properties
    public ICollection<EventStudentObligation> StudentObligations { get; set; } = new List<EventStudentObligation>();
    public ICollection<EventPaymentTransaction> PaymentTransactions { get; set; } = new List<EventPaymentTransaction>();
}