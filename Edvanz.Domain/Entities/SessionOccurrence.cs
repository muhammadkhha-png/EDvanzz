using Edvanz.Domain.Entities.ShareProp;
using System.ComponentModel.DataAnnotations.Schema;

namespace Edvanz.Domain.Entities;

/// <summary>
/// Represents a single materialized occurrence date for a session.
/// Each session's recurrence schedule (Weekly/BiWeekly/Monthly) is expanded into
/// individual rows — one per occurrence date — so attendance records can reference
/// a concrete, indexed row rather than requiring runtime recurrence computation.
/// 
/// REQ-ATT-001/002: Determines if today matches a scheduled occurrence.
/// REQ-ATT-005: Only occurrences within the session's StartDate–EndDate are generated.
/// REQ-ATT-065: Calendar view uses these rows for color-coded date indicators.
/// 
/// PERFORMANCE: This table enables O(1) lookups for "does session X occur on date Y?"
/// and efficient "previous occurrence" navigation via OccurrenceIndex.
/// At ~100 occurrences per session, storage is minimal even at scale.
/// 
/// LIFECYCLE:
/// - Generated in bulk when a session is created.
/// - Regenerated (delete + recreate) if the session's date range or occurrence config changes.
/// - Cascade-deleted when the session is hard-deleted (REQ-SES-041).
/// 
/// Multi-tenant isolation: inherited from the parent Session's TeacherId scope.
/// </summary>
public class SessionOccurrence : BaseEntity
{
    /// <summary>
    /// Foreign key to the session this occurrence belongs to.
    /// CASCADE delete: occurrences are removed when the session is hard-deleted.
    /// </summary>
    [ForeignKey(nameof(Session))]
    public long SessionId { get; set; }
    public Session Session { get; set; } = null!;

    /// <summary>
    /// The specific date this session occurs on.
    /// Stored as date-only (no time component) — time is on the Session entity.
    /// REQ-ATT-001: Compared against today's date for attendance eligibility.
    /// </summary>
    [Column(TypeName = "date")]
    public DateTime OccurrenceDate { get; set; }

    /// <summary>
    /// Zero-based sequential index within the session's full occurrence schedule.
    /// Enables efficient "previous occurrence" lookup:
    ///   WHERE SessionId = @sid AND OccurrenceIndex = @currentIndex - 1
    /// instead of ORDER BY OccurrenceDate DESC OFFSET 1.
    /// REQ-ATT-027: Check the student's attendance for the immediately preceding occurrence.
    /// </summary>
    public int OccurrenceIndex { get; set; }
}