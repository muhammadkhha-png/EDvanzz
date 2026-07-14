using Edvanz.Domain.Entities.ShareProp;

namespace Edvanz.Domain.Entities;

/// <summary>
/// One answer option for an <see cref="OnlineExamQuestion"/>.
/// <see cref="IsCorrect"/> is the answer key — must never be projected to students
/// before their report is finalized (do-not-reintroduce #9; see S2 vs. S6 in §4).
/// </summary>
public class OnlineExamQuestionOption : BaseEntity
{
    public long QuestionId { get; set; }
    public OnlineExamQuestion Question { get; set; } = null!;

    public string OptionText { get; set; } = null!;

    public bool IsCorrect { get; set; }

    /// <summary>Display order within the question, 0-based.</summary>
    public int SortOrder { get; set; }
}