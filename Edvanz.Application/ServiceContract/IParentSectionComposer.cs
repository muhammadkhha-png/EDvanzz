using Edvanz.Application.Dtos.ParentUser;

namespace Edvanz.Application.ServiceContract;

/// <summary>
/// The parent-facing SECTION BUILDERS, extracted from <c>ParentDashboardService</c> so the parent
/// MOBILE dashboard and the public PARENT PORTAL compose the same data the same way — one
/// implementation, never a fork.
///
/// Every method is FAIL-SOFT by contract: a module that throws is logged as a warning and returns
/// an empty section, so one broken module can never break the others or the whole page. A
/// Forbidden result from a module's own visibility gate means "hidden", not "error".
///
/// Where a module already self-gates on the teacher's Parent-visibility flag (attendance via
/// <c>AttendanceViewerType.Parent</c>, payments via <c>PaymentViewerType.Parent</c>) the flag is
/// NOT re-checked here — the section's <c>Visible</c> is simply the module's own verdict. Where no
/// such gate exists (videos, homework, exams) the caller passes the flag in.
/// </summary>
public interface IParentSectionComposer
{
    /// <summary>
    /// Attendance for one month. <paramref name="year"/>/<paramref name="month"/> null → the
    /// teacher-local (Africa/Cairo) CURRENT month, resolved inside the attendance service.
    /// Self-gates on <c>ParentVisibilityAttendance</c>.
    /// </summary>
    Task<ParentDashboardAttendanceDto> BuildAttendanceAsync(
        long teacherId, long teacherStudentId, int? year = null, int? month = null);

    /// <summary>Payment tracking screen. Self-gates on <c>ParentVisibilityPayment</c>.</summary>
    Task<ParentDashboardPaymentDto> BuildPaymentsAsync(long teacherId, long teacherStudentId);

    /// <summary>Seen/unseen video rollup. Gated by the caller on <c>ParentVisibilityVideo</c>.</summary>
    Task<ParentDashboardVideosDto> BuildVideosAsync(long teacherId, long teacherStudentId, bool visible);

    /// <summary>Homework status breakdown. Gated by the caller on <c>ParentVisibilityHomework</c>.</summary>
    Task<ParentDashboardHomeworkDto> BuildHomeworkAsync(long teacherId, long teacherStudentId, bool visible);

    /// <summary>
    /// ONLINE exam result rows (past attempts only — an upcoming exam has no result).
    /// Gated by the caller on <c>ParentVisibilityOnlineExamDefault</c>.
    /// </summary>
    Task<ParentGradeSectionDto> BuildOnlineGradesAsync(
        long teacherId, long teacherStudentId, string? language, bool visible);

    /// <summary>
    /// OFFLINE (paper) exam rows, graded and not-yet-graded alike.
    /// Gated by the caller on <c>ParentVisibilityExamDefault</c>.
    /// </summary>
    Task<ParentGradeSectionDto> BuildOfflineGradesAsync(
        long teacherId, long teacherStudentId, string? language, bool visible);
}
