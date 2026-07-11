using Edvanz.Domain.Entities.ShareProp;
using Edvanz.Domain.Enums;
using System.ComponentModel.DataAnnotations.Schema;

namespace Edvanz.Domain.Entities;

/// <summary>
/// A single targeting rule on a <see cref="VideoUnit"/> (collection-level
/// Target Scope). Structurally identical to <see cref="VideoScope"/> — same
/// three-nullable-FK-plus-discriminator pattern, same DB CHECK constraints —
/// but targets a <see cref="VideoUnit"/> instead of a <see cref="VideoAsset"/>.
///
/// FINAL DECISION (supersedes the earlier "unit carries no scope" design):
/// a Video Collection has its own Target Scope in addition to each video's
/// own <see cref="VideoScope"/>. A student is authorized for a video the
/// moment EITHER scope resolves them — the video's own scope, OR (when the
/// video belongs to a unit) that unit's scope. See
/// <c>IVideoAssetRepo.IsStudentInVideoScopeAsync</c> for the union check.
///
/// Same single-recipient-type-per-entity rule as <see cref="VideoScope"/>:
/// all scope rows for one unit must share one <see cref="VideoScopeType"/>
/// (Individual Students, or Groups, or Sessions — never mixed), enforced in
/// the Application layer (the DB CHECK constraints below only validate a
/// single row's shape, not cross-row homogeneity).
///
/// Access is resolved dynamically on every request — no materialized
/// per-student access table. Group/session membership changes take effect
/// immediately with no synchronization step, mirroring
/// <see cref="VideoScope"/>'s BR-VCM-01 "re-resolved on every access" rule.
/// </summary>
public class VideoUnitScope : BaseEntity
{
    // ══════════════════════════════════════════════
    // UNIT LINKAGE & TENANT SCOPE
    // ══════════════════════════════════════════════

    /// <summary>
    /// The unit this scope row belongs to. NoAction-deleted with the unit's
    /// hard removal (units are soft-deleted in practice — see
    /// <see cref="VideoUnit.DeletedAt"/> — so this FK rarely fires).
    /// </summary>
    [ForeignKey(nameof(VideoUnit))]
    public long VideoUnitId { get; set; }

    /// <summary>The owning unit.</summary>
    public VideoUnit VideoUnit { get; set; } = null!;

    /// <summary>
    /// Foreign key to the owning Teacher. Denormalized from
    /// <see cref="VideoUnit.TeacherId"/> so tenant-scoped indexes don't have
    /// to join back to <c>VideoUnits</c>. The composite FK
    /// <c>(VideoUnitId, TeacherId)</c> guarantees this stays in sync with the
    /// parent at the DB level — same pattern as <see cref="VideoScope"/>.
    /// </summary>
    [ForeignKey(nameof(Teacher))]
    public long TeacherId { get; set; }

    /// <summary>The teacher (denormalized).</summary>
    public Teacher Teacher { get; set; } = null!;

    // ══════════════════════════════════════════════
    // SCOPE DISCRIMINATOR & TARGETS
    // ══════════════════════════════════════════════

    /// <summary>
    /// Identifies which of the three nullable target FKs below is populated
    /// on this row. Enforced by CHECK constraints — see class remarks.
    /// </summary>
    public VideoScopeType ScopeType { get; set; }

    /// <summary>
    /// Foreign key to a specific student. Non-null only when
    /// <see cref="ScopeType"/> = <see cref="VideoScopeType.IndividualStudent"/>.
    /// </summary>
    [ForeignKey(nameof(TeacherStudent))]
    public long? TeacherStudentId { get; set; }

    /// <summary>The targeted student, when this is an IndividualStudent scope.</summary>
    public TeacherStudent? TeacherStudent { get; set; }

    /// <summary>
    /// Foreign key to a specific session. Non-null only when
    /// <see cref="ScopeType"/> = <see cref="VideoScopeType.Session"/>.
    /// </summary>
    [ForeignKey(nameof(Session))]
    public long? SessionId { get; set; }

    /// <summary>The targeted session, when this is a Session scope.</summary>
    public Session? Session { get; set; }

    /// <summary>
    /// Foreign key to a specific session group. Non-null only when
    /// <see cref="ScopeType"/> = <see cref="VideoScopeType.SessionGroup"/>.
    /// </summary>
    [ForeignKey(nameof(SessionGroup))]
    public long? SessionGroupId { get; set; }

    /// <summary>The targeted group, when this is a SessionGroup scope.</summary>
    public SessionGroup? SessionGroup { get; set; }

    // ══════════════════════════════════════════════
    // AUDIT
    // ══════════════════════════════════════════════

    /// <summary>The user (Teacher or Assistant) who added this scope row.</summary>
    [ForeignKey(nameof(AssignedByUser))]
    public long AssignedByUserId { get; set; }

    /// <summary>The user who assigned this scope row.</summary>
    public User AssignedByUser { get; set; } = null!;

    /// <summary>Server-side UTC timestamp of when this scope row was added.</summary>
    public DateTime AssignedAt { get; set; }
}
