using System.Text.Json.Serialization;

namespace Edvanz.Domain.Enums;

/// <summary>
/// Represents the attendance status of a student for a specific session occurrence.
/// REQ-ATT-006: Unified across all three attendance methods.
/// REQ-ATT-017: CrossSessionPresent for cross-session attendance events.
/// Stored as tinyint in the database.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum AttendanceStatus : byte
{
    /// <summary>
    /// Student was absent for this session occurrence.
    /// REQ-ATT-024: Can be changed to Present via Edit Attendance.
    /// </summary>
    Absent = 0,

    /// <summary>
    /// Student was present for this session occurrence.
    /// REQ-ATT-006: Recorded via manual code, multi-select, or barcode scan.
    /// </summary>
    Present = 1,

    /// <summary>
    /// Student attended a different linked session to fulfill this occurrence.
    /// REQ-ATT-017: Cross-session attendance event capturing both assigned and attended sessions.
    /// </summary>
    CrossSessionPresent = 2,

    /// <summary>
    ///     /// FIX 4.1: Student placed on "hold" during attendance taking.
    ///     /// REQ-ATT-061: Distinguishes held students from marked and unmarked.
    ///     /// REQ-ATT-058: "Hold" cancels attendance recording, deferring to later.
    /// </summary>
    Held = 3
}