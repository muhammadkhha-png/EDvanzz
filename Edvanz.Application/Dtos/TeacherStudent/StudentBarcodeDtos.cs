using System.ComponentModel.DataAnnotations;

namespace Edvanz.Application.Dtos.TeacherStudent;

/// <summary>
/// In-app barcode image for a single student (profile screen).
/// The controller streams <see cref="Svg"/> as <c>image/svg+xml</c>; the code text is
/// returned alongside so the frontend can label the image without a second call.
/// </summary>
public class StudentBarcodeSvgDto
{
    /// <summary>The student code the barcode encodes (e.g. "A5").</summary>
    public string StudentCode { get; set; } = null!;

    /// <summary>Standalone Code 128 SVG markup.</summary>
    public string Svg { get; set; } = null!;
}

/// <summary>
/// One student's data needed to render a barcode card in the export PDF.
/// </summary>
public class StudentBarcodeCard
{
    public string StudentName { get; set; } = null!;
    public string StudentCode { get; set; } = null!;

    /// <summary>Pre-rendered Code 128 SVG for this student's code.</summary>
    public string BarcodeSvg { get; set; } = null!;
}

/// <summary>
/// Request body for the barcode export endpoint. One id exports a single card; many ids
/// export a grid. Ids not belonging to the calling teacher are ignored (REQ-STU-052).
/// </summary>
public class ExportStudentBarcodesRequest
{
    [Required]
    [MinLength(1)]
    public List<long> StudentIds { get; set; } = new();
}
