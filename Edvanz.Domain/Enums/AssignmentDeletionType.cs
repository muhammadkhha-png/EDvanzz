using System.Text.Json.Serialization;

namespace Edvanz.Domain.Enums;

/// <summary>
/// The kind of deletion event recorded in <c>AssignmentDeletionLogs</c>.
/// Captures the distinction between a full hard delete (REQ-EXH-037) and the
/// soft action of ending a recurrence (REQ-EXH-012). Both are auditable but
/// have very different effects on existing data.
///
/// Stored as tinyint in the database.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum AssignmentDeletionType : byte
{
    /// <summary>
    /// The tutor permanently deleted the entire assignment template, all its occurrences,
    /// and all student obligations and audit history. There is no recovery mechanism.
    /// REQ-EXH-037: Hard delete with no recovery, after a confirmation dialog.
    /// The deletion log retains a JSON snapshot of the template for historical context.
    /// </summary>
    HardDelete = 0,

    /// <summary>
    /// The tutor stopped the recurrence of an active recurring template.
    /// Previously recorded occurrences and student records are preserved unchanged.
    /// REQ-EXH-012: Stopping the recurrence does not affect or delete prior occurrences.
    /// The template row remains; <c>IsRecurrenceStopped</c> is set to true.
    /// </summary>
    StopRecurrence = 1
}
