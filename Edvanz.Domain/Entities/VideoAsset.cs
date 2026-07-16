using Edvanz.Domain.Entities.ShareProp;
using Edvanz.Domain.Enums;
using System.ComponentModel.DataAnnotations;
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
/// EDITABILITY: <c>PUT /api/videos/{id}</c> exists and allows the teacher to edit
/// title, description, source URL, publish date, status, and unit links after
/// creation — everything except <see cref="TeacherId"/>, <see cref="CreatedByUserId"/>,
/// and <see cref="Id"/>. Changing <see cref="SourceUrl"/> resets watch analytics
/// (<see cref="VideoAnalytics"/>/<see cref="VideoWatchEvent"/>) and zeroes
/// <see cref="DurationSeconds"/>, since a new URL is treated as a different video
/// (unchanged rule). <see cref="DurationSeconds"/> may also be set explicitly by the
/// teacher on update (see <see cref="IsDurationManuallySet"/>); absent a manual
/// override, it remains purely student-reported via the first-watch flow with ±5%
/// tolerance against any prior value (see <c>VideoConstants.DurationToleranceFraction</c>).
/// <see cref="UpdatedAt"/> tracks the timestamp of the last edit.
///
/// PERSISTED CONTRACT: column types, lengths, and FK behaviors are defined in
/// <c>EdvanzDbContext.OnModelCreating</c>. <see cref="TeacherId"/> and
/// <see cref="CreatedByUserId"/> intentionally have NO <c>[ForeignKey]</c>
/// attributes — the fluent API is the single source of truth for NoAction
/// behavior. With both annotation and fluent API present, EF Core 10 silently
/// merges declarations and the explicit <c>OnDelete</c> from the fluent side is
/// dropped (this caused the NoAction-everywhere bug in the initial migration).
/// </summary>
public class VideoAsset : BaseEntity
{
    // ══════════════════════════════════════════════
    // OWNERSHIP & TENANT SCOPE
    // ══════════════════════════════════════════════

    /// <summary>
    /// Foreign key to the owning Teacher. Every read and write of this row is
    /// tenant-scoped on this column. Restrict-deleted (app-layer NoAction on
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
    // VISIBILITY (Track D1 — Draft/Published + scheduled publish)
    // ══════════════════════════════════════════════

    /// <summary>
    /// Draft/Published gate. Draft videos are invisible to students
    /// regardless of <see cref="PublishDate"/>. Editable via
    /// <c>PATCH /api/videos/{id}/status</c> or the full update endpoint.
    /// Defaults to <see cref="VideoStatus.Published"/> so existing rows keep
    /// their current visibility.
    /// </summary>
    public VideoStatus Status { get; set; } = VideoStatus.Published;

    /// <summary>
    /// Optional scheduled-publish timestamp. When set in the future, the
    /// video stays hidden from students even if <see cref="Status"/> is
    /// <see cref="VideoStatus.Published"/> — visible once
    /// <c>PublishDate &lt;= now</c>. Null means "publish immediately" once
    /// <see cref="Status"/> is <see cref="VideoStatus.Published"/>.
    /// </summary>
    public DateTime? PublishDate { get; set; }

    // ══════════════════════════════════════════════
    // RUNTIME-EDITABLE METADATA
    // ══════════════════════════════════════════════

    /// <summary>Last update timestamp. Set on every <c>PUT /api/videos/{id}</c> call.</summary>
    public DateTime? UpdatedAt { get; set; }

    /// <summary>
    /// Optimistic concurrency token. <c>PUT /api/videos/{id}</c> now spans multiple
    /// related writes (fields, unit links, optionally scopes) — this guards against
    /// two concurrent editors (e.g., teacher + assistant) silently overwriting each
    /// other. Same pattern as <c>TeacherSubscription.RowVersion</c> /
    /// <c>AssignmentTemplate.RowVersion</c>.
    /// </summary>
    [Timestamp]
    public byte[] RowVersion { get; set; } = null!;

    /// <summary>
    /// FK to the video photo's (cover image's) <see cref="FileObject"/> in the central file
    /// registry, or null if none set. References <c>FileObject.Id</c>; the gated read URL is
    /// reconstructed from the FileObject's <c>PublicId</c>. Formerly named ThumbnailFileId
    /// ("thumbnail" → "video photo" app-wide); before that, the inline <c>ThumbnailBlobPath</c>.
    /// </summary>
    public long? VideoPhotoFileId { get; set; }

    /// <summary>
    /// True once a teacher has explicitly set <see cref="DurationSeconds"/> via
    /// <c>PUT /api/videos/{id}</c>. When true, StartWatch's first-report-wins logic
    /// must NOT overwrite the value — subsequent student reports are
    /// tolerance-checked against it instead, same as the existing non-zero-duration
    /// branch in <c>TryUpdateDurationWithinToleranceAsync</c>.
    /// </summary>
    public bool IsDurationManuallySet { get; set; } = false;

    // ══════════════════════════════════════════════
    // ORGANIZATION (Track C / G-UNIT)
    // ══════════════════════════════════════════════

    /// <summary>
    /// M:N link rows to this video's parent units (G-UNIT). No rows means the
    /// video is "loose" — no unit assigned. Access to the video is the union of
    /// its own <see cref="Scopes"/> OR any linked unit's <see cref="VideoUnit.Scopes"/>.
    /// Deleting a unit does not delete or orphan its videos; the service layer
    /// removes the corresponding <see cref="VideoAssetUnit"/> rows, and the video
    /// simply becomes loose again for that unit.
    /// </summary>
    public ICollection<VideoAssetUnit> AssetUnits { get; set; } = new List<VideoAssetUnit>();

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
    /// than NoAction-deleting the video. The video survives because it belongs
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
    /// All scope rows targeting this video. NoAction-deleted with the video.
    /// REQ-VCM-FR-02: A video may have many scope rows resolving to a unique set
    /// of students.
    /// </summary>
    public ICollection<VideoScope> Scopes { get; set; } = new List<VideoScope>();

    /// <summary>
    /// Per-student aggregate analytics for this video. One row per
    /// <c>(VideoAssetId, TeacherStudentId)</c> pair, ever. NoAction-deleted.
    /// </summary>
    public ICollection<VideoAnalytics> Analytics { get; set; } = new List<VideoAnalytics>();

    /// <summary>
    /// Append-only watch event log. NoAction-deleted.
    /// </summary>
    public ICollection<VideoWatchEvent> WatchEvents { get; set; } = new List<VideoWatchEvent>();
}
