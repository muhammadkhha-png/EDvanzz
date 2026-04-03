using Edvanz.Application.Dtos.Payment;
using Edvanz.Application.ServiceContract;

namespace Edvanz.Infrastructure.Services;

/// <summary>
/// Stub implementation of IPaymentReportExportService.
/// Replace with ClosedXML (Excel) and QuestPDF (PDF) when those packages are added.
/// Same stub pattern as AttendanceReportExportService.
///
/// REQ-PAY-050: Reports exportable in PDF or Excel (.xlsx) format.
/// REQ-EVT-025: Event reports exportable in PDF or Excel format.
/// REQ-PAY-051: Completes within 5 seconds for 50K students × full year.
/// </summary>
public class PaymentReportExportService : IPaymentReportExportService
{
    /// <inheritdoc />
    public async Task<byte[]> ExportPaymentReportAsync(
        PaymentReportHeaderDto header, object reportData, string format)
    {
        // TODO: Implement with ClosedXML for xlsx and QuestPDF for pdf.
        // For now, return an empty file with a placeholder message.
        var message = $"Payment Report Export Placeholder\n" +
                      $"Report: {header.ReportTitle}\n" +
                      $"Generated: {header.GeneratedAt:yyyy-MM-dd HH:mm:ss}\n" +
                      $"Format: {format}\n" +
                      $"Tutor: {header.TutorAccountName}\n";

        await Task.CompletedTask;
        return System.Text.Encoding.UTF8.GetBytes(message);
    }

    /// <inheritdoc />
    public async Task<byte[]> ExportEventReportAsync(
        PaymentReportHeaderDto header, object reportData, string format)
    {
        // TODO: Implement with ClosedXML for xlsx and QuestPDF for pdf.
        var message = $"Event Report Export Placeholder\n" +
                      $"Report: {header.ReportTitle}\n" +
                      $"Generated: {header.GeneratedAt:yyyy-MM-dd HH:mm:ss}\n" +
                      $"Format: {format}\n" +
                      $"Tutor: {header.TutorAccountName}\n";

        await Task.CompletedTask;
        return System.Text.Encoding.UTF8.GetBytes(message);
    }
}