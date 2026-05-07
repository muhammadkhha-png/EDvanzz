using System.Net;
using Edvanz.Application.Dtos;
using Edvanz.Application.Dtos.ExamHomework;
using Edvanz.Application.ServiceContract;
using Edvanz.Domain.Interfaces;
using Edvanz.Domain.Resources;
using Microsoft.Extensions.Localization;

namespace Edvanz.Infrastructure.Services;

/// <summary>
/// Stub implementation of <see cref="IExamHomeworkReportService"/> for Module 6 reports.
/// Mirrors the same stub pattern used by <c>PaymentReportExportService</c> and
/// <c>AttendanceReportExportService</c>: the service signs off with a placeholder
/// payload until the real ClosedXML/QuestPDF rendering is wired in.
///
/// The stub still:
///   1. Validates the basic shape of each request (mutually-exclusive filter fields,
///      occurrence-existence checks where cheap), so wrong client calls fail fast.
///   2. Builds a small text-based payload describing the report so the round-trip
///      can be exercised end-to-end during development.
///
/// REPLACE WITH REAL RENDERING when ClosedXML and QuestPDF are added — keep the
/// public contract identical so callers do not change.
/// </summary>
public class ExamHomeworkReportService : IExamHomeworkReportService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IStringLocalizer<Messages> _localizer;

    public ExamHomeworkReportService(
        IUnitOfWork unitOfWork, IStringLocalizer<Messages> localizer)
    {
        _unitOfWork = unitOfWork;
        _localizer = localizer;
    }

    /// <inheritdoc />
    public async Task<Result<ReportFile>> GenerateSingleAssignmentAsync(
        long teacherId, SingleAssignmentReportDto dto, ReportFormat format)
    {
        // Verify the occurrence exists and belongs to the teacher.
        var occurrence = await _unitOfWork.ExamHomeworkRepo
            .GetOccurrenceWithTemplateAsync(dto.OccurrenceId, teacherId);
        if (occurrence is null)
            return Result<ReportFile>.Failure(
                _localizer, "OccurrenceNotFound", HttpStatusCode.NotFound);

        var rows = await _unitOfWork.ExamHomeworkRepo
            .GetObligationsByOccurrenceAsync(teacherId, dto.OccurrenceId);

        var file = BuildPlaceholder(
            "Single Assignment Report",
            $"Occurrence #{occurrence.OccurrenceNumber} • {occurrence.Template.Name}",
            $"Students: {rows.Count}",
            format);

        return Result<ReportFile>.Success(file, _localizer, "ReportGenerated");
    }

    /// <inheritdoc />
    public async Task<Result<ReportFile>> GenerateStudentHistoryAsync(
        long teacherId, StudentHistoryReportDto dto, ReportFormat format)
    {
        var student = await _unitOfWork.Students
            .GetActiveByIdAndTeacherAsync(dto.TeacherStudentId, teacherId);
        if (student is null)
            return Result<ReportFile>.Failure(
                _localizer, "StudentNotFound", HttpStatusCode.NotFound);

        var (rows, total) = await _unitOfWork.ExamHomeworkRepo.GetStudentHistoryPagedAsync(
            teacherId, dto.TeacherStudentId,
            dto.StartDate, dto.EndDate, dto.AssignmentType,
            page: 1, pageSize: int.MaxValue);

        var file = BuildPlaceholder(
            "Student Assignment History",
            $"Student: {student.StudentName} ({student.StudentCode})",
            $"Records: {total}",
            format);

        return Result<ReportFile>.Success(file, _localizer, "ReportGenerated");
    }

    /// <inheritdoc />
    public async Task<Result<ReportFile>> GenerateHomeworkAbsenceAsync(
        long teacherId, HomeworkAbsenceReportDto dto, ReportFormat format)
    {
        if (!HasExactlyOneFilter(dto.SessionId, dto.SessionGroupId, dto.AssignmentTemplateId))
            return Result<ReportFile>.Failure(
                _localizer, "ReportFilterAmbiguous", HttpStatusCode.BadRequest);

        await Task.CompletedTask;
        var file = BuildPlaceholder(
            "Homework Absence Report",
            DescribeFilter(dto.SessionId, dto.SessionGroupId, dto.AssignmentTemplateId),
            "Status: NotDone",
            format);

        return Result<ReportFile>.Success(file, _localizer, "ReportGenerated");
    }

    /// <inheritdoc />
    public async Task<Result<ReportFile>> GenerateExamAbsenceAsync(
        long teacherId, ExamAbsenceReportDto dto, ReportFormat format)
    {
        if (!HasExactlyOneFilter(dto.SessionId, dto.SessionGroupId, dto.AssignmentTemplateId))
            return Result<ReportFile>.Failure(
                _localizer, "ReportFilterAmbiguous", HttpStatusCode.BadRequest);

        await Task.CompletedTask;
        var file = BuildPlaceholder(
            "Exam Absence Report",
            DescribeFilter(dto.SessionId, dto.SessionGroupId, dto.AssignmentTemplateId),
            "Status: DidNotAttend",
            format);

        return Result<ReportFile>.Success(file, _localizer, "ReportGenerated");
    }

    /// <inheritdoc />
    public async Task<Result<ReportFile>> GenerateGradesAnalysisAsync(
        long teacherId, GradesAnalysisReportDto dto, ReportFormat format)
    {
        var occurrence = await _unitOfWork.ExamHomeworkRepo
            .GetOccurrenceWithTemplateAsync(dto.OccurrenceId, teacherId);
        if (occurrence is null)
            return Result<ReportFile>.Failure(
                _localizer, "OccurrenceNotFound", HttpStatusCode.NotFound);

        var file = BuildPlaceholder(
            "Grades Analysis Report",
            $"Occurrence #{occurrence.OccurrenceNumber} • {occurrence.Template.Name}",
            $"Sort: {dto.SortBy}",
            format);

        return Result<ReportFile>.Success(file, _localizer, "ReportGenerated");
    }

    /// <inheritdoc />
    public async Task<Result<ReportFile>> GenerateBelowPassingAsync(
        long teacherId, BelowPassingReportDto dto, ReportFormat format)
    {
        if (!HasExactlyOneFilter(dto.OccurrenceId, dto.SessionId, dto.SessionGroupId))
            return Result<ReportFile>.Failure(
                _localizer, "ReportFilterAmbiguous", HttpStatusCode.BadRequest);

        await Task.CompletedTask;
        var file = BuildPlaceholder(
            "Below Passing Grade Report",
            DescribeFilter(dto.OccurrenceId, dto.SessionId, dto.SessionGroupId),
            "Filter: Grade < PassingThresholdSnapshot",
            format);

        return Result<ReportFile>.Success(file, _localizer, "ReportGenerated");
    }

    /// <inheritdoc />
    public async Task<Result<ReportFile>> GenerateAboveThresholdAsync(
        long teacherId, AboveThresholdReportDto dto, ReportFormat format)
    {
        var occurrence = await _unitOfWork.ExamHomeworkRepo
            .GetOccurrenceWithTemplateAsync(dto.OccurrenceId, teacherId);
        if (occurrence is null)
            return Result<ReportFile>.Failure(
                _localizer, "OccurrenceNotFound", HttpStatusCode.NotFound);

        var rows = await _unitOfWork.ExamHomeworkRepo.GetAboveGradeThresholdAsync(
            teacherId, dto.OccurrenceId, dto.GradeThreshold);

        var file = BuildPlaceholder(
            "Above Grade Threshold Report",
            $"Occurrence #{occurrence.OccurrenceNumber} • {occurrence.Template.Name}",
            $"Threshold: {dto.GradeThreshold} • Students: {rows.Count}",
            format);

        return Result<ReportFile>.Success(file, _localizer, "ReportGenerated");
    }

    /// <inheritdoc />
    public async Task<Result<ReportFile>> GenerateSessionGroupSummaryAsync(
        long teacherId, SessionGroupSummaryReportDto dto, ReportFormat format)
    {
        if (!HasExactlyOneFilter(dto.SessionId, dto.SessionGroupId))
            return Result<ReportFile>.Failure(
                _localizer, "ReportFilterAmbiguous", HttpStatusCode.BadRequest);

        if (dto.EndDate < dto.StartDate)
            return Result<ReportFile>.Failure(
                _localizer, "ReportDateRangeInvalid", HttpStatusCode.BadRequest);

        await Task.CompletedTask;
        var file = BuildPlaceholder(
            "Session/Group Assignment Summary",
            DescribeFilter(dto.SessionId, dto.SessionGroupId),
            $"Range: {dto.StartDate:yyyy-MM-dd} → {dto.EndDate:yyyy-MM-dd}",
            format);

        return Result<ReportFile>.Success(file, _localizer, "ReportGenerated");
    }

    // ══════════════════════════════════════════════════════════════════════
    // PRIVATE HELPERS — placeholder rendering, kept dead-simple until QuestPDF
    // and ClosedXML are wired in. Every public method routes through these.
    // ══════════════════════════════════════════════════════════════════════

    private static bool HasExactlyOneFilter(params long?[] filters) =>
        filters.Count(f => f.HasValue) == 1;

    private static string DescribeFilter(long? a, long? b, long? c = null)
    {
        if (a.HasValue) return $"Filter A id: {a.Value}";
        if (b.HasValue) return $"Filter B id: {b.Value}";
        if (c.HasValue) return $"Filter C id: {c.Value}";
        return "(no filter)";
    }

    private static ReportFile BuildPlaceholder(
        string title, string subtitle, string detail, ReportFormat format)
    {
        string body =
            $"{title}\n" +
            $"{subtitle}\n" +
            $"{detail}\n" +
            $"Generated: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC\n" +
            $"Format: {format}\n" +
            "\n" +
            "[ STUB OUTPUT — replace with real ClosedXML / QuestPDF rendering ]\n";

        byte[] content = System.Text.Encoding.UTF8.GetBytes(body);
        string ext = format == ReportFormat.Xlsx ? "xlsx" : "pdf";
        string fileName = $"{Sanitize(title)}_{DateTime.UtcNow:yyyyMMdd_HHmm}.{ext}";

        return new ReportFile(content, fileName, format == ReportFormat.Xlsx
            ? "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
            : "application/pdf");
    }

    private static string Sanitize(string title) =>
        new string(title.Where(c => char.IsLetterOrDigit(c) || c == '_' || c == '-').ToArray());
}
