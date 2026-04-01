using System.Text.Json.Serialization;

namespace Edvanz.Domain.Enums;

/// <summary>
/// Tracks the attendance-taking progress for a session occurrence.
/// REQ-ATT-049: Dashboard shows completed vs. pending sessions.
/// REQ-ATT-051: Color-coded session cards based on this status.
/// Stored as tinyint in the database.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum OccurrenceStatus : byte
{
    /// <summary>
    /// Attendance has not started for this occurrence.
    /// REQ-ATT-051: Red for not yet started despite being today's session.
    /// </summary>
    Pending = 0,

    /// <summary>
    /// Attendance is partially recorded — some students marked, some not.
    /// REQ-ATT-051: Amber for partially completed.
    /// </summary>
    InProgress = 1,

    /// <summary>
    /// All students have been marked for this occurrence.
    /// REQ-ATT-051: Green for fully completed.
    /// </summary>
    Completed = 2
}