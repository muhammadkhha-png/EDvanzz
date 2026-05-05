using Edvanz.Domain.Enums;

namespace Edvanz.Application.Dtos.ExamHomework;

/// <summary>
/// Filter and pagination input for the Assignment Overview list (REQ-EXH-033).
/// Inherits Page/PageSize/Search/SortDirection from <see cref="PaginatedRequest"/>.
/// </summary>
public class AssignmentOverviewRequest : PaginatedRequest
{
    /// <summary>
    /// Optional filter by assignment type (Exam or Homework).
    /// REQ-EXH-033: Filter by assignment type.
    /// </summary>
    public AssignmentType? AssignmentType { get; set; }

    /// <summary>
    /// Optional filter by recurrence pattern.
    /// REQ-EXH-033: Filter by recurrence type.
    /// </summary>
    public RecurrencePattern? RecurrencePattern { get; set; }

    /// <summary>
    /// Optional boolean filter — only recurring (true) or only one-time (false).
    /// </summary>
    public bool? IsRecurring { get; set; }
}

/// <summary>
/// Filter and pagination input for the Assignment Tracking View (REQ-EXH-030/031/032).
/// </summary>
public class TrackingViewRequest : PaginatedRequest
{
    /// <summary>
    /// Filter 1 — exact match on obligation status.
    /// REQ-EXH-031: Filter by completion or attendance status.
    /// </summary>
    public ObligationStatus? Status { get; set; }

    /// <summary>
    /// Filter 2 — show only students with no recorded entry.
    /// REQ-EXH-031: Pending OR NotDone OR DidNotAttend.
    /// </summary>
    public bool? MissingEntries { get; set; }

    /// <summary>
    /// Filter 3 — exam only — grade strictly above the given value.
    /// REQ-EXH-031: Grade-above filter.
    /// </summary>
    public decimal? GradeAbove { get; set; }

    /// <summary>
    /// Filter 4 — exam only — grade strictly below the given value.
    /// REQ-EXH-031: Grade-below filter.
    /// </summary>
    public decimal? GradeBelow { get; set; }

    /// <summary>
    /// Filter 5 — exam only — only students whose grade is below the snapshotted passing threshold.
    /// REQ-EXH-031: Below-passing filter.
    /// </summary>
    public bool? BelowPassing { get; set; }
}

/// <summary>
/// Pagination input for the Grade Entry View (REQ-EXH-026-A).
/// Search matches student name or code.
/// </summary>
public class GradeEntryRequest : PaginatedRequest
{
    // No additional fields beyond PaginatedRequest at this time.
    // Filtering is implicit: only Status IN (Attended, DoneWithoutGrade) rows are returned.
}

/// <summary>
/// Filter and pagination input for the Eligible Students picker (REQ-EXH-035).
/// Lists students NOT already in the template's resolved scope.
/// </summary>
public class EligibleStudentsRequest : PaginatedRequest
{
    /// <summary>
    /// Optional filter — show only students assigned to this session.
    /// </summary>
    public long? SessionId { get; set; }
}
