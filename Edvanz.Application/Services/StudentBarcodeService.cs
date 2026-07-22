using Edvanz.Application.Dtos;
using Edvanz.Application.Dtos.TeacherStudent;
using Edvanz.Application.ServiceContract;
using Edvanz.Domain.Interfaces;
using Microsoft.Extensions.Localization;
using System.Net;

namespace Edvanz.Application.Services;

/// <summary>
/// Orchestrates barcode rendering for students. Owns no rendering technology itself:
/// it fetches tenant-scoped students via <see cref="IUnitOfWork"/>, delegates image
/// generation to <see cref="IBarcodeRenderer"/> (ZXing) and PDF layout to
/// <see cref="IStudentBarcodePdfBuilder"/> (QuestPDF) — both Infrastructure concerns
/// behind Application contracts, so this stays layer-clean.
///
/// REQ-STU-047/048: the barcode encodes the immutable student code. Nothing is persisted;
/// the image is derived on every request (cheap for a short Code 128 string).
/// </summary>
public class StudentBarcodeService : IStudentBarcodeService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IBarcodeRenderer _barcodeRenderer;
    private readonly IQrCodeRenderer _qrCodeRenderer;
    private readonly IStudentBarcodePdfBuilder _pdfBuilder;
    private readonly IStringLocalizer<Domain.Resources.Messages> _localizer;

    public StudentBarcodeService(
        IUnitOfWork unitOfWork,
        IBarcodeRenderer barcodeRenderer,
        IQrCodeRenderer qrCodeRenderer,
        IStudentBarcodePdfBuilder pdfBuilder,
        IStringLocalizer<Domain.Resources.Messages> localizer)
    {
        _unitOfWork = unitOfWork;
        _barcodeRenderer = barcodeRenderer;
        _qrCodeRenderer = qrCodeRenderer;
        _pdfBuilder = pdfBuilder;
        _localizer = localizer;
    }

    /// <summary>
    /// Renders the scannable image for a student code. Code 128 for plain ASCII codes (the
    /// familiar 1D stripe), but Arabic (or any non-ASCII) auto-codes like "أ1" — which the
    /// ZXing Code 128 writer CANNOT encode and would throw "Bad character in input" on (a hard
    /// 500 for those teachers) — fall back to a QR code, which is UTF-8 and encodes them fine.
    /// Both symbologies encode the SAME plain <c>StudentCode</c> payload and both decode back to
    /// it, so the scan-resolution contract is unchanged and already-printed cards stay valid.
    /// </summary>
    private string RenderScannable(string code)
        => code.All(char.IsAscii)
            ? _barcodeRenderer.RenderCode128Svg(code)
            : _qrCodeRenderer.RenderQrCodeSvg(code);

    /// <inheritdoc />
    public async Task<Result<StudentBarcodeSvgDto>> GetBarcodeSvgAsync(long teacherId, long studentId)
    {
        var student = await _unitOfWork.Students.GetActiveByIdAndTeacherAsync(studentId, teacherId);
        if (student is null)
            return Result<StudentBarcodeSvgDto>.Failure(_localizer, "StudentNotFound", HttpStatusCode.NotFound);

        // REQ-STU-047: encode the canonical StudentCode — the exact value EVERY scan path
        // resolves (GetActiveByCodeAndTeacherAsync matches StudentCode). The legacy `Barcode`
        // column is a stale denormalization that was NOT re-synced when a student's code was
        // edited, so encoding it printed a barcode that no longer scanned ("no student matched
        // the scanned barcode"). Reading StudentCode here also self-heals any already-diverged
        // row without a data migration.
        string code = student.StudentCode;

        var dto = new StudentBarcodeSvgDto
        {
            StudentCode = code,
            Svg = RenderScannable(code)
        };

        return Result<StudentBarcodeSvgDto>.Success(dto, _localizer, "Success");
    }

    /// <inheritdoc />
    public async Task<Result<byte[]>> ExportBarcodesPdfAsync(long teacherId, IReadOnlyList<long> studentIds, bool rtl)
    {
        // Distinct, positive ids only — defends the repo query and de-dupes the export.
        var ids = studentIds.Where(id => id > 0).Distinct().ToList();
        if (ids.Count == 0)
            return Result<byte[]>.Failure(_localizer, "NoStudentsSelected", HttpStatusCode.BadRequest);

        // Tenant-scoped fetch: foreign ids simply do not come back, so no IDOR leak.
        var students = await _unitOfWork.Students.GetActiveByIdsAndTeacherAsync(teacherId, ids);
        if (students.Count == 0)
            return Result<byte[]>.Failure(_localizer, "StudentNotFound", HttpStatusCode.NotFound);

        var cards = students
            .OrderBy(s => s.StudentCode)
            .Select(s =>
            {
                // Always encode the canonical StudentCode (the scan key) — never the stale
                // `Barcode` column. See GetBarcodeSvgAsync for the full rationale. Code 128 for
                // ASCII, QR fallback for Arabic (RenderScannable) so an Arabic-coded roster never
                // 500s the whole export.
                string code = s.StudentCode;
                return new StudentBarcodeCard
                {
                    StudentName = s.StudentName,
                    StudentCode = code,
                    BarcodeSvg = RenderScannable(code)
                };
            })
            .ToList();

        byte[] pdf = _pdfBuilder.BuildBarcodeCardsPdf(cards, rtl);
        return Result<byte[]>.Success(pdf, _localizer, "Success");
    }
}
