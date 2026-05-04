using System.Text.Json.Serialization;

namespace Edvanz.Domain.Enums;

/// <summary>
/// The recorded status of a single student against a single assignment occurrence.
/// This enum spans three workflows (exam, completion-only homework, graded homework)
/// and provides a single source of truth for grade-pending detection in the Grade Entry View.
///
/// REQ-EXH-016: Completion-only homework states — Pending, Done, NotDone.
/// REQ-EXH-017: Graded homework states — Pending, DoneWithGrade, DoneWithoutGrade, NotDone.
/// REQ-EXH-019: Exam states — Pending, AttendedWithGrade, Attended (without grade), DidNotAttend.
/// REQ-EXH-026-A: Grade Entry View filters by Status IN (Attended, DoneWithoutGrade).
///
/// VALUES ARE LOCKED. Changing a byte value after data exists is a destructive migration.
/// Bytes 8–15 are reserved for future statuses (e.g., ExcusedAbsence) and must not be used
/// without a design discussion.
///
/// Stored as tinyint in the database.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ObligationStatus : byte
{
    /// <summary>
    /// No status has been recorded yet.
    /// REQ-EXH-007: Default state for every newly created obligation, both exam and homework.
    /// </summary>
    Pending = 0,

    /// <summary>
    /// Completion-only homework: the student completed the assignment.
    /// REQ-EXH-016: Terminal state for CompletionOnly tracking mode — no grade is associated.
    /// </summary>
    Done = 1,

    /// <summary>
    /// Homework: the student did not complete the assignment.
    /// REQ-EXH-016 / REQ-EXH-017: Terminal state for both homework tracking modes.
    /// </summary>
    NotDone = 2,

    /// <summary>
    /// Exam: the student took the exam but a grade has not yet been entered.
    /// Surfaced in the Grade Entry View as "Attended — Grade Pending" (REQ-EXH-026).
    /// REQ-EXH-026-A: One of two states that the Grade Entry View filters on.
    /// </summary>
    Attended = 3,

    /// <summary>
    /// Exam: the student took the exam and a grade has been entered.
    /// REQ-EXH-019: Terminal state for an attended exam.
    /// </summary>
    AttendedWithGrade = 4,

    /// <summary>
    /// Exam: the student was absent from the exam.
    /// REQ-EXH-019: Terminal state for an absent exam.
    /// </summary>
    DidNotAttend = 5,

    /// <summary>
    /// Graded homework: the student completed the assignment but a grade has not yet been entered.
    /// Surfaced in the Grade Entry View as "Done — Grade Pending" (REQ-EXH-026-A).
    /// </summary>
    DoneWithoutGrade = 6,

    /// <summary>
    /// Graded homework: the student completed the assignment and a grade has been entered.
    /// REQ-EXH-017: Terminal state for completed graded homework.
    /// </summary>
    DoneWithGrade = 7
}
