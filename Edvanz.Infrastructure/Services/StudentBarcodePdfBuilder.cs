using Edvanz.Application.Dtos.TeacherStudent;
using Edvanz.Application.ServiceContract;
using QuestPDF.Drawing;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Edvanz.Infrastructure.Services;

/// <summary>
/// Builds the student barcode-card PDF (REQ-STU-052) with QuestPDF. Cards flow in a
/// two-column grid; each card shows the student name, the Code 128 barcode (embedded as
/// vector SVG so it stays print-sharp), and the code text underneath.
/// </summary>
public sealed class StudentBarcodePdfBuilder : IStudentBarcodePdfBuilder
{
    private const string PrimaryFont = "Lato";            // bundled with QuestPDF
    private const string ArabicFont = "Noto Sans Arabic"; // embedded resource (shared with PdfExportService)
    private const int CardsPerRow = 2;

    static StudentBarcodePdfBuilder()
    {
        // Idempotent with PdfExportService's own static ctor — setting the license and
        // registering the font more than once is harmless, and either type may load first.
        QuestPDF.Settings.License = LicenseType.Community;

        using var fontStream = typeof(StudentBarcodePdfBuilder).Assembly
            .GetManifestResourceStream("Edvanz.Infrastructure.Resources.Fonts.NotoSansArabic-Regular.ttf");
        if (fontStream is not null)
            FontManager.RegisterFont(fontStream);
    }

    /// <inheritdoc />
    public byte[] BuildBarcodeCardsPdf(IReadOnlyList<StudentBarcodeCard> cards, bool rtl)
    {
        var document = Document.Create(doc =>
        {
            doc.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(25);
                page.DefaultTextStyle(x => x.FontSize(11).FontFamily(PrimaryFont, ArabicFont));

                var content = rtl ? page.Content().ContentFromRightToLeft() : page.Content();
                ComposeCards(content, cards);
            });
        });

        return document.GeneratePdf();
    }

    private static void ComposeCards(IContainer container, IReadOnlyList<StudentBarcodeCard> cards)
    {
        // Lay the cards out as explicit rows of CardsPerRow, each row wrapped in ShowEntire so a card
        // is NEVER split across a page break (its name, barcode and code always stay together). A table
        // row could otherwise paginate mid-card — the reported bug where the name sat at the bottom of
        // one page and the barcode at the top of the next (REQ-STU-052). A single card is far smaller
        // than a page, so ShowEntire never overflows; when a row doesn't fit, it moves to the next page.
        container.Column(column =>
        {
            for (int i = 0; i < cards.Count; i += CardsPerRow)
            {
                column.Item().ShowEntire().Row(row =>
                {
                    int placed = 0;
                    for (int j = i; j < cards.Count && placed < CardsPerRow; j++, placed++)
                        row.RelativeItem().Element(CardStyle).Column(ComposeCard(cards[j]));

                    // Keep a partly-filled final row's cards at column width (don't stretch a lone card).
                    for (; placed < CardsPerRow; placed++)
                        row.RelativeItem();
                });
            }
        });
    }

    private static Action<ColumnDescriptor> ComposeCard(StudentBarcodeCard card) => col =>
    {
        col.Item().AlignCenter().Text(card.StudentName)
            .SemiBold().FontSize(12);

        // Vector barcode — scales without pixelation on print. The barcode fills the cell width
        // within a fixed height; note QuestPDF rejects an .AlignCenter() between .Height() and .Svg()
        // (Svg derives its own size → conflicting constraints).
        col.Item().PaddingVertical(6).Height(60).Svg(card.BarcodeSvg);

        col.Item().AlignCenter().Text(card.StudentCode)
            .FontSize(11).FontColor(Colors.Grey.Darken2);
    };

    private static IContainer CardStyle(IContainer container) =>
        container
            .Padding(6)
            .Border(0.75f)
            .BorderColor(Colors.Grey.Lighten1)
            .Padding(10);
}
