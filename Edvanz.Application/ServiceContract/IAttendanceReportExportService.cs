using Edvanz.Application.Dtos.Attendance;

namespace Edvanz.Application.ServiceContract;

/// <summary>
/// Defines the contract for exporting attendance reports and student timelines
/// as downloadable files (PDF or Excel).
/// REQ-ATT-041: All reports exportable as PDF or Excel (.xlsx).
/// REQ-ATT-081: Student attendance timeline exportable as PDF or Excel.
///
/// Lives in the Application layer (interface only). Implementation in Infrastructure
/// uses third-party libraries (e.g., ClosedXML for Excel, QuestPDF for PDF).
/// </summary>
public interface IAttendanceReportExportService
{
    /// <summary>
    /// Exports attendance report records to an Excel (.xlsx) file.
    /// REQ-ATT-041: Exportable as Excel.
    /// REQ-ATT-042: Must complete within 5 seconds for up to 50K students.
    /// </summary>
    /// <param name="records">The attendance records to include in the report.</param>
    /// <param name="reportType">The report type (determines column layout and grouping).</param>
    /// <returns>Byte array containing the .xlsx file content.</returns>
    Task<byte[]> ExportReportToExcelAsync(
        List<AttendanceRecordDto> records,
        AttendanceReportType reportType);

    /// <summary>
    /// Exports attendance report records to a PDF file.
    /// REQ-ATT-041: Exportable as PDF.
    /// REQ-ATT-042: Must complete within 5 seconds for up to 50K students.
    /// </summary>
    /// <param name="records">The attendance records to include in the report.</param>
    /// <param name="reportType">The report type (determines layout and grouping).</param>
    /// <returns>Byte array containing the PDF file content.</returns>
    Task<byte[]> ExportReportToPdfAsync(
        List<AttendanceRecordDto> records,
        AttendanceReportType reportType);

    /// <summary>
    /// Exports a student's attendance timeline to an Excel (.xlsx) file.
    /// REQ-ATT-081: Preserves month-by-month structure with summary totals.
    /// </summary>
    /// <param name="summary">The student's all-time attendance summary.</param>
    /// <param name="months">Monthly breakdown data for the date range.</param>
    /// <returns>Byte array containing the .xlsx file content.</returns>
    Task<byte[]> ExportTimelineToExcelAsync(
        StudentAttendanceSummaryDto summary,
        List<MonthlyAttendanceSummaryDto> months);

    /// <summary>
    /// Exports a student's attendance timeline to a PDF file.
    /// REQ-ATT-081: Preserves month-by-month structure with summary totals.
    /// </summary>
    /// <param name="summary">The student's all-time attendance summary.</param>
    /// <param name="months">Monthly breakdown data for the date range.</param>
    /// <returns>Byte array containing the PDF file content.</returns>
    Task<byte[]> ExportTimelineToPdfAsync(
        StudentAttendanceSummaryDto summary,
        List<MonthlyAttendanceSummaryDto> months);
}