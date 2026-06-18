using Edvanz.Domain.Entities.ShareProp;
using Edvanz.Domain.Enums;
using System.ComponentModel.DataAnnotations.Schema;

namespace Edvanz.Domain.Entities;

/// <summary>
/// Captures the historical record of an assignment deletion or recurrence-stop event.
/// REQ-EXH-037: Assignment deletion is a hard delete with no recovery — but the action
/// itself must remain auditable. REQ-EXH-012: Stopping a recurrence is a softer event
/// that preserves prior occurrences; this table records both kinds via
/// <see cref="DeletionType"/>.
///
/// JSON SNAPSHOT PATTERN (design decision 5.4):
/// The parent template is hard-deleted, so <see cref="TemplateId"/> has NO foreign key —
/// it is a plain bigint reference for historical lookup only. A complete JSON snapshot
/// of the template (and its scopes / occurrence count / per-student summary) is stored in
/// <see cref="TemplateSnapshotJson"/>. This lets the NoAction delete the template cleanly
/// while preserving everything needed for forensic queries.
///
/// The Domain entity is intentionally ignorant of JSON serialization. The Application
/// layer owns the snapshot DTO and serializes into this column at delete time.
/// </summary>
public class AssignmentDeletionLog : BaseEntity
{
    // ══════════════════════════════════════════════
    // HISTORICAL TEMPLATE REFERENCE (NO FK)
    // ══════════════════════════════════════════════

    /// <summary>
    /// The Id of the template that was deleted or stopped. NOT a foreign key —
    /// for HardDelete entries, the referenced template no longer exists.
    /// Retained as a plain bigint so the original Id survives in reports.
    /// </summary>
    public long TemplateId { get; set; }

    /// <summary>
    /// Foreign key to the owning Teacher. Used to scope deletion-log queries
    /// to a single tenant per REQ-EXH-NFR-004.
    /// </summary>
    [ForeignKey(nameof(Teacher))]
    public long TeacherId { get; set; }
    public Teacher Teacher { get; set; } = null!;

    // ══════════════════════════════════════════════
    // DELETION DETAILS
    // ══════════════════════════════════════════════

    /// <summary>
    /// Whether this row records a full hard delete (REQ-EXH-037) or a stop-recurrence
    /// action (REQ-EXH-012). Behavior on the underlying data differs significantly
    /// between the two.
    /// </summary>
    public AssignmentDeletionType DeletionType { get; set; }

    /// <summary>
    /// Number of students that had any recorded status or grade against this template
    /// at the time of deletion. REQ-EXH-037: Shown in the confirmation dialog before
    /// the tutor approves deletion.
    /// </summary>
    public int StudentsAffected { get; set; }

    /// <summary>
    /// Complete JSON snapshot of the deleted template, captured at deletion time.
    /// Includes: template fields, all scope rows resolved to readable identifiers,
    /// occurrence count, and per-student summary statistics. Stored as
    /// <c>nvarchar(max)</c>; SQL Server can index <c>JSON_VALUE</c> projections via
    /// computed columns if any field becomes a query hotspot.
    ///
    /// Risk register entry (Section 10): for templates with very large scopes,
    /// the snapshot is bounded by service-layer logic — above 10,000 affected
    /// students, the snapshot stores a summary instead of the full per-student list.
    /// </summary>
    [Column(TypeName = "nvarchar(max)")]
    public string TemplateSnapshotJson { get; set; } = null!;

    // ══════════════════════════════════════════════
    // ACTOR & TIMESTAMP
    // ══════════════════════════════════════════════

    /// <summary>
    /// The User who performed the deletion or stop-recurrence action.
    /// </summary>
    [ForeignKey(nameof(DeletedByUser))]
    public long DeletedByUserId { get; set; }
    public User DeletedByUser { get; set; } = null!;

    /// <summary>
    /// UTC timestamp of the deletion or stop-recurrence action.
    /// Distinct from <see cref="BaseEntity.CreateAt"/> for clarity in reports.
    /// </summary>
    public DateTime DeletedAt { get; set; }

    // CORRECTED:
    /// <summary>
    /// The last generated occurrence at the moment of stop-recurrence.
    /// REQ-EXH-012: Stopping recurrence does not delete prior occurrences — this anchor
    /// records which occurrence was the cutoff so forensic queries can answer
    /// "what was preserved when this template was stopped?".
    ///
    /// Population rules:
    ///   - DeletionType = HardDelete → NULL (entire chain hard-deleted; no anchor possible).
    ///   - DeletionType = StopRecurrence → set to MAX(OccurrenceNumber) at the moment of stop.
    /// </summary>
    [ForeignKey(nameof(LastOccurrence))]
    public long? LastOccurrenceId { get; set; }
    public AssignmentOccurrence? LastOccurrence { get; set; }
}
