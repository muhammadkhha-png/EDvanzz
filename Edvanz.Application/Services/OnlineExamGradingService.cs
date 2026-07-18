using Edvanz.Application.Dtos;
using Edvanz.Application.ServiceContract;
using Edvanz.Domain.Entities;
using Edvanz.Domain.Enums;
using Edvanz.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Edvanz.Application.Services;

public class OnlineExamGradingService : IOnlineExamGradingService
{
    private readonly IUnitOfWork _unitOfWork;

    public OnlineExamGradingService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    /// <inheritdoc />
    public decimal GradeQuestion(
        bool isMultipleChoice, ISet<long> correctOptionIds, ISet<long> selectedOptionIds, decimal degree)
    {
        if (!isMultipleChoice)
        {
            // Defensive symmetry with the multiple-choice branch below: a malformed single-choice
            // question authored with ZERO correct options must never award full marks via
            // SetEquals(∅, ∅). Valid data always has exactly one correct option, so this guard is
            // inert for well-formed questions — online-exam scores are unchanged.
            if (correctOptionIds.Count == 0) return 0m;

            // SingleChoice — all-or-nothing: full degree iff the selection is exactly the
            // correct set. Under the single-choice invariant (exactly one correct option and
            // exactly one selected option, enforced by option validation BEFORE grading) this
            // is identical to the legacy "the one correct option was picked" rule — online-exam
            // scores are unchanged (do not alter results).
            return selectedOptionIds.SetEquals(correctOptionIds) ? degree : 0m;
        }

        // MultipleChoice — QB partial credit: Degree × max(0, (|S∩C| − |S−C|) / |C|), 2dp.
        if (correctOptionIds.Count == 0) return 0m;

        int correctPicks = selectedOptionIds.Count(id => correctOptionIds.Contains(id));
        int wrongPicks = selectedOptionIds.Count(id => !correctOptionIds.Contains(id));

        decimal raw = degree * Math.Max(0m, (decimal)(correctPicks - wrongPicks) / correctOptionIds.Count);
        return Math.Round(raw, 2);
    }

    /// <inheritdoc />
    public decimal GradeQuestion(Domain.Interfaces.OnlineExamQuestionRow question, HashSet<long> selectedOptionIds)
    {
        // Online-exam adapter over the DTO-free primitive grader (the single source of truth,
        // also reused by the video-quiz module). Behavior is byte-for-byte unchanged.
        var correctIds = question.Options.Where(o => o.IsCorrect).Select(o => o.Id).ToHashSet();
        return GradeQuestion(
            question.QuestionType == OnlineExamQuestionType.MultipleChoice,
            correctIds,
            selectedOptionIds,
            question.Degree);
    }

    /// <inheritdoc />
    public int ComputeStars(decimal percentage)
    {
        if (percentage >= 90) return 5;
        if (percentage >= 75) return 4;
        if (percentage >= 60) return 3;
        if (percentage >= 40) return 2;
        if (percentage > 0) return 1;
        return 0;
    }

    /// <inheritdoc />
    public OnlineExamStatsDto ComputeStats(
        IReadOnlyList<Domain.Interfaces.OnlineExamQuestionRow> questions,
        IReadOnlyList<StudentQuestionAnswer> answers,
        decimal score, decimal totalGrade)
    {
        var answeredByQuestion = answers.ToDictionary(a => a.QuestionId);
        int correct = 0, wrong = 0, notAnswered = 0;

        foreach (var q in questions)
        {
            if (!answeredByQuestion.TryGetValue(q.Id, out var answer))
            {
                notAnswered++;
                continue;
            }
            if (answer.AwardedDegree >= q.Degree) correct++;
            else wrong++;
        }

        decimal percentage = totalGrade > 0 ? Math.Round(score / totalGrade * 100, 2) : 0;

        return new OnlineExamStatsDto
        {
            Percentage = percentage,
            Stars = ComputeStars(percentage),
            NotAnswered = notAnswered,
            Correct = correct,
            Wrong = wrong,
        };
    }

    /// <inheritdoc />
    public async Task<bool> TryAutoFinalizeAsync(OnlineExam exam, StudentOnlineExamReport report)
    {
        if (report.Status != StudentOnlineExamStatus.InProgress) return false;
        if (DateTime.UtcNow <= exam.EndDateTime) return false;

        var answers = await _unitOfWork.StudentOnlineExamReportsRepo.GetAnswersForReportAsync(report.Id);
        // Defensive only: a report can't exist InProgress with zero answers via #4's
        // lazy-create path (creation always accompanies the first answer), and #5s
        // (T5s, Phase 6) creates it as Blocked, not InProgress. Kept as a guard rail.
        if (answers.Count == 0) return false;

        decimal totalGrade = await _unitOfWork.OnlineExamsRepo.GetTotalDegreeAsync(exam.Id);
        decimal score = answers.Sum(a => a.AwardedDegree);
        decimal percentage = totalGrade > 0 ? Math.Round(score / totalGrade * 100, 2) : 0;

        report.Score = score;
        report.Percentage = percentage;
        report.SubmittedAt = DateTime.UtcNow;
        report.Status = percentage >= exam.PassPercentage ? StudentOnlineExamStatus.Passed : StudentOnlineExamStatus.Failed;
        report.UpdatedAt = DateTime.UtcNow;

        await _unitOfWork.StudentOnlineExamReportsRepo.UpdateAsync(report);

        try
        {
            await _unitOfWork.SaveChangesAsync();
            return true;
        }
        catch (DbUpdateConcurrencyException)
        {
            // Another concurrent read already auto-finalized this report — fine, not an error.
            return false;
        }
    }
}