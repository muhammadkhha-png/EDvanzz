using Edvanz.Domain.Entities.ShareProp;
using Edvanz.Domain.Enums;
using System.ComponentModel.DataAnnotations.Schema;

namespace Edvanz.Domain.Entities;

/// <summary>
/// Audit trail for attendance record modifications.
/// REQ-ATT-025: All edits saved with edit timestamp, differentiating original from modified records.
/// BR-ATT-006: Editing past attendance records does not alter the original record's timestamp.
///             The original RecordedAt on AttendanceRecord is immutable.
///             This table captures every change as a separate log entry.
/// 
/// Multi-tenant isolation: inherited from the parent AttendanceRecord's TeacherId scope.
/// </summary>
public class AttendanceEditLog : BaseEntity
{
    /// <summary>
    /// Foreign key to the attendance record that was modified.
    /// CASCADE delete: if the attendance record is removed, its edit history follows.
    /// </summary>
    [ForeignKey(nameof(AttendanceRecord))]
    public long AttendanceRecordId { get; set; }
    public AttendanceRecord AttendanceRecord { get; set; } = null!;

    /// <summary>
    /// The attendance status before the edit was applied.
    /// </summary>
    public AttendanceStatus PreviousStatus { get; set; }

    /// <summary>
    /// The attendance status after the edit was applied.
    /// </summary>
    public AttendanceStatus NewStatus { get; set; }

    /// <summary>
    /// Timestamp when the edit was made.
    /// REQ-ATT-025: Preserved for audit differentiation.
    /// </summary>
    public DateTime EditedAt { get; set; }

    /// <summary>
    /// Foreign key to the user who made the edit.
    /// May be the teacher or an assistant.
    /// SET NULL if the editing user is deleted.
    /// </summary>
    [ForeignKey(nameof(EditedByUser))]
    public long? EditedByUserId { get; set; }
    public User? EditedByUser { get; set; }

    /// <summary>
    /// Optional free-text reason for the edit.
    /// Useful for audit review when attendance was changed after the fact.
    /// </summary>
    public string? EditReason { get; set; }
}