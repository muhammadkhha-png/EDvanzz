using System.Text.Json.Serialization;

namespace Edvanz.Domain.Enums;

/// <summary>
/// Defines the recurrence pattern for a session.
/// REQ-SES-007: Weekly, Bi-Weekly, or Monthly occurrence types.
/// Stored as tinyint in the database.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum OccurrenceType
{
    /// <summary>
    /// Session recurs every week on specific days.
    /// REQ-SES-007/008: Tutor selects 1–7 days of the week.
    /// </summary>
    Weekly = 1,

    /// <summary>
    /// Session recurs every two weeks on specific days.
    /// REQ-SES-007/008: Same day-of-week selection as Weekly.
    /// </summary>
    BiWeekly = 2,

    /// <summary>
    /// Session recurs once per month on a specific date or day pattern.
    /// REQ-SES-007: Defined by the tutor.
    /// </summary>
    Monthly = 3
}