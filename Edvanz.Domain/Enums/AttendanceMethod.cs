using System.Text.Json.Serialization;

namespace Edvanz.Domain.Enums;

/// <summary>
/// Defines how an attendance record was created.
/// REQ-ATT-006: Three primary attendance-taking methods.
/// REQ-ATT-007: All methods operate on the same underlying record.
/// REQ-ATT-025: Edits are tracked separately via AttendanceEditLogs.
/// Stored as tinyint in the database.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum AttendanceMethod
{
    /// <summary>
    /// Attendance recorded by typing the student's code into the search/input field.
    /// REQ-ATT-006 Method 1: Manual Code Entry.
    /// REQ-ATT-039: Must complete in under 500 milliseconds.
    /// </summary>
    ManualCode = 1,

    /// <summary>
    /// Attendance recorded by selecting one or more students from the student list.
    /// REQ-ATT-006 Method 2: Multi-Select from Student List.
    /// </summary>
    MultiSelect = 2,

    /// <summary>
    /// Attendance recorded by scanning the student's barcode.
    /// REQ-ATT-006 Method 3: Barcode Scanning.
    /// REQ-ATT-038: Must complete in under 1 second.
    /// </summary>
    BarcodeScan = 3,

    /// <summary>
    /// Attendance record was created or modified via the Edit Attendance screen.
    /// REQ-ATT-023/024: Distinct from Take Attendance; allows past/future date editing.
    /// </summary>
    ManualEdit = 4,

    /// <summary>
    /// Attendance recorded via the "Mark All Present" bulk action.
    /// REQ-ATT-055: Single action marks all unmarked students as present.
    /// </summary>
    MarkAllPresent = 5
}