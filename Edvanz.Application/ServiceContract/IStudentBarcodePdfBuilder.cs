using Edvanz.Application.Dtos.TeacherStudent;

namespace Edvanz.Application.ServiceContract;

/// <summary>
/// Builds a printable PDF of student barcode cards (single or bulk).
/// Implemented in Infrastructure using QuestPDF. REQ-STU-052: bulk barcode export.
/// </summary>
public interface IStudentBarcodePdfBuilder
{
    /// <summary>
    /// Renders one barcode card per supplied student (name + code + Code 128 barcode),
    /// laid out as a grid, into a single PDF byte array.
    /// </summary>
    /// <param name="cards">The students to render — already scoped to the teacher by the caller.</param>
    /// <param name="rtl">True to lay the page out right-to-left (Arabic UI culture).</param>
    byte[] BuildBarcodeCardsPdf(IReadOnlyList<StudentBarcodeCard> cards, bool rtl);
}
