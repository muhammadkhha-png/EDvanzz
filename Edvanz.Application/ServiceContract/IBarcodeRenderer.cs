namespace Edvanz.Application.ServiceContract;

/// <summary>
/// Renders a short code string into a Code 128 barcode as SVG vector markup.
/// Implemented in Infrastructure using ZXing.Net. The barcode is a pure function of the
/// input string (no persistence) — REQ-STU-047 stores only the code, the image is derived.
/// </summary>
public interface IBarcodeRenderer
{
    /// <summary>
    /// Produces standalone SVG markup for a Code 128 barcode encoding <paramref name="code"/>.
    /// The SVG contains only the bars (with a quiet-zone margin) — the human-readable code
    /// text is rendered separately by the caller (profile screen / PDF card).
    /// </summary>
    /// <param name="code">The value to encode (e.g. the student code "A5").</param>
    /// <param name="heightPx">Bar height in pixels. Defaults to a scan-friendly height.</param>
    /// <returns>SVG document as a UTF-8 string.</returns>
    string RenderCode128Svg(string code, int heightPx = 80);
}
