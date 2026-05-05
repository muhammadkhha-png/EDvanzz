using Edvanz.Domain.Enums;

namespace Edvanz.Application.Dtos.ExamHomework;

/// <summary>
/// Output format for a generated report.
/// </summary>
public enum ReportFormat : byte
{
    Pdf = 0,
    Xlsx = 1
}

/// <summary>
/// Generated report binary + metadata. Returned wrapped in <see cref="Result{T}"/>
/// so the controller can map success → File() and failure → ToResponse().
/// </summary>
public sealed record ReportFile(byte[] Content, string FileName, string ContentType);

/// <summary>
/// Sort direction for grade-based reports.
/// </summary>
public enum GradeSortDirection : byte
{
    Asc = 0,
    Desc = 1
}

// ════════════════════════════════════════════════════════════════════════════
// REPORT REQUEST DTOs
// ════════════════════════════════════════════════════════════════════════════

/// <summary>REQ-EXH-039: Single Assignment Report.</summary>
public class SingleAssignmentReportDto
{
    public long OccurrenceId { get; set; }
}

/// <summary>REQ-EXH-040: Single Student Assignment History Report.</summary>
public class StudentHistoryReportDto
{
    public long TeacherStudentId { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public AssignmentType? AssignmentType { get; set; }
}

/// <summary>
/// REQ-EXH-041: Homework Absence Report.
/// Validation: exactly one of the three filter fields must be populated.
/// </summary>
public class HomeworkAbsenceReportDto
{
    public long? SessionId { get; set; }
    public long? SessionGroupId { get; set; }
    public long? AssignmentTemplateId { get; set; }
}

/// <summary>
/// REQ-EXH-042: Exam Absence Report. Same shape as homework variant but for exams.
/// </summary>
public class ExamAbsenceReportDto
{
    public long? SessionId { get; set; }
    public long? SessionGroupId { get; set; }
    public long? AssignmentTemplateId { get; set; }
}

/// <summary>REQ-EXH-043: Grades Analysis Report.</summary>
public class GradesAnalysisReportDto
{
    public long OccurrenceId { get; set; }
    public GradeSortDirection SortBy { get; set; } = GradeSortDirection.Desc;
}

/// <summary>
/// REQ-EXH-044: Below Passing Grade Report.
/// Validation: exactly one of the three filter fields must be populated.
/// </summary>
public class BelowPassingReportDto
{
    public long? OccurrenceId { get; set; }
    public long? SessionId { get; set; }
    public long? SessionGroupId { get; set; }
}

/// <summary>REQ-EXH-045: Above Grade Threshold Report.</summary>
public class AboveThresholdReportDto
{
    public long OccurrenceId { get; set; }
    public decimal GradeThreshold { get; set; }
}

/// <summary>
/// REQ-EXH-046: Session/Group Assignment Summary Report.
/// Validation: exactly one of SessionId / SessionGroupId.
/// </summary>
public class SessionGroupSummaryReportDto
{
    public long? SessionId { get; set; }
    public long? SessionGroupId { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
}
