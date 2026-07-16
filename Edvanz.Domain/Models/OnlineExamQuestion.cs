using Edvanz.Domain.Entities.ShareProp;
using Edvanz.Domain.Enums;

namespace Edvanz.Domain.Entities;

/// <summary>
/// One question within an <see cref="OnlineExam"/>. Editable only while the parent
/// exam is Draft (Q5, enforced in the Application layer — not a DB constraint since
/// it depends on the parent's mutable Status).
/// </summary>
public class OnlineExamQuestion : BaseEntity
{
    public long OnlineExamId { get; set; }
    public OnlineExam OnlineExam { get; set; } = null!;

    public string QuestionText { get; set; } = null!;

    /// <summary>
    /// Optional FK to an image <see cref="FileObject"/> (registry) shown with the question.
    /// References <c>FileObject.Id</c>; the gated read URL is rebuilt from its <c>PublicId</c>.
    /// Visible to the exam's teacher/assistant and to students assigned to the exam.
    /// </summary>
    public long? ImageFileId { get; set; }

    public OnlineExamQuestionType QuestionType { get; set; }

    /// <summary>
    /// Marks for this question. decimal(6,2) — wider than the report's decimal(6,2)
    /// score column is not needed, but wider than (5,2) to stay overflow-safe if a
    /// teacher sets a high per-question weight (§1 "overflow-safe" note).
    /// <c>CK_OnlineExamQuestions_DegreePositive</c> enforces &gt; 0.
    /// </summary>
    public decimal Degree { get; set; }

    /// <summary>Display order within the exam, 0-based.</summary>
    public int SortOrder { get; set; }

    public ICollection<OnlineExamQuestionOption> Options { get; set; } = new List<OnlineExamQuestionOption>();
}