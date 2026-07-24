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
    Blocked = 4,

    /// <summary>
    /// Assigned student who never attempted the exam and the availability window has CLOSED
    /// (now &gt; EndDateTime). Like <c>NotAttended</c> this is VIRTUAL/derived — it is synthesized
    /// on read (student result + my-exams list) and is NEVER persisted to a report row (no write
    /// path — submit, TryAutoFinalize, teacher status PATCH, lazy-create — ever sets it). Appended
    /// as 5 so the 1–4 persisted contract is untouched.
    /// </summary>
    Missed = 5
}