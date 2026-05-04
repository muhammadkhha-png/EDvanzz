using System.Text.Json.Serialization;

namespace Edvanz.Domain.Enums;

/// <summary>
/// The tracking mode for a homework assignment.
/// REQ-EXH-014: Homework supports two tracking modes selectable during creation.
/// REQ-EXH-015: Default is CompletionOnly unless the tutor selects Graded.
/// Null for exam templates — exams always track both completion and grade (REQ-EXH-018).
/// Stored as tinyint in the database.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum HomeworkTrackingMode : byte
{
    /// <summary>
    /// Each student's homework status is recorded simply as Done or Not Done
    /// with no numeric grade associated.
    /// REQ-EXH-016: States are Pending, Done, NotDone.
    /// </summary>
    CompletionOnly = 0,

    /// <summary>
    /// Each student's homework status is recorded as Done or Not Done and a
    /// numeric grade is entered for students who completed it.
    /// REQ-EXH-017: States are Pending, DoneWithGrade, DoneWithoutGrade, NotDone.
    /// </summary>
    Graded = 1
}
