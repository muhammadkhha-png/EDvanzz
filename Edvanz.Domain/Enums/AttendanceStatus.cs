using System.Text.Json.Serialization;

namespace Edvanz.Domain.Enums;

/// <summary>
/// Defines the possible attendance statuses for a student at a session occurrence.
/// REQ-ATT-006: Supports Present and Absent as core statuses.
/// REQ-ATT-061: "Held" status for students deferred during attendance-taking.
/// Stored as tinyint in the database.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum AttendanceStatus
{
    /// <summary>
    /// Student was marked as present for the session occurrence.
    /// REQ-ATT-030: Resets the consecutive absence counter to zero.
    /// </summary>
    Present = 1,

    /// <summary>
    /// Student was absent for the session occurrence.
    /// REQ-ATT-029: Increments the consecutive absence counter.
    /// REQ-ATT-021: Increments the cumulative total absence count.
    /// </summary>
    Absent = 2,

    /// <summary>
    /// Student's attendance is on hold — deferred during the attendance-taking session.
    /// REQ-ATT-061: Tutor selected "Hold" on the absent-student confirmation prompt.
    /// The student can be returned to and processed later in the same session.
    /// </summary>
    Held = 3
}