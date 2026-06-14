using DocumentFormat.OpenXml.Spreadsheet;
using DocumentFormat.OpenXml.Wordprocessing;
using Edvanz.Application.Reporting;
using Edvanz.Application.ServiceContract;
using System.Reflection;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using QuestPDF.Drawing;
using QuestPDF.Previewer;
using QuestPDF;
using Document = QuestPDF.Fluent.Document;

namespace Edvanz.Infrastructure.Services;

/// <summary>
/// QuestPDF implementation of <see cref="IPdfExportService"/>. Pure renderer: reflects
/// [ReportColumn] metadata to extract ordered cell values and lays them out as a paged table
/// with a repeating header band, a page footer, and an optional truncation notice. RTL layout
/// is applied when the header direction is RightToLeft; an Arabic fallback font guarantees glyphs.
/// </summary>
public sealed class PdfExportService : IPdfExportService
{
    private const string PrimaryFont = "Lato";            // bundled with QuestPDF
    private const string ArabicFont = "Noto Sans Arabic"; // registered below from embedded resource

    static PdfExportService()
    {
        // QuestPDF Community License — set once, before any document generation.
        QuestPDF.Settings.License = LicenseType.Community;

        // Register the Arabic fallback font once, from THIS assembly's embedded resource.
        // Resource name = {Infrastructure default namespace}.{folder path}.{file}.
        using var fontStream = typeof(PdfExportService).Assembly
            .GetManifestResourceStream("Edvanz.Infrastructure.Resources.Fonts.NotoSansArabic-Regular.ttf");
        if (fontStream is not null)
            FontManager.RegisterFont(fontStream);
    }

    /// <inheritdoc />
    public Task<byte[]> ExportToPdfAsync<T>(
        IReadOnlyList<T> rows,
        IReadOnlyList<string> columnHeaders,
        PdfReportHeader header,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var columns = GetOrderedColumns<T>();
        if (columns.Count == 0)
            throw new ArgumentException($"Type {typeof(T).Name} has no [ReportColumn] properties.");
        if (columnHeaders.Count != columns.Count)
            throw new ArgumentException(
                $"Header count ({columnHeaders.Count}) does not match [ReportColumn] count ({columns.Count}).");

        var document = BuildDocument(rows, columns, columnHeaders, header);
        return Task.Run(() => document.GeneratePdf(), cancellationToken);
    }

    private static QuestPDF.Fluent.Document BuildDocument<T>(
        IReadOnlyList<T> rows,
        IReadOnlyList<PropertyInfo> columns,
        IReadOnlyList<string> columnHeaders,
        PdfReportHeader header)
    {
        var rtl = header.Direction == PdfTextDirection.RightToLeft;

        return Document.Create(doc =>
        {
            doc.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(30);
                page.DefaultTextStyle(x => x.FontSize(10).FontFamily(PrimaryFont, ArabicFont));

                ComposeHeader(ApplyTextDirection(page.Header(), rtl), header);
                ComposeContent(ApplyTextDirection(page.Content(), rtl), rows, columns, columnHeaders, header.TruncationNotice);
                ComposeFooter(ApplyTextDirection(page.Footer(), rtl), header);
            });
        });
    }

    private static IContainer ApplyTextDirection(IContainer container, bool rtl) =>
        rtl ? container.ContentFromRightToLeft() : container;

    private static void ComposeHeader(IContainer container, PdfReportHeader header)
    {
        container.Column(col =>
        {
            col.Item().Text(header.PrimaryHeading).FontSize(18).SemiBold();
            col.Item().Text(header.Title).FontSize(12).FontColor(QuestPDF.Helpers.Colors.Grey.Darken1);
            col.Item().PaddingTop(2).Text(header.GeneratedAtDisplay).FontSize(9).FontColor(QuestPDF.Helpers.Colors.Grey.Medium);

            if (!string.IsNullOrWhiteSpace(header.FilterSummary))
                col.Item().PaddingTop(4).Text(header.FilterSummary!).FontSize(9).FontColor(QuestPDF.Helpers.Colors.Grey.Darken1);

            col.Item().PaddingTop(6).LineHorizontal(0.5f).LineColor(QuestPDF.Helpers.Colors.Grey.Lighten1);
        });
    }

    private static void ComposeContent<T>(
        IContainer container,
        IReadOnlyList<T> rows,
        IReadOnlyList<PropertyInfo> columns,
        IReadOnlyList<string> columnHeaders,
        string? truncationNotice)
    {
        container.PaddingTop(8).Column(col =>
        {
            col.Item().Table(table =>
            {
                table.ColumnsDefinition(def =>
                {
                    foreach (var _ in columns)
                        def.RelativeColumn();
                });

                table.Header(h =>
                {
                    foreach (var headerText in columnHeaders)
                        h.Cell().Element(HeaderCellStyle).Text(headerText).SemiBold();
                });

                foreach (var row in rows)
                {
                    foreach (var prop in columns)
                        table.Cell().Element(BodyCellStyle).Text(FormatCellValue(prop.GetValue(row)));
                }
            });

            if (!string.IsNullOrWhiteSpace(truncationNotice))
            {
                col.Item().PaddingTop(8)
                    .Background(QuestPDF.Helpers.Colors.Orange.Lighten4)
                    .Padding(6)
                    .Text(truncationNotice!).FontSize(9).FontColor(QuestPDF.Helpers.Colors.Orange.Darken2);
            }
        });
    }

    private static void ComposeFooter(IContainer container, PdfReportHeader header)
    {
        container.Column(col =>
        {
            col.Item().PaddingBottom(4).LineHorizontal(0.5f).LineColor(QuestPDF.Helpers.Colors.Grey.Lighten1);
            col.Item().Row(row =>
            {
                row.RelativeItem().Text(header.FooterText).FontSize(8).FontColor(QuestPDF.Helpers.Colors.Grey.Medium);
                row.ConstantItem(90).AlignRight().Text(text =>
                {
                    text.DefaultTextStyle(x => x.FontSize(8).FontColor(QuestPDF.Helpers.Colors.Grey.Medium));
                    text.CurrentPageNumber();
                    text.Span(" / ");
                    text.TotalPages();
                });
            });
        });
    }

    private static IContainer HeaderCellStyle(IContainer container) =>
        container.Background(QuestPDF.Helpers.Colors.Grey.Lighten3).Border(0.5f).BorderColor(QuestPDF.Helpers.Colors.Grey.Lighten1)
                 .PaddingVertical(5).PaddingHorizontal(6);

    private static IContainer BodyCellStyle(IContainer container) =>
        container.Border(0.5f).BorderColor(QuestPDF.Helpers.Colors.Grey.Lighten2).PaddingVertical(4).PaddingHorizontal(6);
    private static IReadOnlyList<PropertyInfo> GetOrderedColumns<T>() =>
        typeof(T).GetProperties()
            .Select(p => new { Prop = p, Attr = p.GetCustomAttribute<ReportColumnAttribute>() })
            .Where(x => x.Attr is not null)
            .OrderBy(x => x.Attr!.Order)
            .Select(x => x.Prop)
            .ToList();

    private static string FormatCellValue(object? value) => value switch
    {
        null => string.Empty,
        DateTime dt => dt.ToString("yyyy-MM-dd HH:mm"),
        _ => value.ToString() ?? string.Empty
    };
}