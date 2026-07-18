using Edvanz.Application.Dtos;
using Edvanz.Domain.Entities;
using Edvanz.Domain.Interfaces;

namespace Edvanz.Application.ServiceContract;

/// <summary>§3.4 grading math (QB partial credit), §0-QC stars, §0 stats counts, and §3.5 window-end auto-finalize.</summary>
public interface IOnlineExamGradingService
{
    /// <summary>
    /// DTO-free primitive grader — the single source of truth for BOTH online-exam and
    /// video-quiz scoring. <paramref name="isMultipleChoice"/> = <c>false</c> → SingleChoice
    /// all-or-nothing (full <paramref name="degree"/> iff <paramref name="selectedOptionIds"/>
    /// equals <paramref name="correctOptionIds"/>); <c>true</c> → MultipleChoice QB partial
    /// credit (<c>Degree × max(0, (|S∩C| − |S−C|) / |C|)</c>, rounded to 2dp). Takes only
    /// primitives/sets so other modules can reuse it without referencing any online-exam DTO.
    /// </summary>
    decimal GradeQuestion(bool isMultipleChoice, ISet<long> correctOptionIds, ISet<long> selectedOptionIds, decimal degree);

    /// <summary>
    /// SingleChoice: full Degree or 0. MultipleChoice: QB partial credit, 2dp. Thin online-exam
    /// adapter over the primitive <see cref="GradeQuestion(bool, ISet{long}, ISet{long}, decimal)"/>.
    /// </summary>
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