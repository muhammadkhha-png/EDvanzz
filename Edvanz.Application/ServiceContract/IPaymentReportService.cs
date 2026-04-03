using Edvanz.Application.Dtos;
using Edvanz.Application.Dtos.Payment;

namespace Edvanz.Application.ServiceContract;

/// <summary>
/// Defines the contract for Payment Report generation (Module 4 reporting).
/// REQ-PAY-048 through REQ-PAY-065: Ten report types with filtering, sorting, and export.
///
/// All methods return Result&lt;T&gt; for consistent error handling.
/// All methods are async per system architecture requirements.
/// </summary>
public interface IPaymentReportService
{
    /// <summary>
    /// Generates a payment report based on the specified type and parameters.
    /// REQ-PAY-048: Ten report types, all accessible from Reports section.
    /// REQ-PAY-049: Standard header on all reports.
    /// REQ-PAY-051: Completes within 5 seconds for 50K students × full year.
    /// </summary>
    Task<Result<object>> GenerateReportAsync(
        long teacherId, PaymentReportRequestDto request);

    /// <summary>
    /// Exports a payment report as a downloadable file (PDF or Excel).
    /// REQ-PAY-050: Exportable in PDF or Excel (.xlsx) format.
    /// </summary>
    Task<Result<byte[]>> ExportReportAsync(
        long teacherId, PaymentReportRequestDto request, string format);
}