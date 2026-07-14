using Edvanz.Domain.Entities.ShareProp;

namespace Edvanz.Domain.Entities;

/// <summary>
/// One question's answer within a <see cref="StudentOnlineExamReport"/>.
/// <see cref="AwardedDegree"/> is updated on every write (not just at finalize) —
/// this is what makes the answer-by-answer running-total flow (S4) possible without
/// re-grading the whole report on every call (§3.5).
/// </summary>
public class StudentQuestionAnswer : BaseEntity
{
    public long StudentReportId { get; set; }
    public StudentOnlineExamReport StudentReport { get; set; } = null!;

    /// <summary>
    /// Cross-aggregate reference into the exam's question graph. Read-only from this
    /// side — no back-collection on <c>OnlineExamQuestion</c> (keeps the two
    /// aggregates decoupled per §1's "own aggregate root" rule).
    /// </summary>
    public long QuestionId { get; set; }
    public OnlineExamQuestion Question { get; set; } = null!;

    /// <summary>Per-question score. SingleChoice: 0 or full Degree. MultipleChoice: partial credit (§3.4/QB).</summary>
    public decimal AwardedDegree { get; set; }

    public ICollection<StudentQuestionAnswerOption> SelectedOptions { get; set; } = new List<StudentQuestionAnswerOption>();
}