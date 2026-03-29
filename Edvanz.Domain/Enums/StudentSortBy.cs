using System.Text.Json.Serialization;

namespace Edvanz.Domain.Enums;

/// <summary>
/// Valid columns to sort the teacher's student list by.
/// REQ-STU-SRT-001: Sorting by StudentName, StudentCode, or DateAdded.
/// REQ-STU-SRT-002: Default sort is DateAdded descending (newest first).
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum StudentSortBy
{
    /// <summary>
    /// Sort by the date the student was added (CreateAt).
    /// REQ-STU-SRT-002: Default sort — newest first.
    /// </summary>
    DateAdded,

    /// <summary>
    /// Sort alphabetically by student name.
    /// REQ-STU-SRT-001: Supports A→Z / Z→A and Arabic ordering.
    /// </summary>
    StudentName,

    /// <summary>
    /// Sort by student code value.
    /// REQ-STU-SRT-001: Ascending / descending.
    /// </summary>
    StudentCode
}