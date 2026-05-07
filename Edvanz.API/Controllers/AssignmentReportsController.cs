using Edvanz.API.Attributes;
using Edvanz.Application.Dtos.ExamHomework;
using Edvanz.Application.IservicesContract;
using Edvanz.Application.ServiceContract;
using Edvanz.Domain.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Edvanz.API.Controllers;

/// <summary>
/// API controller for Module 6 reports (REQ-EXH-038 through 046).
///
/// ROUTING: <c>api/assignmentreports/...</c> — matches existing convention.
///
/// FORMAT NEGOTIATION: Each endpoint accepts a <c>?format=pdf|xlsx</c> query parameter
/// (defaults to <c>pdf</c>). The Accept header is read as a fallback. Unknown values
/// fall back to PDF rather than returning 406 — keeps the UX simple while still
/// honoring explicit Excel requests.
///
/// AUTHORIZATION: All endpoints require <c>"Exams And Homework", "View"</c>. Reports
/// are read-only by design.
///
/// On success, the controller returns the file bytes via <see cref="FileContentResult"/>
/// with the correct content type and a download filename. On failure, it falls through
/// to <c>ToResponse</c> so the standard JSON error shape is returned.
/// </summary>
[Authorize]
public class AssignmentReportsController : ModuleSixApiBaseController
{
    private const string PdfContentType = "application/pdf";
    private const string XlsxContentType =
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    private readonly IExamHomeworkReportService _reports;

    public AssignmentReportsController(
        IExamHomeworkReportService reports,
        ICurrentUserService currentUser,
        IUnitOfWork unitOfWork)
        : base(currentUser, unitOfWork)
    {
        _reports = reports;
    }

    // ══════════════════════════════════════════════════════════════════════════
    // ENDPOINT 27: SINGLE ASSIGNMENT REPORT  (REQ-EXH-039)
    // POST /api/assignmentreports/single-assignment?format=pdf
    // ══════════════════════════════════════════════════════════════════════════
    [HttpPost("single-assignment")]
    [ModulePermission("Exams And Homework", "View")]
    public async Task<IActionResult> GenerateSingleAssignment(
        [FromBody] SingleAssignmentReportDto dto, [FromQuery] string? format = null)
    {
        long? teacherId = await ResolveTeacherIdAsync();
        if (teacherId is null) return TeacherNotResolved();

        var fmt = ResolveFormat(format);
        var result = await _reports.GenerateSingleAssignmentAsync(teacherId.Value, dto, fmt);
        return BuildFileOrErrorResponse(result, fmt);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // ENDPOINT 28: SINGLE STUDENT ASSIGNMENT HISTORY  (REQ-EXH-040)
    // POST /api/assignmentreports/student-history?format=pdf
    // ══════════════════════════════════════════════════════════════════════════
    [HttpPost("student-history")]
    [ModulePermission("Exams And Homework", "View")]
    public async Task<IActionResult> GenerateStudentHistory(
        [FromBody] StudentHistoryReportDto dto, [FromQuery] string? format = null)
    {
        long? teacherId = await ResolveTeacherIdAsync();
        if (teacherId is null) return TeacherNotResolved();

        var fmt = ResolveFormat(format);
        var result = await _reports.GenerateStudentHistoryAsync(teacherId.Value, dto, fmt);
        return BuildFileOrErrorResponse(result, fmt);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // ENDPOINT 29: HOMEWORK ABSENCE REPORT  (REQ-EXH-041)
    // POST /api/assignmentreports/homework-absence?format=pdf
    // ══════════════════════════════════════════════════════════════════════════
    [HttpPost("homework-absence")]
    [ModulePermission("Exams And Homework", "View")]
    public async Task<IActionResult> GenerateHomeworkAbsence(
        [FromBody] HomeworkAbsenceReportDto dto, [FromQuery] string? format = null)
    {
        long? teacherId = await ResolveTeacherIdAsync();
        if (teacherId is null) return TeacherNotResolved();

        var fmt = ResolveFormat(format);
        var result = await _reports.GenerateHomeworkAbsenceAsync(teacherId.Value, dto, fmt);
        return BuildFileOrErrorResponse(result, fmt);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // ENDPOINT 30: EXAM ABSENCE REPORT  (REQ-EXH-042)
    // POST /api/assignmentreports/exam-absence?format=pdf
    // ══════════════════════════════════════════════════════════════════════════
    [HttpPost("exam-absence")]
    [ModulePermission("Exams And Homework", "View")]
    public async Task<IActionResult> GenerateExamAbsence(
        [FromBody] ExamAbsenceReportDto dto, [FromQuery] string? format = null)
    {
        long? teacherId = await ResolveTeacherIdAsync();
        if (teacherId is null) return TeacherNotResolved();

        var fmt = ResolveFormat(format);
        var result = await _reports.GenerateExamAbsenceAsync(teacherId.Value, dto, fmt);
        return BuildFileOrErrorResponse(result, fmt);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // ENDPOINT 31: GRADES ANALYSIS REPORT  (REQ-EXH-043)
    // POST /api/assignmentreports/grades-analysis?format=pdf
    // ══════════════════════════════════════════════════════════════════════════
    [HttpPost("grades-analysis")]
    [ModulePermission("Exams And Homework", "View")]
    public async Task<IActionResult> GenerateGradesAnalysis(
        [FromBody] GradesAnalysisReportDto dto, [FromQuery] string? format = null)
    {
        long? teacherId = await ResolveTeacherIdAsync();
        if (teacherId is null) return TeacherNotResolved();

        var fmt = ResolveFormat(format);
        var result = await _reports.GenerateGradesAnalysisAsync(teacherId.Value, dto, fmt);
        return BuildFileOrErrorResponse(result, fmt);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // ENDPOINT 32: BELOW PASSING GRADE REPORT  (REQ-EXH-044)
    // POST /api/assignmentreports/below-passing?format=pdf
    // ══════════════════════════════════════════════════════════════════════════
    [HttpPost("below-passing")]
    [ModulePermission("Exams And Homework", "View")]
    public async Task<IActionResult> GenerateBelowPassing(
        [FromBody] BelowPassingReportDto dto, [FromQuery] string? format = null)
    {
        long? teacherId = await ResolveTeacherIdAsync();
        if (teacherId is null) return TeacherNotResolved();

        var fmt = ResolveFormat(format);
        var result = await _reports.GenerateBelowPassingAsync(teacherId.Value, dto, fmt);
        return BuildFileOrErrorResponse(result, fmt);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // ENDPOINT 33: ABOVE GRADE THRESHOLD REPORT  (REQ-EXH-045)
    // POST /api/assignmentreports/above-threshold?format=pdf
    // ══════════════════════════════════════════════════════════════════════════
    [HttpPost("above-threshold")]
    [ModulePermission("Exams And Homework", "View")]
    public async Task<IActionResult> GenerateAboveThreshold(
        [FromBody] AboveThresholdReportDto dto, [FromQuery] string? format = null)
    {
        long? teacherId = await ResolveTeacherIdAsync();
        if (teacherId is null) return TeacherNotResolved();

        var fmt = ResolveFormat(format);
        var result = await _reports.GenerateAboveThresholdAsync(teacherId.Value, dto, fmt);
        return BuildFileOrErrorResponse(result, fmt);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // ENDPOINT 34: SESSION/GROUP ASSIGNMENT SUMMARY  (REQ-EXH-046)
    // POST /api/assignmentreports/session-group-summary?format=pdf
    // ══════════════════════════════════════════════════════════════════════════
    [HttpPost("session-group-summary")]
    [ModulePermission("Exams And Homework", "View")]
    public async Task<IActionResult> GenerateSessionGroupSummary(
        [FromBody] SessionGroupSummaryReportDto dto, [FromQuery] string? format = null)
    {
        long? teacherId = await ResolveTeacherIdAsync();
        if (teacherId is null) return TeacherNotResolved();

        var fmt = ResolveFormat(format);
        var result = await _reports.GenerateSessionGroupSummaryAsync(teacherId.Value, dto, fmt);
        return BuildFileOrErrorResponse(result, fmt);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // PRIVATE HELPERS
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Parses the <c>format</c> query parameter, then the Accept header, then defaults
    /// to PDF. Case-insensitive. Unknown values fall back to PDF rather than 406.
    /// </summary>
    private ReportFormat ResolveFormat(string? formatQuery)
    {
        if (!string.IsNullOrWhiteSpace(formatQuery))
        {
            if (string.Equals(formatQuery, "xlsx", StringComparison.OrdinalIgnoreCase)
             || string.Equals(formatQuery, "excel", StringComparison.OrdinalIgnoreCase))
                return ReportFormat.Xlsx;
            return ReportFormat.Pdf;
        }

        // Inspect Accept header.
        var accept = Request.Headers.Accept.ToString();
        if (!string.IsNullOrEmpty(accept) && accept.Contains(XlsxContentType, StringComparison.OrdinalIgnoreCase))
            return ReportFormat.Xlsx;

        return ReportFormat.Pdf;
    }

    /// <summary>
    /// Returns a <see cref="FileContentResult"/> on success, or the standard
    /// <c>ToResponse</c> JSON-error shape on failure. Centralizes the dual-output
    /// behavior so each endpoint stays a one-liner at the controller level.
    /// </summary>
    private IActionResult BuildFileOrErrorResponse(
        Application.Dtos.Result<ReportFile> result, ReportFormat fmt)
    {
        if (!result.IsSuccess || result.Data is null)
            return ToResponse(result);

        var file = result.Data;
        string contentType = fmt == ReportFormat.Xlsx ? XlsxContentType : PdfContentType;
        return File(file.Content, contentType, file.FileName);
    }
}
