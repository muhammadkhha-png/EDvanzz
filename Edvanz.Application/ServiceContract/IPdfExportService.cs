using Edvanz.Application.Reporting;

namespace Edvanz.Application.ServiceContract;

/// <summary>
/// Generic, reusable contract for rendering a tabular dataset to a PDF file. Columns and their
/// order come from [ReportColumn] attributes on <typeparamref name="T"/>; the caller supplies the
/// matching ordered, localized header texts. Implemented in Infrastructure using QuestPDF.
/// </summary>
public interface IPdfExportService
{
    /// <summary>
    /// Renders <paramref name="rows"/> to a PDF byte array.
    /// </summary>
    /// <typeparam name="T">Row DTO whose [ReportColumn] properties define the columns.</typeparam>
    /// <param name="rows">Data rows — already capped and already localized/converted by the caller.</param>
    /// <param name="columnHeaders">
    /// Localized header texts, ordered to match the [ReportColumn] order on <typeparamref name="T"/>.
    /// Count must equal the number of [ReportColumn] properties.
    /// </param>
    /// <param name="header">Page header/footer metadata and layout direction.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<byte[]> ExportToPdfAsync<T>(
        IReadOnlyList<T> rows,
        IReadOnlyList<string> columnHeaders,
        PdfReportHeader header,
        CancellationToken cancellationToken = default);
}