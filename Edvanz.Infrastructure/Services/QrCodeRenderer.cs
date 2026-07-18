using Edvanz.Application.ServiceContract;
using ZXing;
using ZXing.QrCode;
using ZXing.QrCode.Internal;

namespace Edvanz.Infrastructure.Services;

/// <summary>
/// QR code renderer backed by ZXing.Net (MIT). Emits vector SVG so the output is crisp at
/// any size on the student "Show QR Code" screen. Stateless and thread-safe — safe as a
/// scoped or singleton registration.
///
/// Mirrors <see cref="BarcodeRenderer"/> (Code 128) in structure, but writes a 2D
/// <see cref="BarcodeFormat.QR_CODE"/>. The teacher app scans this QR to resolve the
/// per-teacher student code — the same value <c>GetActiveByCodeAndTeacherAsync</c> resolves
/// on the exam/attendance/payment scan paths.
/// </summary>
public sealed class QrCodeRenderer : IQrCodeRenderer
{
    /// <summary>
    /// Quiet zone (module-width units) required around a QR symbol for reliable scanning.
    /// The QR spec mandates a margin of ≥ 4 modules; fewer and phone scanners miss the
    /// finder patterns.
    /// </summary>
    private const int QuietZoneModules = 4;

    /// <inheritdoc />
    public string RenderQrCodeSvg(string code, int sizePx = 240)
    {
        if (string.IsNullOrWhiteSpace(code))
            throw new ArgumentException("QR code value must not be empty.", nameof(code));

        var writer = new BarcodeWriterSvg
        {
            Format = BarcodeFormat.QR_CODE,
            Options = new QrCodeEncodingOptions
            {
                // QR is square — width == height keeps the modules undistorted.
                Width = sizePx,
                Height = sizePx,
                Margin = QuietZoneModules,
                // Level M (~15% recovery) is the standard balance for scanning off a phone screen.
                ErrorCorrection = ErrorCorrectionLevel.M,
                CharacterSet = "UTF-8",
                // Symbol only — the human-readable code is drawn by the caller (profile screen),
                // which keeps fonts consistent and avoids the SVG renderer's text support.
                PureBarcode = true
            }
        };

        // BarcodeWriterSvg.Write returns an SvgImage whose ToString() is the SVG document.
        return writer.Write(code).ToString();
    }
}
