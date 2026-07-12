using Edvanz.Domain.Entities.ShareProp;

namespace Edvanz.Domain.Entities;

/// <summary>
/// One answer option for a <see cref="VideoExamQuestion"/>. <see cref="IsCorrect"/>
/// is the answer key — SingleChoice questions have exactly one correct option;
/// MultipleChoice questions have one or more, both enforced at the application
/// layer. FK to VideoExamQuestion is CASCADE.
/// </summary>
public class VideoExamQuestionOption : BaseEntity
{
    public long QuestionId { get; set; }
    public VideoExamQuestion Question { get; set; } = null!;

    public string Text { get; set; } = null!;

    public bool IsCorrect { get; set; }

    /// <summary>Display order within the question, 0-based.</summary>
    public int SortOrder { get; set; }
}