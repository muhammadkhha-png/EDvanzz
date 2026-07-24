using Edvanz.Application.Dtos;

namespace Edvanz.Application.ServiceContract;

public interface IStudentOnlineExamService
{
    Task<Result<StudentOnlineExamListDto>> GetMyExamsAsync(
        long teacherId, long teacherStudentId, string? studentLanguage);
    Task<Result<OnlineExamTakeScreenDto>> GetTakeScreenAsync(long teacherId, long teacherStudentId, long onlineExamId);
    Task<Result<OnlineExamReviewDto>> GetReviewAsync(long teacherId, long teacherStudentId, long onlineExamId);
    Task<Result<OnlineExamStatsDto>> SubmitAnswerAsync(
    long teacherId, long teacherStudentId, long onlineExamId, SubmitOnlineExamAnswerRequest request);

    Task<Result<OnlineExamStatsDto>> SubmitExamAsync(
        long teacherId, long teacherStudentId, long onlineExamId, SubmitOnlineExamRequest request);

    Task<Result<OnlineExamStatsDto>> GetResultAsync(
        long teacherId, long teacherStudentId, long onlineExamId);

    /// <summary>
    /// O1 — student/front-end self-service block: when the frontend detects the caller leaving an
    /// in-progress exam it locks the caller's own report to <c>Blocked</c>. Lazy-creates the report
    /// if needed (like <c>GetOrCreateReportAsync</c>). Idempotent: already-<c>Blocked</c> → success
    /// with the inert code <c>AlreadyBlocked</c>; already submitted/finalized → 409
    /// <c>ExamAlreadyFinalized</c> (block is moot). Distinct from the teacher T5s block. No retake.
    /// </summary>
    Task<Result<OnlineExamStatsDto>> BlockMyExamAsync(
        long teacherId, long teacherStudentId, long onlineExamId);

    /// <summary>
    /// O2 — records ONE anti-cheat violation (the app fires this each time the student leaves/
    /// backgrounds the exam while <c>BlockOnViolation</c> is on). Server-authoritative so the tally
    /// survives an app kill: atomically increments the caller's <c>ViolationCount</c> and, once it
    /// reaches the exam's <c>MaxViolations</c>, sets the report Blocked. Returns the fresh
    /// <c>{ violationCount, maxViolations, isBlocked }</c>. Gates: exam not Draft (404), window open
    /// (409 <c>WindowClosed</c>), student assigned (403 <c>NotInScope</c>). Terminal reports (submitted
    /// / already Blocked) are no-ops that echo the current tally. Idempotency is per-call — each call is
    /// one violation. Distinct from <see cref="BlockMyExamAsync"/> (the terminal one-shot block).
    /// </summary>
    Task<Result<ViolationRecordedDto>> RecordViolationAsync(
        long teacherId, long teacherStudentId, long onlineExamId);
}