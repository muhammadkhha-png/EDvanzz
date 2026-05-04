using System.Text.Json.Serialization;

namespace Edvanz.Domain.Enums;

/// <summary>
/// The recurrence pattern of an assignment template.
/// REQ-EXH-008: The tutor configures each assignment as one-time or recurring during creation.
/// REQ-EXH-009: Homework supports OneTime, EverySession, EveryTwoSessions.
/// REQ-EXH-010: Exam supports OneTime or Monthly.
/// REQ-EXH-013: Pattern is not editable after the first occurrence has student data.
/// Stored as tinyint in the database.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum RecurrencePattern : byte
{
    /// <summary>
    /// Assignment is created once for its single defined date.
    /// REQ-EXH-009 / REQ-EXH-010: Default for both exams and homework.
    /// </summary>
    OneTime = 0,

    /// <summary>
    /// Exam recurs once per calendar month on a date or session occurrence
    /// defined by the tutor. Exam-only.
    /// REQ-EXH-010: Monthly recurrence option for exams.
    /// </summary>
    Monthly = 1,

    /// <summary>
    /// Homework recurs on every scheduled session occurrence of the targeted
    /// session(s). Homework-only.
    /// REQ-EXH-009: Every Session option for homework.
    /// </summary>
    EverySession = 2,

    /// <summary>
    /// Homework recurs once every two scheduled session occurrences of the
    /// targeted session(s). Homework-only.
    /// REQ-EXH-009: Every 2 Sessions option for homework.
    /// </summary>
    EveryTwoSessions = 3
}
