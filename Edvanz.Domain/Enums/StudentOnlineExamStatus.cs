namespace Edvanz.Domain.Enums;

/// <summary>
/// Status of a <see cref="Entities.StudentOnlineExamReport"/>. <c>NotAttended</c> is
/// deliberately NOT a member here — it is a virtual/derived state (assigned student
/// with no report row at all), never persisted (§0 Q7, §3.2 — do-not-reintroduce #7).
/// Numeric values are part of the persisted contract — never reorder.
/// </summary>
public enum StudentOnlineExamStatus : byte
{
    /// <summary>Report row exists; not yet finalized (no answers, or answer-by-answer in progress).</summary>
    InProgress = 1,

    Passed = 2,
    Failed = 3,

    /// <summary>Manual teacher/front-end action only (Q2) — no proctoring/auto-block.</summary>
    Blocked = 4
}