using Edvanz.Domain.Entities.ShareProp;

namespace Edvanz.Domain.Entities;

/// <summary>
/// One selected option for a <see cref="StudentVideoExamAnswer"/> — the video-module mirror
/// of <see cref="StudentQuestionAnswerOption"/>. Multiple rows per answer for MultipleChoice
/// questions; exactly one for SingleChoice. FK to the answer is CASCADE (leaf of the report
/// tree). <see cref="VideoExamQuestionOptionId"/> is a plain cross-aggregate reference (no
/// navigation FK) so the teacher's exam replace-all is never blocked.
/// </summary>
public class StudentVideoExamAnswerOption : BaseEntity
{
    public long StudentVideoExamAnswerId { get; set; }
    public StudentVideoExamAnswer StudentVideoExamAnswer { get; set; } = null!;

    /// <summary>The selected <see cref="VideoExamQuestionOption"/> (plain reference, no FK).</summary>
    public long VideoExamQuestionOptionId { get; set; }
}
