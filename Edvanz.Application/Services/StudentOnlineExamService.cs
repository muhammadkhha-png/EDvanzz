using Edvanz.Application.Dtos;
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

public class StudentOnlineExamService : IStudentOnlineExamService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IStringLocalizer<Messages> _localizer;
    private readonly IOnlineExamGradingService _grading;
    private readonly IFileAccessService _fileAccess;

    public StudentOnlineExamService(
        IUnitOfWork unitOfWork, IStringLocalizer<Messages> localizer, IOnlineExamGradingService grading,
        IFileAccessService fileAccess)
    {
        _unitOfWork = unitOfWork;
        _localizer = localizer;
        _grading = grading;
        _fileAccess = fileAccess;
    }

    // ══════════════════════════════════════════════════════════════════════
    // S1 — MY EXAMS (upcoming/past split, QD)
    // ══════════════════════════════════════════════════════════════════════
    public async Task<Result<StudentOnlineExamListDto>> GetMyExamsAsync(long teacherId, long teacherStudentId)
    {
        var (sessionId, groupId) = await _unitOfWork.OnlineExamsRepo.GetStudentSessionContextAsync(teacherStudentId);
        var exams = await _unitOfWork.OnlineExamsRepo.GetExamsAssignedToStudentAsync(teacherId, sessionId, groupId);

        if (exams.Count == 0)
            return Result<StudentOnlineExamListDto>.Success(new StudentOnlineExamListDto(), _localizer);

        // One grouped fetch for this student's reports across the candidate exam ids.
        var examIds = exams.Select(e => e.Id).ToList();
        var reports = await _unitOfWork.GetRepository<Domain.Entities.StudentOnlineExamReport, long>()
            .GetAsync(r => examIds.Contains(r.OnlineExamId) && r.TeacherStudentId == teacherStudentId);
        var reportByExam = reports.ToDictionary(r => r.OnlineExamId);

        var now = DateTime.UtcNow;
        var result = new StudentOnlineExamListDto();

        foreach (var exam in exams)
        {
            reportByExam.TryGetValue(exam.Id, out var report);
            bool finalized = report?.SubmittedAt is not null;

            var row = new OnlineExamStudentListItemDto
            {
                ExamId = exam.Id,
                ExamName = exam.Title,
                ExamDate = DateOnly.FromDateTime(exam.StartDateTime),
                ExamTime = TimeOnly.FromDateTime(exam.StartDateTime),
                Duration = exam.EndDateTime - exam.StartDateTime,
                QuestionsCount = exam.Questions.Count,
                ExamDegree = exam.Questions.Sum(q => q.Degree),
                StudentDegree = finalized ? report!.Score : null,
                StudentStatus = finalized ? report!.Status.ToString() : null,
            };

            // QD split: past = finalized ∨ End<now; upcoming = Published ∧ not finalized ∧ window open.
            if (finalized || exam.EndDateTime < now)
                result.Past.Add(row);
            else if (exam.Status == OnlineExamStatus.Published)
                result.Upcoming.Add(row);
            // else: Closed-but-never-finalized falls into neither bucket (§4 note — intentional per plan wording).
        }

        return Result<StudentOnlineExamListDto>.Success(result, _localizer);
    }

    // ══════════════════════════════════════════════════════════════════════
    // S2 — TAKE SCREEN (no IsCorrect)
    // ══════════════════════════════════════════════════════════════════════
    public async Task<Result<OnlineExamTakeScreenDto>> GetTakeScreenAsync(
        long teacherId, long teacherStudentId, long onlineExamId)
    {
        var exam = await _unitOfWork.OnlineExamsRepo.GetByIdAndTeacherAsync(onlineExamId, teacherId);
        if (exam is null || exam.Status != OnlineExamStatus.Published)
            return Result<OnlineExamTakeScreenDto>.Failure(_localizer, OnlineExamConstants.Messages.NotFound, HttpStatusCode.NotFound);

        bool isAssigned = await _unitOfWork.OnlineExamsRepo
            .BuildAssignedStudentIdsQuery(onlineExamId, teacherId)
            .AnyAsync(id => id == teacherStudentId);
        if (!isAssigned)
            return Result<OnlineExamTakeScreenDto>.Failure(_localizer, "OnlineExam.NotInScope", HttpStatusCode.Forbidden);

        var questions = await _unitOfWork.OnlineExamsRepo.GetQuestionsForStudentAsync(onlineExamId);
        foreach (var q in questions)
            q.ImageUrl = await _fileAccess.TryBuildGatedUrlAsync(q.ImageFileInternalId);

        var dto = new OnlineExamTakeScreenDto
        {
            ExamId = exam.Id,
            ExamName = exam.Title,
            Description = exam.Description,
            Instructions = exam.Instructions,
            StartDateTime = exam.StartDateTime,
            EndDateTime = exam.EndDateTime,
            ExamDegree = await _unitOfWork.OnlineExamsRepo.GetTotalDegreeAsync(onlineExamId),
            Questions = questions.ToList(),
        };

        return Result<OnlineExamTakeScreenDto>.Success(dto, _localizer);
    }

    // ══════════════════════════════════════════════════════════════════════
    // S6-read — REVIEW SKELETON (correct hidden until finalize; nothing to
    // reveal yet since S3/S4 land in Phase 5 — this is the read path only)
    // ══════════════════════════════════════════════════════════════════════
    public async Task<Result<OnlineExamReviewDto>> GetReviewAsync(
        long teacherId, long teacherStudentId, long onlineExamId)
    {
        var exam = await _unitOfWork.OnlineExamsRepo.GetByIdAndTeacherAsync(onlineExamId, teacherId);
        if (exam is null || exam.Status == OnlineExamStatus.Draft)
            return Result<OnlineExamReviewDto>.Failure(_localizer, OnlineExamConstants.Messages.NotFound, HttpStatusCode.NotFound);

        var report = await _unitOfWork.StudentOnlineExamReportsRepo.GetReportWithAnswersAsync(onlineExamId, teacherStudentId);

        // Allow review if currently assigned, OR if they have a report (case-3: since
        // unassigned but already attempted — they can still see their own review).
        if (report is null)
        {
            bool isAssigned = await _unitOfWork.OnlineExamsRepo
                .BuildAssignedStudentIdsQuery(onlineExamId, teacherId)
                .AnyAsync(id => id == teacherStudentId);
            if (!isAssigned)
                return Result<OnlineExamReviewDto>.Failure(_localizer, "OnlineExam.NotInScope", HttpStatusCode.Forbidden);
        }
        if (report is not null)
            await _grading.TryAutoFinalizeAsync(exam, report);


        bool finalized = report?.SubmittedAt is not null;

        // Dedicated projection per finalize state — IsCorrect only ever touches the
        // response when finalized=true (do-not-reintroduce #9).
        var dto = new OnlineExamReviewDto
        {
            ExamId = exam.Id,
            ExamName = exam.Title,
            Finalized = finalized,
            ReportStatus = report?.Status.ToString(),
            Score = finalized ? report!.Score : null,
            Percentage = finalized ? report!.Percentage : null,
        };

        var answersByQuestion = report?.Answers.ToDictionary(a => a.QuestionId) ?? new();

        if (finalized)
        {
            var teacherRows = await _unitOfWork.OnlineExamsRepo.GetQuestionsForTeacherAsync(onlineExamId);
            dto.Questions = teacherRows.Select(q =>
            {
                answersByQuestion.TryGetValue(q.Id, out var answer);
                var selectedIds = answer?.SelectedOptions.Select(o => o.QuestionOptionId).ToHashSet() ?? new HashSet<long>();

                return new OnlineExamReviewQuestionDto
                {
                    QuestionId = q.Id,
                    QuestionText = q.QuestionText,
                    QuestionType = q.QuestionType,
                    Degree = q.Degree,
                    AwardedDegree = answer?.AwardedDegree,
                    Options = q.Options.Select(o => new OnlineExamReviewOptionDto
                    {
                        OptionId = o.Id,
                        OptionText = o.OptionText,
                        IsSelected = selectedIds.Contains(o.Id),
                        IsCorrect = o.IsCorrect,
                    }).ToList(),
                };
            }).ToList();
        }
        else
        {
            var studentRows = await _unitOfWork.OnlineExamsRepo.GetQuestionsForStudentAsync(onlineExamId);
            dto.Questions = studentRows.Select(q =>
            {
                answersByQuestion.TryGetValue(q.Id, out var answer);
                var selectedIds = answer?.SelectedOptions.Select(o => o.QuestionOptionId).ToHashSet() ?? new HashSet<long>();

                return new OnlineExamReviewQuestionDto
                {
                    QuestionId = q.Id,
                    QuestionText = q.QuestionText,
                    QuestionType = q.QuestionType,
                    Degree = q.Degree,
                    AwardedDegree = null,
                    Options = q.Options.Select(o => new OnlineExamReviewOptionDto
                    {
                        OptionId = o.Id,
                        OptionText = o.OptionText,
                        IsSelected = selectedIds.Contains(o.Id),
                        IsCorrect = null,
                    }).ToList(),
                };
            }).ToList();
        }

        return Result<OnlineExamReviewDto>.Success(dto, _localizer);
    }
    // ══════════════════════════════════════════════════════════════════════
    // S4 — ANSWER-BY-ANSWER (never finalizes)
    // ══════════════════════════════════════════════════════════════════════
    public async Task<Result<OnlineExamStatsDto>> SubmitAnswerAsync(
        long teacherId, long teacherStudentId, long onlineExamId, SubmitOnlineExamAnswerRequest request)
    {
        var (exam, failure) = await ValidateSubmissionWindowAsync(teacherId, teacherStudentId, onlineExamId);
        if (failure is not null) return failure;

        var questions = await _unitOfWork.OnlineExamsRepo.GetQuestionsForTeacherAsync(onlineExamId);
        var question = questions.FirstOrDefault(q => q.Id == request.QuestionId);
        if (question is null)
            return Result<OnlineExamStatsDto>.Failure(_localizer, OnlineExamConstants.Messages.NotFound, HttpStatusCode.NotFound);

        var optionValidation = ValidateSelectedOptions(question, request.SelectedOptionIds);
        if (optionValidation is not null)
            return Result<OnlineExamStatsDto>.Failure(_localizer, optionValidation, HttpStatusCode.BadRequest);

        var report = await GetOrCreateReportAsync(teacherId, teacherStudentId, onlineExamId);
        var lockCheck = CheckNotLocked(report);
        if (lockCheck is not null)
            return Result<OnlineExamStatsDto>.Failure(_localizer, lockCheck, HttpStatusCode.Conflict);

        var selectedIds = request.SelectedOptionIds.Distinct().ToHashSet();
        decimal awarded = _grading.GradeQuestion(question, selectedIds);
        var utcNow = DateTime.UtcNow;

        await UpsertAnswerAsync(report.Id, request.QuestionId, awarded, selectedIds, utcNow);
        await _unitOfWork.SaveChangesAsync();

        var allAnswers = await _unitOfWork.StudentOnlineExamReportsRepo.GetAnswersForReportAsync(report.Id);
        decimal totalGrade = questions.Sum(q => q.Degree);
        decimal score = allAnswers.Sum(a => a.AwardedDegree);

        // Running total only — Status/SubmittedAt untouched (never finalizes, §3.5).
        report.Score = score;
        report.Percentage = totalGrade > 0 ? Math.Round(score / totalGrade * 100, 2) : 0;
        report.UpdatedAt = utcNow;
        await _unitOfWork.StudentOnlineExamReportsRepo.UpdateAsync(report);
        await _unitOfWork.SaveChangesAsync();

        var stats = _grading.ComputeStats(questions, allAnswers, score, totalGrade);
        return Result<OnlineExamStatsDto>.Success(stats, _localizer);
    }

    // ══════════════════════════════════════════════════════════════════════
    // S3 — BULK SUBMIT (finalize + lock)
    // ══════════════════════════════════════════════════════════════════════
    public async Task<Result<OnlineExamStatsDto>> SubmitExamAsync(
        long teacherId, long teacherStudentId, long onlineExamId, SubmitOnlineExamRequest request)
    {
        var (exam, failure) = await ValidateSubmissionWindowAsync(teacherId, teacherStudentId, onlineExamId);
        if (failure is not null) return failure;

        var questions = await _unitOfWork.OnlineExamsRepo.GetQuestionsForTeacherAsync(onlineExamId);
        var questionsById = questions.ToDictionary(q => q.Id);

        foreach (var a in request.Answers)
        {
            if (!questionsById.TryGetValue(a.QuestionId, out var q))
                return Result<OnlineExamStatsDto>.Failure(_localizer, OnlineExamConstants.Messages.NotFound, HttpStatusCode.BadRequest);

            var optionValidation = ValidateSelectedOptions(q, a.SelectedOptionIds);
            if (optionValidation is not null)
                return Result<OnlineExamStatsDto>.Failure(_localizer, optionValidation, HttpStatusCode.BadRequest);
        }

        var report = await GetOrCreateReportAsync(teacherId, teacherStudentId, onlineExamId);
        var lockCheck = CheckNotLocked(report);
        if (lockCheck is not null)
            return Result<OnlineExamStatsDto>.Failure(_localizer, lockCheck, HttpStatusCode.Conflict);

        bool ownsTransaction = !_unitOfWork.HasActiveTransaction;
        if (ownsTransaction) await _unitOfWork.BeginTransactionAsync();

        try
        {
            var utcNow = DateTime.UtcNow;

            foreach (var a in request.Answers)
            {
                var q = questionsById[a.QuestionId];
                var selected = a.SelectedOptionIds.Distinct().ToHashSet();
                decimal awarded = _grading.GradeQuestion(q, selected);
                await UpsertAnswerAsync(report.Id, a.QuestionId, awarded, selected, utcNow);
            }

            await _unitOfWork.SaveChangesAsync();

            var allAnswers = await _unitOfWork.StudentOnlineExamReportsRepo.GetAnswersForReportAsync(report.Id);
            decimal totalGrade = questions.Sum(q => q.Degree);
            decimal score = allAnswers.Sum(a => a.AwardedDegree);
            decimal percentage = totalGrade > 0 ? Math.Round(score / totalGrade * 100, 2) : 0;

            report.Score = score;
            report.Percentage = percentage;
            report.SubmittedAt = utcNow; // finalize + lock
            report.Status = percentage >= exam!.PassPercentage ? StudentOnlineExamStatus.Passed : StudentOnlineExamStatus.Failed;
            report.UpdatedAt = utcNow;
            await _unitOfWork.StudentOnlineExamReportsRepo.UpdateAsync(report);

            try
            {
                await _unitOfWork.SaveChangesAsync();
            }
            catch (Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException)
            {
                // Concurrent submit already finalized this report — RowVersion caught the race.
                if (ownsTransaction) await _unitOfWork.RollbackAsync();
                return Result<OnlineExamStatsDto>.Failure(_localizer, OnlineExamConstants.Messages.AlreadySubmitted, HttpStatusCode.Conflict);
            }

            if (ownsTransaction) await _unitOfWork.CommitAsync();

            var stats = _grading.ComputeStats(questions, allAnswers, score, totalGrade);
            return Result<OnlineExamStatsDto>.Success(stats, _localizer);
        }
        catch
        {
            if (ownsTransaction) await _unitOfWork.RollbackAsync();
            throw;
        }
    }

    // ══════════════════════════════════════════════════════════════════════
    // S5 — RESULT (triggers auto-finalize if window has closed)
    // ══════════════════════════════════════════════════════════════════════
    public async Task<Result<OnlineExamStatsDto>> GetResultAsync(
        long teacherId, long teacherStudentId, long onlineExamId)
    {
        var exam = await _unitOfWork.OnlineExamsRepo.GetByIdAndTeacherAsync(onlineExamId, teacherId);
        if (exam is null)
            return Result<OnlineExamStatsDto>.Failure(_localizer, OnlineExamConstants.Messages.NotFound, HttpStatusCode.NotFound);

        var report = await _unitOfWork.StudentOnlineExamReportsRepo.GetByExamAndStudentAsync(onlineExamId, teacherStudentId);
        if (report is null)
            return Result<OnlineExamStatsDto>.Failure(_localizer, OnlineExamConstants.Messages.ReportNotFound, HttpStatusCode.NotFound);

        await _grading.TryAutoFinalizeAsync(exam, report);

        var questions = await _unitOfWork.OnlineExamsRepo.GetQuestionsForTeacherAsync(onlineExamId);
        var answers = await _unitOfWork.StudentOnlineExamReportsRepo.GetAnswersForReportAsync(report.Id);
        decimal totalGrade = questions.Sum(q => q.Degree);

        var stats = _grading.ComputeStats(questions, answers, report.Score, totalGrade);
        return Result<OnlineExamStatsDto>.Success(stats, _localizer);
    }

    // ══════════════════════════════════════════════════════════════════════
    // PRIVATE HELPERS
    // ══════════════════════════════════════════════════════════════════════

    private async Task<(OnlineExam? Exam, Result<OnlineExamStatsDto>? Failure)> ValidateSubmissionWindowAsync(
        long teacherId, long teacherStudentId, long onlineExamId)
    {
        var exam = await _unitOfWork.OnlineExamsRepo.GetByIdAndTeacherAsync(onlineExamId, teacherId);
        if (exam is null || exam.Status != OnlineExamStatus.Published)
            return (null, Result<OnlineExamStatsDto>.Failure(_localizer, OnlineExamConstants.Messages.NotFound, HttpStatusCode.NotFound));

        var now = DateTime.UtcNow;
        if (now < exam.StartDateTime || now > exam.EndDateTime)
            return (null, Result<OnlineExamStatsDto>.Failure(_localizer, OnlineExamConstants.Messages.WindowClosed, HttpStatusCode.Conflict));

        bool isAssigned = await _unitOfWork.OnlineExamsRepo
            .BuildAssignedStudentIdsQuery(onlineExamId, teacherId)
            .AnyAsync(id => id == teacherStudentId);
        if (!isAssigned)
            return (null, Result<OnlineExamStatsDto>.Failure(_localizer, OnlineExamConstants.Messages.NotInScope, HttpStatusCode.Forbidden));

        return (exam, null);
    }

    private static string? ValidateSelectedOptions(Domain.Interfaces.OnlineExamQuestionRow question, List<long> selectedOptionIds)
    {
        var validOptionIds = question.Options.Select(o => o.Id).ToHashSet();
        var selected = selectedOptionIds.Distinct().ToHashSet();

        if (selected.Any(id => !validOptionIds.Contains(id)))
            return OnlineExamConstants.Messages.InvalidOptionSelection;

        if (question.QuestionType == OnlineExamQuestionType.SingleChoice && selected.Count != 1)
            return OnlineExamConstants.Messages.SingleChoiceNeedsExactlyOneCorrect;

        if (question.QuestionType == OnlineExamQuestionType.MultipleChoice && selected.Count == 0)
            return OnlineExamConstants.Messages.MultipleChoiceNeedsAtLeastOneCorrect;

        return null;
    }

    private static string? CheckNotLocked(StudentOnlineExamReport report)
    {
        if (report.Status == StudentOnlineExamStatus.Blocked)
            return OnlineExamConstants.Messages.StudentBlocked;

        if (report.SubmittedAt is not null) // locked — Passed or Failed already
            return OnlineExamConstants.Messages.AlreadySubmitted;

        return null;
    }

    private async Task<StudentOnlineExamReport> GetOrCreateReportAsync(long teacherId, long teacherStudentId, long onlineExamId)
    {
        var report = await _unitOfWork.StudentOnlineExamReportsRepo.GetByExamAndStudentAsync(onlineExamId, teacherStudentId);
        if (report is not null) return report;

        report = new StudentOnlineExamReport
        {
            OnlineExamId = onlineExamId,
            TeacherStudentId = teacherStudentId,
            TeacherId = teacherId,
            Status = StudentOnlineExamStatus.InProgress,
            Score = 0,
            Percentage = 0,
            CreateAt = DateTime.UtcNow,
        };
        await _unitOfWork.StudentOnlineExamReportsRepo.AddAsync(report);
        await _unitOfWork.SaveChangesAsync(); // need report.Id for child answers
        return report;
    }

    private async Task UpsertAnswerAsync(
        long reportId, long questionId, decimal awardedDegree, HashSet<long> selectedIds, DateTime utcNow)
    {
        var existing = await _unitOfWork.StudentOnlineExamReportsRepo.GetAnswerByReportAndQuestionAsync(reportId, questionId);

        if (existing is null)
        {
            await _unitOfWork.StudentOnlineExamReportsRepo.AddAnswerAsync(new StudentQuestionAnswer
            {
                StudentReportId = reportId,
                QuestionId = questionId,
                AwardedDegree = awardedDegree,
                CreateAt = utcNow,
                SelectedOptions = selectedIds.Select(id => new StudentQuestionAnswerOption
                {
                    QuestionOptionId = id,
                    CreateAt = utcNow,
                }).ToList(),
            });
        }
        else
        {
            existing.AwardedDegree = awardedDegree; // re-answer overwrites
            await _unitOfWork.StudentOnlineExamReportsRepo.ReplaceAnswerOptionsAsync(
                existing.Id,
                selectedIds.Select(id => new StudentQuestionAnswerOption
                {
                    StudentQuestionAnswerId = existing.Id,
                    QuestionOptionId = id,
                    CreateAt = utcNow,
                }).ToList());
        }
    }
}