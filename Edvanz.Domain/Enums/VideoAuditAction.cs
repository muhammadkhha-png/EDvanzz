namespace Edvanz.Domain.Enums;

/// <summary>
/// Identifies what action produced a <c>VideoAssetAudit</c> row. Stored as a short
/// string column rather than a tinyint — audit rows are read by humans (forensic
/// review by support engineers) and the readability of <c>SELECT * FROM
/// VideoAssetAudit</c> matters more than two bytes per row.
///
/// REQ-VCM-BR-03: Video deletion is permanent and irreversible. The audit row is
/// the only durable record of what was deleted.
///
/// VALUES ARE PART OF THE PERSISTED CONTRACT.
/// Use the <c>Values</c> nested class as the single source of truth — never inline
/// these strings in service code.
/// </summary>
public static class VideoAuditAction
{
    /// <summary>Video was hard-deleted by its owning teacher (Story E).</summary>
    public const string HardDelete = "HARD_DELETE";

    /// <summary>
    /// Reserved for a future bulk-delete flow. Not emitted by v1 of the module —
    /// declared here so the column constraint can be widened without a migration
    /// rename when the feature ships.
    /// </summary>
    public const string BulkDelete = "BULK_DELETE";
}
