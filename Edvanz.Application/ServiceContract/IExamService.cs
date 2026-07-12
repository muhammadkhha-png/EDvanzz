using Edvanz.Application.Dtos;
using Edvanz.Application.Dtos.Exams;

namespace Edvanz.Application.ServiceContract;

/// <summary>
/// Application contract for the offline Exams module (the clean <c>/api/exams</c> surface).
/// Distinct from the shared <see cref="IExamHomeworkService"/>, which still serves homework and
/// the legacy assignment routes. All methods return <see cref="Result{T}"/> with a stable
/// <c>Code</c> and a localized message; teacherId/actingUserId are resolved from the JWT.
/// </summary>
public interface IExamService
{
    /// <summary>
    /// Creates an offline exam. Builds ONE occurrence per anchored session — DuringSession links a
    /// scheduled <c>SessionOccurrence</c> (its date + attendance drive the exam); SeparateTime uses
    /// its own date — and creates a Pending obligation for each session's students.
    /// </summary>
    Task<Result<ExamCreatedDto>> CreateExamAsync(long teacherId, long actingUserId, CreateExamDto dto);

    /// <summary>
    /// Lists a session's scheduled occurrences within the given month, for the "during session"
    /// exam-date picker.
    /// </summary>
    Task<Result<List<SessionExamDateDto>>> GetSessionExamDatesAsync(
        long teacherId, long sessionId, int year, int month);

    /// <summary>
    /// Exam Home: every exam occurrence split into upcoming (date not yet reached) and past
    /// (date passed), each carrying delivery type, session, date, and assigned/attended/missed counts.
    /// </summary>
    Task<Result<ExamHomeDto>> GetExamHomeAsync(long teacherId, int upcomingPage, int pastPage, int pageSize);

    /// <summary>
    /// The opened-exam view: header + global statistics + per-session groups (each session with its
    /// own statistics and student roster). One call renders the whole screen.
    /// </summary>
    Task<Result<ExamViewDto>> GetExamViewAsync(long teacherId, long examId);

    /// <summary>
    /// Paged student roster for a single session within an exam (drill-in / large-session view).
    /// </summary>
    Task<Result<ExamSessionRosterDto>> GetExamSessionRosterAsync(
        long teacherId, long examId, long sessionId, int page, int pageSize, string? search);

    /// <summary>
    /// Saves a batch of distinct per-student grades in one transaction. Enforces 0 ≤ grade ≤ the
    /// exam's max (code <c>GradeExceedsMax</c>), rejects grading an absent student, and enforces the
    /// during-session grades-only guard (a during-session student not yet marked attended cannot be
    /// graded from the exam screen). A <c>null</c> grade clears a previously entered grade (reverting
    /// AttendedWithGrade back to Attended, attendance preserved). Valid rows are applied; invalid rows
    /// are reported per-item.
    /// </summary>
    Task<Result<BatchGradeResultDto>> SaveGradesAsync(long teacherId, long actingUserId, BatchGradeDto dto);

    /// <summary>
    /// Batch manual attendance (present/absent) for a SEPARATE-time exam occurrence. Rejected for
    /// during-session exams (their attendance comes from the session — <c>AttendanceReadOnlyForDuringSession</c>).
    /// Present keeps any entered grade; Absent clears it.
    /// </summary>
    Task<Result<ExamAttendanceResultDto>> MarkExamAttendanceAsync(
        long teacherId, long actingUserId, ExamAttendanceDto dto);

    /// <summary>
    /// Scan a student's QR/code to mark them attended for a SEPARATE-time exam occurrence (idempotent).
    /// Rejected for during-session exams.
    /// </summary>
    Task<Result<ExamScanResultDto>> ScanExamAttendanceAsync(
        long teacherId, long actingUserId, ExamScanDto dto);
}
