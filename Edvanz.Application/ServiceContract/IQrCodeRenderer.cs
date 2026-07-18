namespace Edvanz.Application.ServiceContract;

/// <summary>
/// Renders a short code string into a QR code as SVG vector markup.
/// Implemented in Infrastructure using ZXing.Net. The QR is a pure function of the input
/// string (no persistence) — the student app renders the per-teacher student code so the
/// teacher can scan it; the encoded value is the SAME one the exam/attendance/payment
/// barcode-scan paths resolve (TeacherStudent.StudentCode), so the scan loop closes.
///
/// Mirrors <see cref="IBarcodeRenderer"/> (Code 128) in structure; this renders a 2D
/// <c>QR_CODE</c> instead. The Code 128 renderer is intentionally left untouched — hard-copy
/// cards stay Code 128; only the in-app "Show QR Code" screen uses this.
/// </summary>
public interface IQrCodeRenderer
{
    /// <summary>
    /// Produces standalone SVG markup for a QR code encoding <paramref name="code"/>.
    /// The SVG contains only the QR modules (with a quiet-zone margin) — the human-readable
    /// code text is rendered separately by the caller (the student "Show QR Code" screen).
    /// </summary>
    /// <param name="code">The value to encode (the per-teacher student code, e.g. "A5").</param>
    /// <param name="sizePx">Square symbol edge length in pixels. Defaults to a scan-friendly size.</param>
    /// <returns>SVG document as a UTF-8 string.</returns>
    string RenderQrCodeSvg(string code, int sizePx = 240);
}
