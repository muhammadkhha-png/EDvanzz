using Edvanz.Domain.Entities.ShareProp;
using Edvanz.Domain.Enums;
using System.ComponentModel.DataAnnotations.Schema;

namespace Edvanz.Domain.Entities;

/// <summary>
/// A single targeting rule on a <see cref="VideoAsset"/>. Mirrors the multi-row
/// scope pattern from Module 6's <c>AssignmentScope</c> for codebase consistency.
///
/// REQ-VCM-FR-02: A video can be targeted to (a) a single student, (b) all
/// students currently in a session, or (c) all students across the sessions of a
/// session group. A video carries one or more scope rows; the resolver
/// (<c>IVideoScopeResolver</c>, Application layer) deduplicates the union of
/// resolved students at access-check time and at analytics time.
///
/// FOREIGN-KEY MODELING:
/// Three nullable FKs replace the polymorphic-FK pattern. Two CHECK constraints
/// configured in <c>EdvanzDbContext.OnModelCreating</c> enforce data integrity at
/// the database layer:
/// <list type="number">
///   <item>Exactly one of the three target FKs is non-null on every row.</item>
///   <item><see cref="ScopeType"/> matches the populated FK.</item>
/// </list>
/// These constraints make it impossible for the application code to write a
/// malformed scope row, even by mistake — the database refuses it.
///
/// COMPOSITE TENANT SCOPE:
/// <see cref="TeacherId"/> is denormalized from the parent <c>VideoAsset</c> and
/// participates in a composite FK <c>(VideoAssetId, TeacherId)</c> declared in
/// the fluent API. This eliminates an entire class of cross-tenant data
/// corruption: a row whose <c>TeacherId</c> doesn't match its parent's
/// <c>TeacherId</c> simply cannot be inserted.
/// </summary>
public class VideoScope : BaseEntity
{
    // ══════════════════════════════════════════════
    // VIDEO LINKAGE & TENANT SCOPE
    // ══════════════════════════════════════════════

    /// <summary>
    /// The video this scope row belongs to. Cascade-deleted with the video.
    /// </summary>
    [ForeignKey(nameof(VideoAsset))]
    public long VideoAssetId { get; set; }

    /// <summary>The owning video.</summary>
    public VideoAsset VideoAsset { get; set; } = null!;

    /// <summary>
    /// Foreign key to the owning Teacher. Denormalized from
    /// <see cref="VideoAsset.TeacherId"/> so tenant-scoped indexes don't have to
    /// join back to <c>VideoAssets</c>. The composite FK
    /// <c>(VideoAssetId, TeacherId)</c> guarantees this stays in sync with the
    /// parent at the DB level.
    /// </summary>
    [ForeignKey(nameof(Teacher))]
    public long TeacherId { get; set; }

    /// <summary>The teacher (denormalized).</summary>
    public Teacher Teacher { get; set; } = null!;

    // ══════════════════════════════════════════════
    // SCOPE DISCRIMINATOR & TARGETS
    // ══════════════════════════════════════════════

    /// <summary>
    /// Identifies which of the three nullable target FKs below is populated on
    /// this row. Enforced by CHECK constraints — see class remarks.
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

    /// <summary>
    /// The user (Teacher or Assistant) who added this scope row. Distinct from
    /// the parent video's <c>CreatedByUserId</c> because scopes are added in
    /// separate calls (<c>POST /scopes</c>, <c>PUT /scopes</c>) that may be made
    /// by a different actor than the one who created the video.
    /// </summary>
    [ForeignKey(nameof(AssignedByUser))]
    public long AssignedByUserId { get; set; }

    /// <summary>The user who assigned this scope row.</summary>
    public User AssignedByUser { get; set; } = null!;

    /// <summary>
    /// Server-side UTC timestamp of when this scope row was added. Distinct from
    /// <see cref="BaseEntity.CreateAt"/>: the base column is set by EF Core
    /// conventions, this column is explicitly the assignment time.
    /// </summary>
    public DateTime AssignedAt { get; set; }
}
