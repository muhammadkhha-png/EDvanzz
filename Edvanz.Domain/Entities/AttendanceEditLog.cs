using Edvanz.Domain.Entities.ShareProp;
using Edvanz.Domain.Enums;
using System.ComponentModel.DataAnnotations.Schema;

namespace Edvanz.Domain.Entities;

/// <summary>
/// Audit trail entry for an attendance record modification.
/// REQ-ATT-025: All edits saved with edit timestamp, differentiating original from modified.
/// BR-ATT-006: Editing does not alter the original RecordedAt. Edits logged here alongside original.
/// REQ-ATT-024: Supports changing Present↔Absent, adding missed records, removing erroneous entries.
///
/// Audit Fix: AttendanceRecordId changed to nullable (long?) with SetNull FK.
/// When a parent AttendanceRecord is deleted (REQ-ATT-024: remove erroneous entries),
/// the edit logs survive with AttendanceRecordId = null, preserving the audit trail
/// as required by BR-ATT-006.
/// </summary>
public class AttendanceEditLog : BaseEntity
{
    /// <summary>
    /// Foreign key to the attendance record that was edited.
    /// Audit Fix: Changed from long to long? with SetNull FK behavior.
    /// When parent record is deleted, this becomes null but the log survives (BR-ATT-006).
    /// </summary>
    [ForeignKey(nameof(AttendanceRecord))]
    public long? AttendanceRecordId { get; set; }

    /// <summary>
    /// Navigation to the parent attendance record. Nullable after record deletion.
    /// </summary>
    public AttendanceRecord? AttendanceRecord { get; set; }

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