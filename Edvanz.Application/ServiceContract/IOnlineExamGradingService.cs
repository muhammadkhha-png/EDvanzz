using Edvanz.Application.Dtos;
using Edvanz.Domain.Entities;
using Edvanz.Domain.Interfaces;

namespace Edvanz.Application.ServiceContract;

/// <summary>§3.4 grading math (QB partial credit), §0-QC stars, §0 stats counts, and §3.5 window-end auto-finalize.</summary>
public interface IOnlineExamGradingService
{
    /// <summary>SingleChoice: full Degree or 0. MultipleChoice: QB partial credit, 2dp.</summary>
    decimal GradeQuestion(OnlineExamQuestionRow question, HashSet<long> selectedOptionIds);

    /// <summary>QC: 90→5, 75→4, 60→3, 40→2, &gt;0→1, 0→0.</summary>
    int ComputeStars(decimal percentage);

    /// <summary>correct = full marks; wrong = answered but &lt; full (partial folds to wrong); notAnswered = no answer row.</summary>
    OnlineExamStatsDto ComputeStats(
        IReadOnlyList<OnlineExamQuestionRow> questions,
        IReadOnlyList<StudentQuestionAnswer> answers,
        decimal score, decimal totalGrade);

    /// <summary>
    /// §3.5 window-end auto-finalize: an InProgress report past EndDateTime with ≥1
    /// answer finalizes to Passed/Failed from its accumulated Score. Lazy — called
    /// from every student read path that needs an accurate terminal state (S5, S6).
    /// No-op (returns false) if not InProgress or window still open. Swallows a
    /// losing concurrency race (another request already finalized it) as a no-op.
    /// </summary>
    Task<bool> TryAutoFinalizeAsync(OnlineExam exam, StudentOnlineExamReport report);
}