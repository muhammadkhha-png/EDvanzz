namespace Edvanz.Application.Dtos.StudentUser;

/// <summary>
/// The per-teacher attendance QR for the authenticated student ("Show QR Code" screen).
/// The student displays <see cref="QrSvg"/> and the teacher scans it (from the teacher app's
/// "Scan QR Code from Student account") to record attendance / exam presence / payment.
///
/// <see cref="Code"/> is the per-teacher student code (<c>TeacherStudent.StudentCode</c>) —
/// exactly the value the teacher's scan resolves via <c>GetActiveByCodeAndTeacherAsync</c>,
/// so the QR-to-scan loop closes. There is ONE code per linked teacher; the app requests
/// this endpoint once per teacher (no global account code is involved here).
/// </summary>
public class StudentTeacherBarcodeDto
{
    /// <summary>The teacher this QR is scoped to (the <c>{teacherId}</c> route segment).</summary>
    public long TeacherId { get; set; }

    /// <summary>Teacher's full name (User.FullName) — shown as the screen header.</summary>
    public string TeacherName { get; set; } = null!;

    /// <summary>
    /// The per-teacher student code the QR encodes. This is <c>TeacherStudent.StudentCode</c> —
    /// the authoritative value the teacher's barcode scan resolves.
    /// </summary>
    public string Code { get; set; } = null!;

    /// <summary>Standalone QR-code SVG markup encoding <see cref="Code"/>.</summary>
    public string QrSvg { get; set; } = null!;
}
