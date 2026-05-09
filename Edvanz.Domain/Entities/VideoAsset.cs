using Edvanz.Domain.Entities.ShareProp;
using Edvanz.Domain.Enums;
using System.ComponentModel.DataAnnotations.Schema;

namespace Edvanz.Domain.Entities;

/// <summary>
/// Represents a single video reference owned by a teacher. One <c>VideoAsset</c>
/// row is created per <c>POST /api/videos</c> call and lives until either the
/// teacher hard-deletes it (Story E) or their account is removed.
///
/// REQ-VCM-FR-01: A teacher publishes a video by pasting an external URL (YouTube
/// or Google Drive). The platform never hosts the bytes; it only stores the
/// reference, the scope, and the analytics generated from student playback.
///
/// REQ-VCM-BR-03: HARD-DELETE-ONLY entity. There is no <c>IsDeleted</c> flag and
/// no <c>DeletedAt</c> column. This is a documented exception to the project-wide
/// soft-delete convention. Audit history is preserved instead via a JSON snapshot
/// captured atomically into <see cref="VideoAssetAudit"/> in the same transaction
/// as the <c>DELETE</c>. Once a row is gone, it is gone — recovery is only
/// possible by replaying the audit snapshot back into the table.
///
/// IMMUTABILITY: Title, description, and source URL are set once at creation and
/// never change afterwards. There is no edit endpoint and no <c>UpdatedAt</c>
/// column (Q2(a) decision). Only <see cref="DurationSeconds"/> may be mutated
/// after creation, and only by the first-watch flow with ±5% tolerance against
/// any prior value (see <c>VideoConstants.DurationToleranceFraction</c>).
///
/// PERSISTED CONTRACT: column types, lengths, and FK behaviors are defined in
/// <c>EdvanzDbContext.OnModelCreating</c>. <see cref="TeacherId"/> and
/// <see cref="CreatedByUserId"/> intentionally have NO <c>[ForeignKey]</c>
/// attributes — the fluent API is the single source of truth for cascade
/// behavior. With both annotation and fluent API present, EF Core 10 silently
/// merges declarations and the explicit <c>OnDelete</c> from the fluent side is
/// dropped (this caused the Cascade-everywhere bug in the initial migration).
/// </summary>
public class VideoAsset : BaseEntity
{
    // ══════════════════════════════════════════════
    // OWNERSHIP & TENANT SCOPE
    // ══════════════════════════════════════════════

    /// <summary>
    /// Foreign key to the owning Teacher. Every read and write of this row is
    /// tenant-scoped on this column. Restrict-deleted (app-layer cascade on
    /// teacher purge); see <c>OnModelCreating</c> for FK behavior.
    /// </summary>
    public long TeacherId { get; set; }

    /// <summary>The Teacher that owns this video.</summary>
    public Teacher Teacher { get; set; } = null!;

    // ══════════════════════════════════════════════
    // CORE VIDEO METADATA (immutable after creation)
    // ══════════════════════════════════════════════

    /// <summary>
    /// Display title. Trimmed in the service layer before persistence.
    /// REQ-VCM-FR-01: Mandatory. Q2(a): Immutable after creation.
    /// </summary>
    public string Title { get; set; } = null!;

    /// <summary>
    /// Optional teacher notes shown alongside the player. Q2(a): Immutable.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// The full URL pasted by the teacher, preserved verbatim for traceability
    /// (e.g., audit review, debugging duplicate uploads). The embed URL used by
    /// the Flutter player is built from <see cref="ExternalId"/> instead — never
    /// from this raw string.
    /// </summary>
    public string SourceUrl { get; set; } = null!;

    /// <summary>
    /// Provider that hosts the video. Drives the embed-URL builder branch in the
    /// service layer. Persisted as <c>tinyint</c>.
    /// </summary>
    public VideoSourceType SourceType { get; set; }

    /// <summary>
    /// Provider-specific identifier extracted from <see cref="SourceUrl"/> at
    /// create time:
    /// <list type="bullet">
    ///   <item>YouTube: the 11-char videoId (from <c>watch?v=</c>, <c>youtu.be/</c>,
    ///         <c>embed/</c>, or <c>shorts/</c> URL formats — all normalize to the
    ///         same id).</item>
    ///   <item>Google Drive: the fileId from <c>/file/d/{id}/...</c>.</item>
    /// </list>
    /// Storing the id separately means the player endpoint never re-parses the
    /// URL on the hot read path, and four equivalent YouTube URL formats
    /// deduplicate to the same id for the optional duplicate-detection index.
    /// </summary>
    public string ExternalId { get; set; } = null!;

    // ══════════════════════════════════════════════
    // RUNTIME-LEARNED METADATA (mutable, narrow rules)
    // ══════════════════════════════════════════════

    /// <summary>
    /// The video's total length in seconds. Initially 0 — we don't pre-validate
    /// the URL against the provider, so duration is unknown at create time
    /// (documented limitation in §5 of the spec). The first student to call
    /// <c>POST /start</c> reports their player's metadata; the server accepts it
    /// and stores it here. Subsequent reports are accepted only if they fall
    /// within ±<c>VideoConstants.DurationToleranceFraction</c> of this value —
    /// out-of-band reports are silently ignored to defeat completion-percentage
    /// inflation attacks.
    /// </summary>
    public int DurationSeconds { get; set; }

    // ══════════════════════════════════════════════
    // AUDIT
    // ══════════════════════════════════════════════

    /// <summary>
    /// The user (Teacher or Assistant) who clicked Create. Distinct from
    /// <see cref="TeacherId"/> because an assistant with <c>ManageVideos</c>
    /// permission can add a video on their tutor's behalf — the row still
    /// belongs to the tutor, but the action is attributed here.
    ///
    /// Nullable because of <c>SET_NULL</c> on the FK: when the actor's User
    /// account is permanently purged, this column becomes <c>NULL</c> rather
    /// than cascade-deleting the video. The video survives because it belongs
    /// to the <c>Teacher</c> tenant, not the actor.
    /// </summary>
    public long? CreatedByUserId { get; set; }

    /// <summary>
    /// The user who created this video. Nullable: see <see cref="CreatedByUserId"/>.
    /// </summary>
    public User? CreatedByUser { get; set; }

    // ══════════════════════════════════════════════
    // NAVIGATION COLLECTIONS
    // ══════════════════════════════════════════════

    /// <summary>
    /// All scope rows targeting this video. Cascade-deleted with the video.
    /// REQ-VCM-FR-02: A video may have many scope rows resolving to a unique set
    /// of students.
    /// </summary>
    public ICollection<VideoScope> Scopes { get; set; } = new List<VideoScope>();

    /// <summary>
    /// Per-student aggregate analytics for this video. One row per
    /// <c>(VideoAssetId, TeacherStudentId)</c> pair, ever. Cascade-deleted.
    /// </summary>
    public ICollection<VideoAnalytics> Analytics { get; set; } = new List<VideoAnalytics>();

    /// <summary>
    /// Append-only watch event log. Cascade-deleted.
    /// </summary>
    public ICollection<VideoWatchEvent> WatchEvents { get; set; } = new List<VideoWatchEvent>();
}
