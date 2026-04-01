using System.Text.Json.Serialization;

namespace Edvanz.Domain.Enums;

/// <summary>
/// Identifies which method was used to record attendance.
/// REQ-ATT-006: Three supported methods.
/// REQ-ATT-007: All methods produce the same underlying record.
/// Stored as tinyint in the database.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum AttendanceMethod : byte
{
    /// <summary>
    /// Method 1: Tutor typed the student code into the search field.
    /// REQ-ATT-006 Method 1.
    /// </summary>
    ManualCode = 1,

    /// <summary>
    /// Method 2: Tutor selected students from the full list via checkboxes.
    /// REQ-ATT-006 Method 2.
    /// </summary>
    MultiSelect = 2,

    /// <summary>
    /// Method 3: Tutor scanned the student's barcode.
    /// REQ-ATT-006 Method 3 / REQ-ATT-012.
    /// </summary>
    BarcodeScan = 3
}