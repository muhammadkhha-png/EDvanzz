using Edvanz.Application.Dtos;
using Edvanz.Application.Dtos.ExamHomework;

namespace Edvanz.Application.ServiceContract;

/// <summary>
/// Application-layer contract for the Module 6 reports (REQ-EXH-038 through 046).
/// Kept separate from <see cref="IExamHomeworkService"/> per Single-Responsibility:
/// reports are read-only document generation, the main service handles state transitions.
///
/// All methods return <see cref="Result{T}"/> wrapping a <see cref="ReportFile"/> on success.
/// REQ-EXH-038: Render budget is &lt; 5 seconds for 50K rows. Synchronous response.
/// REQ-EXH-NFR-004: All queries multi-tenant scoped to teacherId from JWT.
/// </summary>
public interface IExamHomeworkReportService
{
    /// <summary>REQ-EXH-039: Single Assignment Report.</summary>
    Task<Result<ReportFile>> GenerateSingleAssignmentAsync(
        long teacherId, SingleAssignmentReportDto dto, ReportFormat format);

    /// <summary>REQ-EXH-040: Single Student Assignment History Report.</summary>
    Task<Result<ReportFile>> GenerateStudentHistoryAsync(
        long teacherId, StudentHistoryReportDto dto, ReportFormat format);

    /// <summary>REQ-EXH-041: Homework Absence Report.</summary>
    Task<Result<ReportFile>> GenerateHomeworkAbsenceAsync(
        long teacherId, HomeworkAbsenceReportDto dto, ReportFormat format);

    /// <summary>REQ-EXH-042: Exam Absence Report.</summary>
    Task<Result<ReportFile>> GenerateExamAbsenceAsync(
        long teacherId, ExamAbsenceReportDto dto, ReportFormat format);

    /// <summary>REQ-EXH-043: Grades Analysis Report.</summary>
    Task<Result<ReportFile>> GenerateGradesAnalysisAsync(
        long teacherId, GradesAnalysisReportDto dto, ReportFormat format);

    /// <summary>REQ-EXH-044: Below Passing Grade Report.</summary>
    Task<Result<ReportFile>> GenerateBelowPassingAsync(
        long teacherId, BelowPassingReportDto dto, ReportFormat format);

    /// <summary>REQ-EXH-045: Above Grade Threshold Report.</summary>
    Task<Result<ReportFile>> GenerateAboveThresholdAsync(
        long teacherId, AboveThresholdReportDto dto, ReportFormat format);

    /// <summary>REQ-EXH-046: Session/Group Assignment Summary Report.</summary>
    Task<Result<ReportFile>> GenerateSessionGroupSummaryAsync(
        long teacherId, SessionGroupSummaryReportDto dto, ReportFormat format);
}
