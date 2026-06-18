using Edvanz.Domain.Entities.ShareProp;
using Edvanz.Domain.Enums;
using System.ComponentModel.DataAnnotations.Schema;

namespace Edvanz.Domain.Entities;

/// <summary>
/// Materializes each real occurrence date for a session based on its recurrence rules.
/// REQ-ATT-001/002: Determines attendance eligibility by matching today's date against occurrences.
/// REQ-ATT-003: Enables "Today's Session" badge by querying occurrences for today's date.
/// REQ-ATT-005: Respects session StartDate/EndDate boundaries.
/// REQ-ATT-049: Dashboard daily summary — count occurrences for today per teacher.
/// REQ-ATT-065: Edit Attendance calendar — lists all occurrence dates with color indicators.
///
/// PERFORMANCE DECISION:
/// Pre-generating occurrence rows avoids runtime recurrence computation on every request.
/// With 50K concurrent users, computing "does this session occur today?" from OccurrenceType/SelectedDays
/// would be un-indexable and catastrophic at scale. This table makes it a simple indexed date lookup.
///
/// Multi-tenant isolation: TeacherId is stored directly (not derived via Session join)
/// to enable tenant-scoped composite indexes without cross-table joins.
/// </summary>
public class SessionOccurrence : BaseEntity
{
    /// <summary>
    /// Foreign key to the owning Teacher. Stored directly for tenant-scoped index performance.
    /// REQ-ATT-NFR-003: All attendance data scoped to individual tutor account.
    /// </summary>
    [ForeignKey(nameof(Teacher))]
    public long TeacherId { get; set; }
    public Teacher Teacher { get; set; } = null!;

    /// <summary>
    /// Foreign key to the Session this occurrence belongs to.
    /// NoAction-deleted when the session is hard-deleted (BR-SES-004).
    /// </summary>
    [ForeignKey(nameof(Session))]
    public long SessionId { get; set; }
    public Session Session { get; set; } = null!;

    /// <summary>
    /// The specific calendar date of this session occurrence.
    /// REQ-ATT-001: Compared against today's date for eligibility.
    /// REQ-ATT-NFR-004: Date precision stored as date type.
    /// </summary>
    [Column(TypeName = "date")]
    public DateTime OccurrenceDate { get; set; }

    /// <summary>
    /// Tracks attendance-taking progress for this occurrence.
    /// REQ-ATT-049: Dashboard shows completed/pending counts.
    /// REQ-ATT-051: Color-coded session cards (green/amber/red/grey).
    /// Updated as students are marked — avoids counting AttendanceRecords per query.
    /// </summary>
    public OccurrenceStatus Status { get; set; } = OccurrenceStatus.Pending;

    // Navigation property
    public ICollection<AttendanceRecord> AttendanceRecords { get; set; } = new List<AttendanceRecord>();
}