using System.Text.Json.Serialization;

namespace Edvanz.Domain.Enums;

/// <summary>
/// The type of academic assignment created by a tutor.
/// REQ-EXH-001: The system supports two assignment types — Exam and Homework.
/// Each type shares a common creation structure but differs in grading behavior
/// and recurrence options (REQ-EXH-009 vs REQ-EXH-010).
/// Stored as tinyint in the database.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum AssignmentType : byte
{
    /// <summary>
    /// An exam assignment. Always tracks both attendance status and a numeric grade
    /// (REQ-EXH-018). Recurrence options are One-Time or Monthly (REQ-EXH-010).
    /// Requires MaxGrade and PassingThreshold on the template (REQ-EXH-020/021).
    /// </summary>
    Exam = 0,

    /// <summary>
    /// A homework assignment. Tracking mode is selected per assignment —
    /// Completion Only or Graded (REQ-EXH-014). Recurrence options are
    /// One-Time, Every Session, or Every 2 Sessions (REQ-EXH-009).
    /// </summary>
    Homework = 1
}
