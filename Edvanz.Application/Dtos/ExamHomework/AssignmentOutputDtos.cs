using Edvanz.Domain.Enums;

namespace Edvanz.Application.Dtos.ExamHomework;

/// <summary>
/// Hydrated scope row for output. The display fields are populated when the scope
/// resolves to a known target.
/// </summary>
public class ScopeOutputDto
{
    public long Id { get; set; }
    public AssignmentScopeType ScopeType { get; set; }
    public long? TeacherStudentId { get; set; }
    public string? StudentName { get; set; }
    public string? StudentCode { get; set; }
    public long? SessionId { get; set; }
    public string? SessionName { get; set; }
    public long? SessionGroupId { get; set; }
    public string? SessionGroupName { get; set; }
}

/// <summary>
/// Full template payload returned by create/get/update endpoints.
/// </summary>
public class AssignmentTemplateDto
{
    public long Id { get; set; }
    public AssignmentType AssignmentType { get; set; }
    public string Name { get; set; } = null!;
    public string NameAr { get; set; } = null!;
    public string? Notes { get; set; }
    public bool IsRecurring { get; set; }
    public RecurrencePattern RecurrencePattern { get; set; }
    public DateTime? RecurrenceEndDate { get; set; }
    public bool IsRecurrenceStopped { get; set; }
    public HomeworkTrackingMode? TrackingMode { get; set; }
    public decimal? MaxGrade { get; set; }
    public decimal? PassingThreshold { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public byte[] RowVersion { get; set; } = null!;

    /// <summary>Populated on create response — Id of the synchronously materialized first occurrence.</summary>
    public long? FirstOccurrenceId { get; set; }

    /// <summary>Populated on create response — number of obligations created at materialization time.</summary>
    public int? StudentsAssigned { get; set; }

    public List<ScopeOutputDto> Scopes { get; set; } = new();
}

/// <summary>
/// Per-row aggregate counts used in the Assignment Overview (REQ-EXH-033).
/// </summary>
public class CompletionSummaryDto
{
    public int TotalStudents { get; set; }
    public int DoneOrAttended { get; set; }
    public int NotDoneOrAbsent { get; set; }
    public int Pending { get; set; }
}

/// <summary>
/// One row of the Assignment Overview list (REQ-EXH-033).
/// </summary>
public class AssignmentOverviewItemDto
{
    public long Id { get; set; }
    public AssignmentType AssignmentType { get; set; }
    public string Name { get; set; } = null!;
    public string NameAr { get; set; } = null!;
    public bool IsRecurring { get; set; }
    public RecurrencePattern RecurrencePattern { get; set; }
    public bool IsRecurrenceStopped { get; set; }

    /// <summary>
    /// Date of the next future occurrence; falls back to the last past occurrence
    /// when no future one exists (e.g., one-time templates that already passed).
    /// </summary>
    public DateTime? NextOrLastOccurrenceDate { get; set; }

    /// <summary>
    /// Human-readable scope summary, e.g., "3 students · 1 session".
    /// Composed in the service layer to keep the repo query simple.
    /// </summary>
    public string ScopeSummary { get; set; } = string.Empty;

    public CompletionSummaryDto CompletionSummary { get; set; } = new();
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// Compact occurrence row for the per-template occurrences list (REQ-EXH-011).
/// </summary>
public class OccurrenceSummaryItemDto
{
    public long Id { get; set; }
    public int OccurrenceNumber { get; set; }
    public DateTime DueDate { get; set; }
    public string Status { get; set; } = string.Empty;
    public decimal? MaxGradeSnapshot { get; set; }
    public decimal? PassingThresholdSnapshot { get; set; }
    public HomeworkTrackingMode? TrackingModeSnapshot { get; set; }
    public CompletionSummaryDto Totals { get; set; } = new();
}

/// <summary>
/// Full occurrence detail returned by the single-occurrence GET (REQ-EXH-011, 029).
/// Carries the snapshotted grading config the UI uses for the lifetime of this occurrence.
/// </summary>
public class OccurrenceDetailDto
{
    public long Id { get; set; }
    public long TemplateId { get; set; }
    public string TemplateName { get; set; } = null!;
    public string TemplateNameAr { get; set; } = null!;
    public AssignmentType AssignmentType { get; set; }
    public int OccurrenceNumber { get; set; }
    public DateTime DueDate { get; set; }
    public string Status { get; set; } = string.Empty;

    public decimal? MaxGradeSnapshot { get; set; }
    public decimal? PassingThresholdSnapshot { get; set; }
    public HomeworkTrackingMode? TrackingModeSnapshot { get; set; }

    public byte[] RowVersion { get; set; } = null!;
}

/// <summary>
/// Tracking-view header summary (REQ-EXH-029).
/// Single grouped aggregate query — no per-row work.
/// </summary>
public class OccurrenceSummaryDto
{
    public long OccurrenceId { get; set; }
    public int TotalStudents { get; set; }
    public int DoneOrAttended { get; set; }
    public int NotDoneOrAbsent { get; set; }
    public int Pending { get; set; }

    /// <summary>
    /// Status IN (Attended, DoneWithoutGrade) — drives the Grade Entry View badge.
    /// </summary>
    public int GradePending { get; set; }

    /// <summary>
    /// Exam-only count of obligations whose grade is below the snapshotted PassingThreshold.
    /// Null for homework occurrences.
    /// </summary>
    public int? BelowPassingCount { get; set; }
}
