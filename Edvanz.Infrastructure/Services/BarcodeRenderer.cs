using Edvanz.Application.ServiceContract;
using ZXing;
using ZXing.Common;

namespace Edvanz.Infrastructure.Services;

/// <summary>
/// Code 128 barcode renderer backed by ZXing.Net (MIT). Emits vector SVG so the same
/// output is crisp both in-app (profile screen) and in the printable PDF cards.
/// Stateless and thread-safe — safe as a scoped or singleton registration.
/// </summary>
public sealed class BarcodeRenderer : IBarcodeRenderer
{
    /// <summary>
    /// Quiet zone (module-width units) required on each side for reliable scanning.
    /// Code 128 needs a margin ≥ 10 modules, otherwise scanners miss the start/stop bars.
    /// </summary>
    private const int QuietZoneModules = 10;

    /// <inheritdoc />
    public string RenderCode128Svg(string code, int heightPx = 80)
    {
        if (string.IsNullOrWhiteSpace(code))
            throw new ArgumentException("Barcode value must not be empty.", nameof(code));

        var writer = new BarcodeWriterSvg
        {
            Format = BarcodeFormat.CODE_128,
            Options = new EncodingOptions
            {
                Height = heightPx,
                Margin = QuietZoneModules,
                // Bars only — the human-readable code is drawn by the caller (profile / PDF card),
                // which avoids depending on the SVG renderer's text support and keeps fonts consistent.
                PureBarcode = true
            }
        };

        // BarcodeWriterSvg.Write returns an SvgImage whose ToString() is the SVG document.
        return writer.Write(code).ToString();
    }
}
