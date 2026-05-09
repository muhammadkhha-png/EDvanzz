namespace Edvanz.Domain.Enums;

/// <summary>
/// Sortable columns on the teacher analytics report
/// (<c>GET /api/videos/{id}/analytics</c>, Story F).
///
/// Maps to the <c>sortBy</c> query parameter; bound to a JSON-string converter
/// at the controller boundary so clients send <c>"StudentName"</c> rather than
/// <c>0</c>.
///
/// Numeric values are NOT a persisted contract — these are query-parameter
/// values only. Adding new sort options is safe; reordering is fine too.
/// </summary>
public enum VideoAnalyticsSortBy
{
    /// <summary>
    /// Default sort. Alphabetical by <c>TeacherStudent.StudentName</c>.
    /// </summary>
    StudentName = 0,

    /// <summary>How many times the student opened the video.</summary>
    OpenCount = 1,

    /// <summary>Cumulative watch time across all sessions and devices.</summary>
    TotalWatchSeconds = 2,

    /// <summary>Capped 0-100 completion percentage.</summary>
    EstimatedCompletionPct = 3,

    /// <summary>Most recent Open or Stop event timestamp.</summary>
    LastOpenedAt = 4
}
