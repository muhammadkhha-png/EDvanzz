namespace Edvanz.Domain.Enums;

/// <summary>
/// Determines how student barcodes are made available.
/// AAM-FR-04.7: InApp displays within the student's app profile; HardCopyOnly restricts to printed cards.
/// </summary>
public enum BarcodeDisplayMode
{
    InApp = 1,
    HardCopyOnly = 2
}