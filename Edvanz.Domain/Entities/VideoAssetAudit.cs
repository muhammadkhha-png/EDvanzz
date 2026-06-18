using Edvanz.Domain.Entities.ShareProp;
using System.ComponentModel.DataAnnotations.Schema;

namespace Edvanz.Domain.Entities;

/// <summary>
/// Permanent record of a deleted video. Replaces the soft-delete pattern for
/// <see cref="VideoAsset"/> — the spec mandates hard delete (REQ-VCM-BR-03) so
/// the audit row is the only durable evidence the video ever existed.
///
/// SURVIVES THE PARENT'S DELETION:
/// Unlike most child tables, <c>VideoAssetAudit</c> deliberately does NOT have
/// an FK to <c>VideoAssets.Id</c>. The whole point is that the audit row exists
/// because the parent is gone. <see cref="VideoAssetId"/> is preserved as a
/// plain <c>bigint</c> for forensic queries ("show me audit for the video that
/// used to be id 42"), but no FK and no NoAction.
///
/// TRANSACTIONAL GUARANTEE (Story E):
/// The audit row is INSERTED in the same transaction as the NoAction DELETEs of
/// the underlying video data. If any step fails, the audit row rolls back too —
/// no orphan audits, no destroyed-but-unrecorded videos.
///
/// COLD-STORAGE FUTURE-PROOFING:
/// As <see cref="SnapshotJson"/> grows in size for high-traffic videos (one row
/// per student in scope, plus aggregates), tail latency on the audit insert
/// matters. The optional <see cref="SnapshotArchiveUrl"/> column lets a future
/// migration move the JSON to blob storage and replace the inline column with a
/// reference, without a schema change. NULL in v1.
///
/// FK MODELING: <see cref="TeacherId"/> and <see cref="DeletedByUserId"/>
/// intentionally have NO <c>[ForeignKey]</c> attributes — fluent API in
/// <c>OnModelCreating</c> is the single source of truth for NoAction behavior.
/// </summary>
public class VideoAssetAudit : BaseEntity
{
    // ══════════════════════════════════════════════
    // ORPHANED REFERENCE (no FK by design)
    // ══════════════════════════════════════════════

    /// <summary>
    /// The original <c>VideoAssets.Id</c> of the deleted video. Preserved as a
    /// plain bigint with no foreign key — the parent row no longer exists.
    /// Indexed for forensic lookup ("was video 42 ever deleted?").
    /// </summary>
    public long VideoAssetId { get; set; }

    // ══════════════════════════════════════════════
    // TENANT SCOPE
    // ══════════════════════════════════════════════

    /// <summary>
    /// Foreign key to the owning Teacher. Lets the audit dashboard scope by
    /// tenant without joining anything that might be NoAction-deleted later.
    /// Restrict-deleted: only when the teacher's whole account is hard-purged
    /// by app-layer code does this audit row go (and that code clears it
    /// explicitly in the same transaction).
    /// </summary>
    public long TeacherId { get; set; }

    /// <summary>The teacher whose video was deleted.</summary>
    public Teacher Teacher { get; set; } = null!;

    // ══════════════════════════════════════════════
    // ACTION CLASSIFICATION
    // ══════════════════════════════════════════════

    /// <summary>
    /// What action produced this audit row. Use values from
    /// <see cref="Enums.VideoAuditAction"/> — never inline the literal string in
    /// service code. v1 only emits <c>HARD_DELETE</c>; <c>BULK_DELETE</c> is
    /// reserved for a future flow.
    /// </summary>
    public string Action { get; set; } = null!;

    // ══════════════════════════════════════════════
    // SNAPSHOT PAYLOAD
    // ══════════════════════════════════════════════

    /// <summary>
    /// Full JSON snapshot of the deleted video and everything attached to it at
    /// the moment of deletion. Shape (per spec §2.5.1):
    /// <list type="bullet">
    ///   <item><c>videoAsset</c> — the row from <c>VideoAssets</c></item>
    ///   <item><c>scopes</c> — every <c>VideoScope</c> for this video</item>
    ///   <item><c>analytics</c> — every <c>VideoAnalytics</c> row, with the
    ///         student's name and code denormalized so the audit reader doesn't
    ///         need to join (and the student's row may itself be gone)</item>
    ///   <item><c>aggregates</c> — totalStudentsInScope, totalStudentsWatched,
    ///         totalWatchSecondsAcrossAll, avgCompletionPctAtDelete</item>
    ///   <item><c>deletedByUserId</c>, <c>deletedAt</c></item>
    /// </list>
    /// Stored as <c>nvarchar(max)</c>. Never queried as JSON in v1 — read whole
    /// and parsed by the rare forensic code path that needs it.
    /// </summary>
    public string SnapshotJson { get; set; } = null!;

    /// <summary>
    /// Reserved for a future cold-storage migration. Always NULL in v1: the
    /// snapshot lives entirely inside <see cref="SnapshotJson"/>. When the
    /// migration ships, this column will hold the URL of the blob holding the
    /// snapshot, and <see cref="SnapshotJson"/> will be cleared for migrated
    /// rows. The migration logic — not the application code — will own the
    /// dual-source read.
    /// </summary>
    public string? SnapshotArchiveUrl { get; set; }

    // ══════════════════════════════════════════════
    // ACTOR & TIMESTAMP
    // ══════════════════════════════════════════════

    /// <summary>
    /// The user (Teacher or Assistant) who triggered the deletion. Nullable
    /// because of <c>SET_NULL</c> on the FK: the audit row outlives the actor.
    /// </summary>
    public long? DeletedByUserId { get; set; }

    /// <summary>
    /// The user who deleted the video. Nullable: see <see cref="DeletedByUserId"/>.
    /// </summary>
    public User? DeletedByUser { get; set; }

    /// <summary>
    /// Server-side UTC time the deletion was committed. Distinct from
    /// <see cref="BaseEntity.CreateAt"/> for the same reason
    /// <c>VideoScope.AssignedAt</c> is — explicit, named, and used in audit
    /// dashboard sort orders.
    /// </summary>
    public DateTime DeletedAt { get; set; }
}
