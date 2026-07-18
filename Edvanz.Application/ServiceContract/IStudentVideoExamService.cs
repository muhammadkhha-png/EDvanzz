using Edvanz.Application.Dtos;
using Edvanz.Application.Dtos.VideoContentManagement;

namespace Edvanz.Application.ServiceContract;

/// <summary>
/// Student-facing video-quiz flow (Module 14) — the take-screen, submit/auto-grade,
/// retake, result, and review for a video's <c>VideoExam</c>. Video-module twin of
/// <c>IStudentOnlineExamService</c>, but video quizzes RETAKE (online exams never do).
///
/// TENANT/IDENTITY: <paramref name="teacherId"/> comes from the route (a student may link
/// to several teachers) and <paramref name="teacherStudentId"/> is resolved by the
/// controller from the active <c>StudentTeacherLink</c> — never trusted from the body.
///
/// GATES (every method): the module must be active (BR-ADM-010, runtime check — students
/// have no <c>module</c> JWT claim), the student must be in the video's scope AND the video
/// Published/PublishDate-visible (canonical <c>IsStudentInVideoScopeAsync</c>), and the video
/// must have a quiz.
/// </summary>
public interface IStudentVideoExamService
{
    /// <summary>
    /// Take-screen: the quiz's questions + options (NEVER the answer key) plus the student's
    /// current attempt state (status, last score, can-retake). Errors:
    /// <c>ModuleDeactivated</c> (403), <c>VideoNotFound</c> (404), <c>VideoNotInScope</c> (403),
    /// <c>VideoQuizNotFound</c> (404).
    /// </summary>
    Task<Result<VideoExamTakeScreenDto>> GetTakeScreenAsync(
        long teacherId, long teacherStudentId, long videoAssetId);

    /// <summary>
    /// Submits + auto-grades an attempt (finalize). Grades via the shared online-exam grader
    /// (single-choice all-or-nothing, multiple-choice partial credit). Returns the stats.
    /// A already-submitted attempt returns 409 <c>VideoQuizAlreadySubmitted</c> — retake first.
    /// Additional errors: the access gates above, plus <c>VideoQuizQuestionNotFound</c> (400),
    /// <c>VideoQuizInvalidOptionSelection</c> (400), <c>VideoQuizSelectOneAnswer</c> (400),
    /// <c>ConcurrencyConflict</c> (409).
    /// </summary>
    Task<Result<VideoExamStatsDto>> SubmitAsync(
        long teacherId, long teacherStudentId, long videoAssetId, SubmitVideoExamRequest request);

    /// <summary>
    /// Retake ("Retry"): resets the student's attempt to a fresh in-progress one — prior
    /// answers/score are wiped — and returns the fresh take-screen. Video-quiz only.
    /// </summary>
    Task<Result<VideoExamTakeScreenDto>> RetryAsync(
        long teacherId, long teacherStudentId, long videoAssetId);

    /// <summary>
    /// Result: the student's stats for their attempt. 404 <c>VideoQuizAttemptNotFound</c> when
    /// the student has never attempted this quiz.
    /// </summary>
    Task<Result<VideoExamStatsDto>> GetResultAsync(
        long teacherId, long teacherStudentId, long videoAssetId);

    /// <summary>
    /// Review: every question with the student's selected options; the answer key
    /// (<c>isCorrect</c>) and per-question awarded degree are revealed ONLY once the attempt is
    /// finalized (Finalized=false hides them). Returns a skeleton (no selections) when the
    /// student has not attempted yet.
    /// </summary>
    Task<Result<VideoExamReviewDto>> GetReviewAsync(
        long teacherId, long teacherStudentId, long videoAssetId);
}
