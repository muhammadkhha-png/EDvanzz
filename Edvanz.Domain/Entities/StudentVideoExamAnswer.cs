using Edvanz.Domain.Entities.ShareProp;

namespace Edvanz.Domain.Entities;

/// <summary>
/// One question's answer within a <see cref="StudentVideoExamReport"/> — the video-module
/// mirror of <see cref="StudentQuestionAnswer"/>. FK to the report is CASCADE (leaf of the
/// video-quiz report tree; removed with the report on a video delete / retry wipe).
///
/// <see cref="VideoExamQuestionId"/> is a plain cross-aggregate reference (no navigation FK)
/// so the teacher's exam replace-all (which deletes the whole <see cref="VideoExam"/> tree)
/// is never blocked by an outstanding student answer.
/// </summary>
public class StudentVideoExamAnswer : BaseEntity
{
    public long StudentVideoExamReportId { get; set; }
    public StudentVideoExamReport StudentVideoExamReport { get; set; } = null!;

    /// <summary>The answered <see cref="VideoExamQuestion"/> (plain reference, no FK — see class remarks).</summary>
    public long VideoExamQuestionId { get; set; }

    /// <summary>Per-question score. SingleChoice: 0 or 1. MultipleChoice: partial credit (shared grader).</summary>
    public decimal AwardedDegree { get; set; }

    public ICollection<StudentVideoExamAnswerOption> SelectedOptions { get; set; } = new List<StudentVideoExamAnswerOption>();
}
