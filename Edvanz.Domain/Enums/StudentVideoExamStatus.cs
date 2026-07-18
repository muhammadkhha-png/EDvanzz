namespace Edvanz.Domain.Enums;

/// <summary>
/// Lifecycle status of a <see cref="Entities.StudentVideoExamReport"/> — the student's
/// attempt at a video's quiz (<see cref="Entities.VideoExam"/>).
///
/// Deliberately simpler than <see cref="StudentOnlineExamStatus"/>: a video quiz has NO
/// pass/fail threshold (<see cref="Entities.VideoExam"/> carries no PassPercentage) and no
/// proctoring/block, so the only states are "attempt open" and "attempt submitted". A
/// student may RETAKE — <c>Completed</c> → (retry) → <c>InProgress</c> — overwriting the
/// prior answers/score (video-quiz only; online exams never retake).
///
/// Numeric values are part of the persisted contract — never reorder.
/// </summary>
public enum StudentVideoExamStatus : byte
{
    /// <summary>Report row exists; a fresh attempt is open (no submitted answers yet).</summary>
    InProgress = 1,

    /// <summary>The student has submitted (finalized) this attempt; <c>SubmittedAt</c> is set.</summary>
    Completed = 2,
}
