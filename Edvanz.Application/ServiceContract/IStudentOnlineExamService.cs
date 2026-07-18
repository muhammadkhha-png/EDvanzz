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
}