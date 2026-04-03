using Edvanz.Application.Dtos.Payment;

namespace Edvanz.Application.ServiceContract;

/// <summary>
/// Defines the contract for exporting payment reports to PDF or Excel files.
/// Infrastructure layer concern — uses ClosedXML/QuestPDF or similar.
/// Same pattern as IAttendanceReportExportService.
///
/// REQ-PAY-050: Reports exportable in PDF or Excel (.xlsx) format.
/// REQ-PAY-051: Completes within 5 seconds for 50K students × full year.
/// REQ-EVT-025: Event reports also exportable.
/// </summary>
public interface IPaymentReportExportService
{
    /// <summary>
    /// Exports payment report data to the specified format.
    /// </summary>
    /// <param name="header">Standard report header information.</param>
    /// <param name="reportData">The report data (type varies by report type).</param>
    /// <param name="format">"pdf" or "xlsx".</param>
    /// <returns>Byte array of the generated file.</returns>
    Task<byte[]> ExportPaymentReportAsync(
        PaymentReportHeaderDto header, object reportData, string format);

    /// <summary>
    /// Exports event payment report data to the specified format.
    /// </summary>
    Task<byte[]> ExportEventReportAsync(
        PaymentReportHeaderDto header, object reportData, string format);
}