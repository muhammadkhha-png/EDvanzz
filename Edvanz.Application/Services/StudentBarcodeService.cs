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
    private readonly IStudentBarcodePdfBuilder _pdfBuilder;
    private readonly IStringLocalizer<Domain.Resources.Messages> _localizer;

    public StudentBarcodeService(
        IUnitOfWork unitOfWork,
        IBarcodeRenderer barcodeRenderer,
        IStudentBarcodePdfBuilder pdfBuilder,
        IStringLocalizer<Domain.Resources.Messages> localizer)
    {
        _unitOfWork = unitOfWork;
        _barcodeRenderer = barcodeRenderer;
        _pdfBuilder = pdfBuilder;
        _localizer = localizer;
    }

    /// <inheritdoc />
    public async Task<Result<StudentBarcodeSvgDto>> GetBarcodeSvgAsync(long teacherId, long studentId)
    {
        var student = await _unitOfWork.Students.GetActiveByIdAndTeacherAsync(studentId, teacherId);
        if (student is null)
            return Result<StudentBarcodeSvgDto>.Failure(_localizer, "StudentNotFound", HttpStatusCode.NotFound);

        // Barcode value defaults to the student code (REQ-STU-047); older rows may have it null.
        string code = string.IsNullOrWhiteSpace(student.Barcode) ? student.StudentCode : student.Barcode!;

        var dto = new StudentBarcodeSvgDto
        {
            StudentCode = code,
            Svg = _barcodeRenderer.RenderCode128Svg(code)
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
                string code = string.IsNullOrWhiteSpace(s.Barcode) ? s.StudentCode : s.Barcode!;
                return new StudentBarcodeCard
                {
                    StudentName = s.StudentName,
                    StudentCode = code,
                    BarcodeSvg = _barcodeRenderer.RenderCode128Svg(code)
                };
            })
            .ToList();

        byte[] pdf = _pdfBuilder.BuildBarcodeCardsPdf(cards, rtl);
        return Result<byte[]>.Success(pdf, _localizer, "Success");
    }
}
