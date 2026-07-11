using Edvanz.Application.Dtos;
using Edvanz.Application.Dtos.TeacherStudent;

namespace Edvanz.Application.ServiceContract;

/// <summary>
/// Barcode presentation for teacher-scoped students:
///   • an in-app SVG image for the student profile screen, and
///   • a printable PDF export for a single student or a bulk selection (REQ-STU-052).
/// The barcode encodes <see cref="Edvanz.Domain.Entities.TeacherStudent.Barcode"/> (= the
/// student code, REQ-STU-047) and is rendered on demand — nothing is stored.
/// </summary>
public interface IStudentBarcodeService
{
    /// <summary>
    /// Returns the Code 128 barcode SVG for a single student, scoped to the teacher.
    /// Fails with 404 when the student does not belong to the teacher (no IDOR).
    /// </summary>
    Task<Result<StudentBarcodeSvgDto>> GetBarcodeSvgAsync(long teacherId, long studentId);

    /// <summary>
    /// Renders a barcode-card PDF for the supplied student ids (one id or many), scoped to
    /// the teacher. Foreign or non-existent ids are silently ignored; an empty resulting set
    /// returns a failure. <paramref name="rtl"/> selects page direction for Arabic culture.
    /// </summary>
    Task<Result<byte[]>> ExportBarcodesPdfAsync(long teacherId, IReadOnlyList<long> studentIds, bool rtl);
}
