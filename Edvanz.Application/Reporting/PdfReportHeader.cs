namespace Edvanz.Application.Reporting;

/// <summary>Text/layout direction for a generated report.</summary>
public enum PdfTextDirection
{
    LeftToRight = 0,
    RightToLeft = 1
}

/// <summary>
/// Page header/footer metadata for a generated PDF. All strings arrive already localized
/// and already formatted by the caller; the export service is a pure renderer and performs
/// no localization or timezone logic.
/// </summary>
public class PdfReportHeader
{
    /// <summary>
    /// The most prominent line at the top of every page. For the audit-trail report this is
    /// the teacher's name (REQ-USR-030).
    /// </summary>
    public string PrimaryHeading { get; set; } = string.Empty;

    /// <summary>Localized report title, shown beneath the primary heading.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Pre-formatted local generation timestamp (already converted to the user's zone).</summary>
    public string GeneratedAtDisplay { get; set; } = string.Empty;

    /// <summary>Optional localized one-line summary of the applied filters.</summary>
    public string? FilterSummary { get; set; }

    /// <summary>Localized footer text (brand/report line), shown bottom-left of every page.</summary>
    public string FooterText { get; set; } = string.Empty;

    /// <summary>
    /// Optional localized notice shown below the table when the row cap truncated the data.
    /// Null when the full result set fit within the cap.
    /// </summary>
    public string? TruncationNotice { get; set; }

    /// <summary>Layout/text direction. RightToLeft mirrors the header, table, and footer.</summary>
    public PdfTextDirection Direction { get; set; } = PdfTextDirection.LeftToRight;
}