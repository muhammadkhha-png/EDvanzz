using Edvanz.Domain.Entities.ShareProp;

namespace Edvanz.Domain.Entities;

/// <summary>
/// One selected option for a <see cref="StudentQuestionAnswer"/>. Multiple rows per
/// answer for MultipleChoice questions; exactly one for SingleChoice.
/// </summary>
public class StudentQuestionAnswerOption : BaseEntity
{
    public long StudentQuestionAnswerId { get; set; }
    public StudentQuestionAnswer StudentQuestionAnswer { get; set; } = null!;

    /// <summary>
    /// Cross-aggregate reference into the exam's option graph. NoAction on this FK
    /// specifically prevents a converging delete path (§2 — do-not-reintroduce #1).
    /// </summary>
    public long QuestionOptionId { get; set; }
    public OnlineExamQuestionOption QuestionOption { get; set; } = null!;
}