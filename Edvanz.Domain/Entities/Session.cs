using Edvanz.Domain.Entities.ShareProp;
using Edvanz.Domain.Enums;
using System.ComponentModel.DataAnnotations.Schema;

namespace Edvanz.Domain.Entities;

/// <summary>
/// Represents a tutoring session created and managed by a Teacher.
/// REQ-SES-001: Each tutor can create unlimited sessions.
/// REQ-SES-006: Defines all configurable session fields.
/// REQ-SES-NFR-004: All session data is scoped to the individual tutor account via TeacherId.
/// REQ-SES-041: Hard delete — no soft-delete or recycle bin for sessions.
/// 
/// Multi-tenant isolation: TeacherId is the tenant key. All queries must scope by TeacherId.
/// </summary>
public class Session : BaseEntity
{
    /// <summary>
    /// Foreign key to the owning Teacher. All session data is scoped to this teacher.
    /// REQ-SES-NFR-004: Not visible or accessible by other tutor accounts.
    /// </summary>
    [ForeignKey(nameof(Teacher))]
    public long TeacherId { get; set; }
    public Teacher Teacher { get; set; } = null!;

    /// <summary>
    /// Display name of the session. Manual or auto-generated.
    /// REQ-SES-002: Manual entry or auto-generated (Session A1, A2 … B1, B2 …).
    /// REQ-SES-003: Supports both Arabic and English input.
    /// REQ-SES-005: Editable after creation.
    /// BR-SES-001: Must be unique within the same tutor account.
    /// </summary>
    public string SessionName { get; set; } = null!;

    /// <summary>
    /// Recurrence pattern of the session.
    /// REQ-SES-007: Weekly, BiWeekly, or Monthly.
    /// REQ-SES-009: Not editable after save if session has active student assignments or links.
    /// </summary>
    public OccurrenceType OccurrenceType { get; set; }

    /// <summary>
    /// Comma-separated day-of-week indices for Weekly/BiWeekly occurrence types.
    /// Format: "0,3,5" where 0=Saturday, 1=Sunday, 2=Monday, 3=Tuesday, 4=Wednesday, 5=Thursday, 6=Friday.
    /// REQ-SES-008: At least one day required for Weekly/BiWeekly.
    /// Null for Monthly occurrence type.
    /// 
    /// Stored as a compact string rather than a child table because:
    /// - Maximum 7 values (bounded set)
    /// - Avoids N+1 joins on every session query
    /// - Parsed by the application layer for display and validation
    /// </summary>
    public string? SelectedDays { get; set; }

    /// <summary>
    /// For Monthly occurrence only: the specific day of the month (1–31).
    /// REQ-SES-007: The tutor defines the day pattern for monthly recurrence.
    /// Null for Weekly/BiWeekly occurrence types.
    /// </summary>
    public byte? MonthlyDayOfMonth { get; set; }

    /// <summary>
    /// How students are charged for this session.
    /// REQ-SES-010: Monthly (per calendar month) or PerSession (per attendance).
    /// REQ-SES-012: Editable after creation.
    /// </summary>
    public PaymentType PaymentType { get; set; }

    /// <summary>
    /// Monetary value in Egyptian Pounds (EGP).
    /// REQ-SES-011: Corresponds to the selected payment type.
    /// REQ-SES-012: Editable after creation.
    /// </summary>
    [Column(TypeName = "decimal(10,2)")]
    public decimal SessionAmount { get; set; }

    /// <summary>
    /// Start of the active date range.
    /// REQ-SES-013: Defines when the session begins.
    /// </summary>
    [Column(TypeName = "date")]
    public DateTime StartDate { get; set; }

    /// <summary>
    /// End of the active date range.
    /// REQ-SES-013: Defines when the session ends.
    /// REQ-SES-014: Must be strictly after StartDate.
    /// REQ-SES-015: Sessions past this date are marked Completed/Expired.
    /// </summary>
    [Column(TypeName = "date")]
    public DateTime EndDate { get; set; }

    /// <summary>
    /// Time the session starts each occurrence.
    /// REQ-SES-006: Mandatory field.
    /// </summary>
    public TimeSpan StartTime { get; set; }

    /// <summary>
    /// Duration of each session occurrence in minutes.
    /// REQ-SES-006: Mandatory field.
    /// Stored as smallint for efficient arithmetic (end time = StartTime + DurationMinutes).
    /// </summary>
    public short DurationMinutes { get; set; }

    /// <summary>
    /// Optional FK to the SessionGroup this session belongs to.
    /// REQ-SES-026: Null = ungrouped (appears in ungrouped area of session list).
    /// REQ-SES-030: Movable between groups or removable from group at any time.
    /// REQ-SES-031: Set to null when the group is deleted (sessions become ungrouped).
    /// </summary>
    [ForeignKey(nameof(SessionGroup))]
    public long? SessionGroupId { get; set; }
    public SessionGroup? SessionGroup { get; set; }

    /// <summary>
    /// Navigation property: students assigned to this session.
    /// REQ-SES-021: Student count derived from this collection.
    /// REQ-SES-042: When session is deleted, students' SessionId is set to null.
    /// </summary>
    public ICollection<TeacherStudent> TeacherStudents { get; set; } = new List<TeacherStudent>();

    /// <summary>
    /// Navigation property: membership links where this session is the "left" side (lower Id).
    /// REQ-SES-032: Session linking/membership feature.
    /// </summary>
    public ICollection<SessionLink> SessionLinksAsSource { get; set; } = new List<SessionLink>();

    /// <summary>
    /// Navigation property: membership links where this session is the "right" side (higher Id).
    /// REQ-SES-036: Symmetric linking — both directions are navigable.
    /// </summary>
    public ICollection<SessionLink> SessionLinksAsTarget { get; set; } = new List<SessionLink>();
}