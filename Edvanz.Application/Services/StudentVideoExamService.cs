using Edvanz.Application.Dtos;
using Edvanz.Application.Dtos.VideoContentManagement;
using Edvanz.Application.ServiceContract;
using Edvanz.Domain.Constants;
using Edvanz.Domain.Entities;
using Edvanz.Domain.Enums;
using Edvanz.Domain.Interfaces;
using Edvanz.Domain.Resources;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using System.Net;

namespace Edvanz.Application.Services;

/// <summary>
/// Student-facing video-quiz flow (Module 14). Video-module twin of
/// <see cref="StudentOnlineExamService"/>, adding RETAKE. Grading is delegated to the shared
/// online-exam grader (<see cref="IOnlineExamGradingService"/>) — this service never forks the
/// grading math; it only maps <c>VideoExamQuestion</c>/<c>Option</c> data onto the grader's
/// primitives and tallies the stats.
/// </summary>
public sealed class StudentVideoExamService : IStudentVideoExamService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IStringLocalizer<Messages> _localizer;
    private readonly IOnlineExamGradingService _grading;
    private readonly IFileAccessService _fileAccess;

    public StudentVideoExamService(
        IUnitOfWork unitOfWork,
        IStringLocalizer<Messages> localizer,
        IOnlineExamGradingService grading,
        IFileAccessService fileAccess)
    {
        _unitOfWork = unitOfWork;
        _localizer = localizer;
        _grading = grading;
        _fileAccess = fileAccess;
    }

    // ══════════════════════════════════════════════════════════════════════
    // TAKE SCREEN (no answer key)
    // ══════════════════════════════════════════════════════════════════════
    /// <inheritdoc />
    public async Task<Result<VideoExamTakeScreenDto>> GetTakeScreenAsync(
        long teacherId, long teacherStudentId, long videoAssetId)
    {
        var gate = await CheckAccessAsync(teacherId, teacherStudentId, videoAssetId);
        if (gate is not null)
            return Result<VideoExamTakeScreenDto>.Failure(_localizer, gate.Value.ErrorKey, gate.Value.Status);

        var header = await _unitOfWork.VideoAssetsRepo.GetVideoExamHeaderAsync(videoAssetId, teacherId);
        if (header is null)
            return Result<VideoExamTakeScreenDto>.Failure(
                _localizer, VideoConstants.Messages.VideoQuizNotFound, HttpStatusCode.NotFound);

        var dto = await BuildTakeScreenDtoAsync(teacherId, teacherStudentId, videoAssetId, header);
        return Result<VideoExamTakeScreenDto>.Success(dto, _localizer);
    }

    // ══════════════════════════════════════════════════════════════════════
    // SUBMIT (auto-grade + finalize)
    // ══════════════════════════════════════════════════════════════════════
    /// <inheritdoc />
    public async Task<Result<VideoExamStatsDto>> SubmitAsync(
        long teacherId, long teacherStudentId, long videoAssetId, SubmitVideoExamRequest request)
    {
        var gate = await CheckAccessAsync(teacherId, teacherStudentId, videoAssetId);
        if (gate is not null)
            return Result<VideoExamStatsDto>.Failure(_localizer, gate.Value.ErrorKey, gate.Value.Status);

        var exam = await _unitOfWork.VideoAssetsRepo.GetExamWithQuestionsAsync(videoAssetId, teacherId);
        if (exam is null)
            return Result<VideoExamStatsDto>.Failure(
                _localizer, VideoConstants.Messages.VideoQuizNotFound, HttpStatusCode.NotFound);

        var questionsById = exam.Questions.ToDictionary(q => q.Id);

        // Validate + grade every answered question up-front (fail-fast, before any write).
        var graded = new List<(long QuestionId, decimal Awarded, HashSet<long> Selected)>();
        foreach (var a in request.Answers ?? new List<SubmitVideoExamAnswerDto>())
        {
            if (!questionsById.TryGetValue(a.QuestionId, out var q))
                return Result<VideoExamStatsDto>.Failure(
                    _localizer, VideoConstants.Messages.VideoQuizQuestionNotFound, HttpStatusCode.BadRequest);

            var selected = (a.SelectedOptionIds ?? new List<long>()).Distinct().ToHashSet();
            if (selected.Count == 0)
                continue; // unanswered — scores 0, counted as NotAnswered

            var validOptionIds = q.Options.Select(o => o.Id).ToHashSet();
            if (selected.Any(id => !validOptionIds.Contains(id)))
                return Result<VideoExamStatsDto>.Failure(
                    _localizer, VideoConstants.Messages.VideoQuizInvalidOptionSelection, HttpStatusCode.BadRequest);

            if (q.QuestionType == VideoExamQuestionType.SingleChoice && selected.Count != 1)
                return Result<VideoExamStatsDto>.Failure(
                    _localizer, VideoConstants.Messages.VideoQuizSelectOneAnswer, HttpStatusCode.BadRequest);

            var correctIds = q.Options.Where(o => o.IsCorrect).Select(o => o.Id).ToHashSet();

            // Shared grader primitive (owned by the online-exam module — never forked here):
            // single-choice all-or-nothing, multiple-choice partial credit. 1 point per question.
            decimal awarded = _grading.GradeQuestion(
                q.QuestionType == VideoExamQuestionType.MultipleChoice,
                correctIds,
                selected,
                VideoExamPointsPerQuestion);

            graded.Add((a.QuestionId, awarded, selected));
        }

        var report = await _unitOfWork.StudentVideoExamReportsRepo
            .GetByVideoAndStudentAsync(videoAssetId, teacherStudentId);

        // Locked once finalized — retake first (explicit reset), mirroring the online-exam lock.
        if (report?.SubmittedAt is not null)
            return Result<VideoExamStatsDto>.Failure(
                _localizer, VideoConstants.Messages.VideoQuizAlreadySubmitted, HttpStatusCode.Conflict);

        int totalQuestions = exam.Questions.Count;
        decimal totalDegree = totalQuestions * VideoExamPointsPerQuestion;
        decimal score = graded.Sum(g => g.Awarded);
        decimal percentage = totalDegree > 0 ? Math.Round(score / totalDegree * 100, 2) : 0;

        bool ownsTransaction = !_unitOfWork.HasActiveTransaction;
        if (ownsTransaction) await _unitOfWork.BeginTransactionAsync();
        try
        {
            var utcNow = DateTime.UtcNow;
            bool isNew = report is null;

            if (isNew)
            {
                report = new StudentVideoExamReport
                {
                    VideoAssetId = videoAssetId,
                    VideoExamId = exam.Id,
                    TeacherStudentId = teacherStudentId,
                    TeacherId = teacherId,
                    CreateAt = utcNow,
                };
            }
            else
            {
                // Existing in-progress attempt — clear any stale answers before re-filling.
                await _unitOfWork.StudentVideoExamReportsRepo.ClearAnswersForReportAsync(report!.Id);
                report.VideoExamId = exam.Id;
            }

            // FK columns are filled by EF from the navigation on insert — no manual id wiring.
            foreach (var g in graded)
            {
                report!.Answers.Add(new StudentVideoExamAnswer
                {
                    VideoExamQuestionId = g.QuestionId,
                    AwardedDegree = g.Awarded,
                    CreateAt = utcNow,
                    SelectedOptions = g.Selected
                        .Select(id => new StudentVideoExamAnswerOption
                        {
                            VideoExamQuestionOptionId = id,
                            CreateAt = utcNow,
                        })
                        .ToList(),
                });
            }

            report!.Score = score;
            report.Percentage = percentage;
            report.Status = StudentVideoExamStatus.Completed;
            report.SubmittedAt = utcNow;
            report.UpdatedAt = utcNow;

            if (isNew) await _unitOfWork.StudentVideoExamReportsRepo.AddReportAsync(report);
            else await _unitOfWork.StudentVideoExamReportsRepo.UpdateAsync(report);

            try
            {
                await _unitOfWork.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                // A concurrent submit already finalized this attempt — RowVersion caught the race.
                if (ownsTransaction) await _unitOfWork.RollbackAsync();
                return Result<VideoExamStatsDto>.Failure(
                    _localizer, VideoConstants.Messages.VideoQuizAlreadySubmitted, HttpStatusCode.Conflict);
            }
            catch (DbUpdateException) when (isNew)
            {
                // First-attempt INSERT race: a concurrent first submit/retry created the report row
                // between our null-read and this insert, colliding on the unique index
                // UX_StudentVideoExamReports_Video_Student. Re-read and return the same clean outcome
                // the existing-report branch would — a finalized winner is AlreadySubmitted (409),
                // otherwise a plain conflict for the client to retry (never a 500).
                if (ownsTransaction) await _unitOfWork.RollbackAsync();
                var existing = await _unitOfWork.StudentVideoExamReportsRepo
                    .GetByVideoAndStudentAsync(videoAssetId, teacherStudentId);
                return existing?.SubmittedAt is not null
                    ? Result<VideoExamStatsDto>.Failure(
                        _localizer, VideoConstants.Messages.VideoQuizAlreadySubmitted, HttpStatusCode.Conflict)
                    : Result<VideoExamStatsDto>.Failure(
                        _localizer, VideoConstants.Messages.ConcurrencyConflict, HttpStatusCode.Conflict);
            }

            if (ownsTransaction) await _unitOfWork.CommitAsync();
        }
        catch
        {
            if (ownsTransaction) await _unitOfWork.RollbackAsync();
            throw;
        }

        var stats = BuildStats(
            totalQuestions, graded.Select(g => g.Awarded).ToList(), score, totalDegree,
            StudentVideoExamStatus.Completed);
        return Result<VideoExamStatsDto>.Success(stats, _localizer, VideoConstants.Messages.VideoQuizSubmitted);
    }

    // ══════════════════════════════════════════════════════════════════════
    // RETRY (reset for a fresh attempt) — video-quiz ONLY
    // ══════════════════════════════════════════════════════════════════════
    /// <inheritdoc />
    public async Task<Result<VideoExamTakeScreenDto>> RetryAsync(
        long teacherId, long teacherStudentId, long videoAssetId)
    {
        var gate = await CheckAccessAsync(teacherId, teacherStudentId, videoAssetId);
        if (gate is not null)
            return Result<VideoExamTakeScreenDto>.Failure(_localizer, gate.Value.ErrorKey, gate.Value.Status);

        var header = await _unitOfWork.VideoAssetsRepo.GetVideoExamHeaderAsync(videoAssetId, teacherId);
        if (header is null)
            return Result<VideoExamTakeScreenDto>.Failure(
                _localizer, VideoConstants.Messages.VideoQuizNotFound, HttpStatusCode.NotFound);

        bool ownsTransaction = !_unitOfWork.HasActiveTransaction;
        if (ownsTransaction) await _unitOfWork.BeginTransactionAsync();
        try
        {
            var utcNow = DateTime.UtcNow;
            var report = await _unitOfWork.StudentVideoExamReportsRepo
                .GetByVideoAndStudentAsync(videoAssetId, teacherStudentId);
            bool isNew = report is null;

            if (report is null)
            {
                report = new StudentVideoExamReport
                {
                    VideoAssetId = videoAssetId,
                    VideoExamId = header.ExamId,
                    TeacherStudentId = teacherStudentId,
                    TeacherId = teacherId,
                    Status = StudentVideoExamStatus.InProgress,
                    Score = 0,
                    Percentage = 0,
                    CreateAt = utcNow,
                };
                await _unitOfWork.StudentVideoExamReportsRepo.AddReportAsync(report);
            }
            else
            {
                await _unitOfWork.StudentVideoExamReportsRepo.ClearAnswersForReportAsync(report.Id);
                report.Status = StudentVideoExamStatus.InProgress;
                report.Score = 0;
                report.Percentage = 0;
                report.SubmittedAt = null;
                report.VideoExamId = header.ExamId;
                report.UpdatedAt = utcNow;
                await _unitOfWork.StudentVideoExamReportsRepo.UpdateAsync(report);
            }

            try
            {
                await _unitOfWork.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (ownsTransaction) await _unitOfWork.RollbackAsync();
                return Result<VideoExamTakeScreenDto>.Failure(
                    _localizer, VideoConstants.Messages.ConcurrencyConflict, HttpStatusCode.Conflict);
            }
            catch (DbUpdateException) when (isNew)
            {
                // First-attempt INSERT race: a concurrent first submit/retry created the report row
                // between our null-read and this insert (UX_StudentVideoExamReports_Video_Student).
                // Our reset lost the create race — return a clean conflict for the client to retry
                // (never a 500); a follow-up retry will take the existing-report reset branch.
                if (ownsTransaction) await _unitOfWork.RollbackAsync();
                return Result<VideoExamTakeScreenDto>.Failure(
                    _localizer, VideoConstants.Messages.ConcurrencyConflict, HttpStatusCode.Conflict);
            }

            if (ownsTransaction) await _unitOfWork.CommitAsync();
        }
        catch
        {
            if (ownsTransaction) await _unitOfWork.RollbackAsync();
            throw;
        }

        var dto = await BuildTakeScreenDtoAsync(teacherId, teacherStudentId, videoAssetId, header);
        return Result<VideoExamTakeScreenDto>.Success(dto, _localizer, VideoConstants.Messages.VideoQuizReset);
    }

    // ══════════════════════════════════════════════════════════════════════
    // RESULT
    // ══════════════════════════════════════════════════════════════════════
    /// <inheritdoc />
    public async Task<Result<VideoExamStatsDto>> GetResultAsync(
        long teacherId, long teacherStudentId, long videoAssetId)
    {
        var gate = await CheckAccessAsync(teacherId, teacherStudentId, videoAssetId);
        if (gate is not null)
            return Result<VideoExamStatsDto>.Failure(_localizer, gate.Value.ErrorKey, gate.Value.Status);

        var report = await _unitOfWork.StudentVideoExamReportsRepo
            .GetReportWithAnswersAsync(videoAssetId, teacherStudentId);
        if (report is null)
            return Result<VideoExamStatsDto>.Failure(
                _localizer, VideoConstants.Messages.VideoQuizAttemptNotFound, HttpStatusCode.NotFound);

        int totalQuestions = await _unitOfWork.VideoAssetsRepo.GetExamQuestionCountAsync(videoAssetId);
        decimal totalDegree = totalQuestions * VideoExamPointsPerQuestion;

        var stats = BuildStats(
            totalQuestions, report.Answers.Select(a => a.AwardedDegree).ToList(),
            report.Score, totalDegree, report.Status);
        // Headline figures come from the stored (finalized) report so they never drift.
        stats.Percentage = report.Percentage;
        stats.Stars = _grading.ComputeStars(report.Percentage);
        return Result<VideoExamStatsDto>.Success(stats, _localizer);
    }

    // ══════════════════════════════════════════════════════════════════════
    // REVIEW (answer key revealed only after finalize)
    // ══════════════════════════════════════════════════════════════════════
    /// <inheritdoc />
    public async Task<Result<VideoExamReviewDto>> GetReviewAsync(
        long teacherId, long teacherStudentId, long videoAssetId)
    {
        var gate = await CheckAccessAsync(teacherId, teacherStudentId, videoAssetId);
        if (gate is not null)
            return Result<VideoExamReviewDto>.Failure(_localizer, gate.Value.ErrorKey, gate.Value.Status);

        var exam = await _unitOfWork.VideoAssetsRepo.GetExamWithQuestionsAsync(videoAssetId, teacherId);
        if (exam is null)
            return Result<VideoExamReviewDto>.Failure(
                _localizer, VideoConstants.Messages.VideoQuizNotFound, HttpStatusCode.NotFound);

        var report = await _unitOfWork.StudentVideoExamReportsRepo
            .GetReportWithAnswersAsync(videoAssetId, teacherStudentId);
        bool finalized = report?.SubmittedAt is not null;

        var answersByQuestion = report?.Answers.ToDictionary(a => a.VideoExamQuestionId)
                                 ?? new Dictionary<long, StudentVideoExamAnswer>();

        var dto = new VideoExamReviewDto
        {
            VideoAssetId = videoAssetId,
            VideoExamId = exam.Id,
            Title = exam.Title,
            Finalized = finalized,
            Status = report?.Status.ToString(),
            Score = finalized ? report!.Score : null,
            Percentage = finalized ? report!.Percentage : null,
            Stars = finalized ? _grading.ComputeStars(report!.Percentage) : null,
        };

        // PERF: batch-resolve every question-image gated URL in ONE query (was an N+1 —
        // TryBuildGatedUrlAsync per question inside the loop). A missing id maps to null, identical
        // to the single method; authorization is enforced on the actual /api/files/{id} fetch.
        var imageUrls = await _fileAccess.TryBuildGatedUrlsAsync(
            exam.Questions.Where(q => q.ImageFileId is not null).Select(q => q.ImageFileId!.Value));

        var questions = new List<VideoExamReviewQuestionDto>(exam.Questions.Count);
        foreach (var q in exam.Questions)
        {
            answersByQuestion.TryGetValue(q.Id, out var answer);
            var selectedIds = answer?.SelectedOptions.Select(o => o.VideoExamQuestionOptionId).ToHashSet()
                              ?? new HashSet<long>();

            questions.Add(new VideoExamReviewQuestionDto
            {
                QuestionId = q.Id,
                QuestionText = q.Text,
                QuestionType = q.QuestionType,
                Degree = VideoExamPointsPerQuestion,
                AwardedDegree = finalized ? answer?.AwardedDegree : null,
                ImageUrl = q.ImageFileId is long fid ? imageUrls.GetValueOrDefault(fid) : null,
                Options = q.Options.Select(o => new VideoExamReviewOptionDto
                {
                    OptionId = o.Id,
                    OptionText = o.Text,
                    IsSelected = selectedIds.Contains(o.Id),
                    IsCorrect = finalized ? o.IsCorrect : (bool?)null,
                }).ToList(),
            });
        }
        dto.Questions = questions;

        return Result<VideoExamReviewDto>.Success(dto, _localizer);
    }

    // ══════════════════════════════════════════════════════════════════════
    // PRIVATE HELPERS
    // ══════════════════════════════════════════════════════════════════════

    /// <summary>Every video question is worth one point (VideoExamQuestion carries no Degree column).</summary>
    private const decimal VideoExamPointsPerQuestion = 1m;

    /// <summary>
    /// Shared access gate for every student quiz call: module active (BR-ADM-010), video exists,
    /// and the student is in the video's scope (which also enforces Published/PublishDate — the
    /// canonical <c>IsStudentInVideoScopeAsync</c>). Returns null when access is granted.
    /// </summary>
    private async Task<(string ErrorKey, HttpStatusCode Status)?> CheckAccessAsync(
        long teacherId, long teacherStudentId, long videoAssetId)
    {
        bool active = await _unitOfWork.ModuleTeacherRepo!
            .IsModuleActiveAsync(teacherId, VideoConstants.ModuleName);
        if (!active)
            return (VideoConstants.Messages.ModuleDeactivated, HttpStatusCode.Forbidden);

        var video = await _unitOfWork.VideoAssetsRepo.GetVideoByIdAndTeacherAsync(videoAssetId, teacherId);
        if (video is null)
            return (VideoConstants.Messages.VideoNotFound, HttpStatusCode.NotFound);

        bool inScope = await _unitOfWork.VideoAssetsRepo
            .IsStudentInVideoScopeAsync(teacherStudentId, videoAssetId, teacherId);
        if (!inScope)
            return (VideoConstants.Messages.VideoNotInScope, HttpStatusCode.Forbidden);

        return null;
    }

    /// <summary>Builds the take-screen DTO (access + quiz existence already verified by the caller).</summary>
    private async Task<VideoExamTakeScreenDto> BuildTakeScreenDtoAsync(
        long teacherId, long teacherStudentId, long videoAssetId, VideoExamHeaderRow header)
    {
        var questions = await _unitOfWork.VideoAssetsRepo.GetStudentVideoExamQuestionsAsync(videoAssetId);
        // PERF: batch-resolve every question-image gated URL in ONE query (was an N+1).
        var imageUrls = await _fileAccess.TryBuildGatedUrlsAsync(
            questions.Where(q => q.ImageFileInternalId is not null).Select(q => q.ImageFileInternalId!.Value));
        foreach (var q in questions)
            q.ImageUrl = q.ImageFileInternalId is long fid ? imageUrls.GetValueOrDefault(fid) : null;

        var report = await _unitOfWork.StudentVideoExamReportsRepo
            .GetByVideoAndStudentAsync(videoAssetId, teacherStudentId);
        bool finalized = report?.SubmittedAt is not null;

        return new VideoExamTakeScreenDto
        {
            VideoAssetId = videoAssetId,
            VideoExamId = header.ExamId,
            Title = header.Title,
            Description = header.Description,
            QuestionsCount = questions.Count,
            TotalDegree = questions.Count * VideoExamPointsPerQuestion,
            Status = report?.Status.ToString(),
            CanRetake = report?.Status == StudentVideoExamStatus.Completed,
            LastPercentage = finalized ? report!.Percentage : null,
            LastScore = finalized ? report!.Score : null,
            Questions = questions.Select(q => new VideoExamStudentQuestionDto
            {
                QuestionId = q.Id,
                QuestionText = q.QuestionText,
                QuestionType = q.QuestionType,
                Degree = q.Degree,
                ImageUrl = q.ImageUrl,
                Options = q.Options.Select(o => new VideoExamStudentOptionDto
                {
                    OptionId = o.Id,
                    OptionText = o.OptionText,
                }).ToList(),
            }).ToList(),
        };
    }

    /// <summary>
    /// Tallies the stats shape from the graded per-question awards. Mirrors the online-exam
    /// grader's ComputeStats semantics (correct = full marks; wrong = answered but partial/zero;
    /// notAnswered = no answer), reusing the shared <c>ComputeStars</c> for the star rating.
    /// </summary>
    private VideoExamStatsDto BuildStats(
        int totalQuestions, IReadOnlyList<decimal> answeredAwards,
        decimal score, decimal totalDegree, StudentVideoExamStatus status)
    {
        int correct = answeredAwards.Count(a => a >= VideoExamPointsPerQuestion);
        int wrong = answeredAwards.Count(a => a < VideoExamPointsPerQuestion);
        int notAnswered = Math.Max(0, totalQuestions - answeredAwards.Count);
        decimal percentage = totalDegree > 0 ? Math.Round(score / totalDegree * 100, 2) : 0;

        return new VideoExamStatsDto
        {
            Percentage = percentage,
            Stars = _grading.ComputeStars(percentage),
            NotAnswered = notAnswered,
            Correct = correct,
            Wrong = wrong,
            Score = score,
            TotalDegree = totalDegree,
            Status = status.ToString(),
        };
    }
}
