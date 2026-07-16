using Edvanz.Domain.Entities.ShareProp;
using Edvanz.Domain.Enums;

namespace Edvanz.Domain.Entities;

/// <summary>
/// One question within a <see cref="VideoExam"/>. <see cref="QuestionType"/>
/// determines how many <see cref="Options"/> may be marked correct — enforced
/// in <c>VideoService.ValidateExamShape</c>, not by a DB CHECK constraint.
/// FK to VideoExam is CASCADE — deleting the exam removes its questions.
/// </summary>
public class VideoExamQuestion : BaseEntity
{
    public long ExamId { get; set; }
    public VideoExam Exam { get; set; } = null!;

    public string Text { get; set; } = null!;

    public VideoExamQuestionType QuestionType { get; set; }

    /// <summary>
    /// Optional FK to an image <see cref="FileObject"/> (registry) shown with the question.
    /// References <c>FileObject.Id</c>; gated read URL rebuilt from its <c>PublicId</c>.
    /// Teacher/assistant-scoped access.
    /// </summary>
    public long? ImageFileId { get; set; }

    /// <summary>Display order within the exam, 0-based.</summary>
    public int SortOrder { get; set; }

    public ICollection<VideoExamQuestionOption> Options { get; set; } = new List<VideoExamQuestionOption>();
}