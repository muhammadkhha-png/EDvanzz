using Edvanz.Domain.Entities.ShareProp;
using Edvanz.Domain.Enums;
using System.ComponentModel.DataAnnotations.Schema;

namespace Edvanz.Domain.Entities;

/// <summary>
/// Audit trail entry for an attendance record modification.
/// REQ-ATT-025: All edits saved with edit timestamp, differentiating original from modified.
/// BR-ATT-006: Editing does not alter the original RecordedAt. Edits logged here alongside original.
/// REQ-ATT-024: Supports changing Present↔Absent, adding missed records, removing erroneous entries.
/// Cascade-deleted when the parent AttendanceRecord is removed.
/// </summary>
public class AttendanceEditLog : BaseEntity
{
    /// <summary>
    /// Foreign key to the attendance record that was edited.
    /// CASCADE on delete: edit logs are meaningless without the parent record.
    /// </summary>
    [ForeignKey(nameof(AttendanceRecord))]
    public long AttendanceRecordId { get; set; }
    public AttendanceRecord AttendanceRecord { get; set; } = null!;

    /// <summary>
    /// The attendance status before this edit was applied.
    /// </summary>
    public AttendanceStatus PreviousStatus { get; set; }

    /// <summary>
    /// The attendance status after this edit was applied.
    /// </summary>
    public AttendanceStatus NewStatus { get; set; }

    /// <summary>
    /// The attendance method before this edit (may change if originally scanned but corrected manually).
    /// </summary>
    public AttendanceMethod PreviousAttendanceMethod { get; set; }

    /// <summary>
    /// The attendance method after this edit.
    /// </summary>
    public AttendanceMethod NewAttendanceMethod { get; set; }

    /// <summary>
    /// When the edit was made.
    /// REQ-ATT-025: Edit timestamp preserved for audit purposes.
    /// </summary>
    public DateTime EditedAt { get; set; }

    /// <summary>
    /// Who made the edit. FK to Users table. Nullable for system-generated corrections.
    /// </summary>
    public long? EditedByUserId { get; set; }

    /// <summary>
    /// Optional reason or note for the edit.
    /// Supports audit trail clarity for historical review.
    /// </summary>
    public string? EditReason { get; set; }
}