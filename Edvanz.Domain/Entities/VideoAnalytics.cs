using Edvanz.Domain.Entities.ShareProp;
using System.ComponentModel.DataAnnotations.Schema;

namespace Edvanz.Domain.Entities;

/// <summary>
/// Per-student per-video aggregate row. Exactly one row exists per
/// <c>(VideoAssetId, TeacherStudentId)</c> pair, ever — enforced by a unique
/// index declared in fluent API.
///
/// REQ-VCM-FR-04: Powers the teacher analytics report (Story F) and the student's
/// "did I open this video?" indicator on the video list (Story B).
///
/// CONCURRENCY MODEL — read carefully:
/// This row is the convergence point of multi-device watching. Two stop events
/// from a phone and a tablet must both correctly increment
/// <see cref="TotalWatchSeconds"/>. The implementation never does a
/// read-modify-write in application code. Instead, the service layer issues a
/// single SQL <c>UPDATE</c> via EF Core's <c>ExecuteUpdateAsync</c>:
/// <code>
/// UPDATE VideoAnalytics
/// SET TotalWatchSeconds          = TotalWatchSeconds + @acceptedDelta,
///     LastResumePositionSeconds  = @positionSeconds,
///     LastUpdated                = SYSUTCDATETIME(),
///     OpenCount                  = OpenCount + @openIncrement
/// WHERE VideoAssetId = @video AND TeacherStudentId = @student
/// </code>
/// The increment lives in SQL. SQL Server takes a row-level X lock, the second
/// concurrent update waits ~1ms, and both deltas are correctly applied.
/// <see cref="LastResumePositionSeconds"/> and <see cref="LastUpdated"/> are
/// "current state" fields — last-writer-wins is the correct semantic for them.
///
/// ANALYTICS HISTORY PRESERVATION (PUT replace-all on scopes):
/// When a teacher reassigns scopes and a previously-targeted student is no longer
/// in the resolved set, this row is NOT deleted. The student loses access to
/// the video, but their watch history survives so the teacher's analytics report
/// can still show "student X watched 40% of this video before being removed".
/// Spec §Story D.
///
/// COMPOSITE TENANT SCOPE:
/// <see cref="TeacherId"/> is denormalized and participates in the
/// <c>(VideoAssetId, TeacherId)</c> composite FK with the parent video — same
/// integrity guarantee as <see cref="VideoScope"/>. The fluent API in
/// <c>EdvanzDbContext.OnModelCreating</c> is the single source of truth; do NOT
/// add <c>[ForeignKey(nameof(Teacher))]</c> here (see <see cref="VideoScope"/>
/// remarks for why).
/// </summary>
public class VideoAnalytics : BaseEntity
{
    // ══════════════════════════════════════════════
    // VIDEO LINKAGE & TENANT SCOPE
    // ══════════════════════════════════════════════

    /// <summary>
    /// The video these analytics describe. NoAction-deleted with the video via
    /// the composite FK in fluent API.
    /// </summary>
    public long VideoAssetId { get; set; }

    /// <summary>The owning video.</summary>
    public VideoAsset VideoAsset { get; set; } = null!;

    /// <summary>
    /// Foreign key to the owning Teacher (denormalized from
    /// <see cref="VideoAsset.TeacherId"/>). Composite-FK enforced.
    /// </summary>
    public long TeacherId { get; set; }

    /// <summary>The teacher (denormalized).</summary>
    public Teacher Teacher { get; set; } = null!;

    /// <summary>
    /// The student whose watch activity is aggregated here. NoAction-deleted when
    /// the student is permanently purged — their watch history goes with them.
    /// </summary>
    [ForeignKey(nameof(TeacherStudent))]
    public long TeacherStudentId { get; set; }

    /// <summary>The student.</summary>
    public TeacherStudent TeacherStudent { get; set; } = null!;

    // ══════════════════════════════════════════════
    // AGGREGATES
    // ══════════════════════════════════════════════

    /// <summary>
    /// Number of times this student has called <c>POST /start</c> for this video.
    /// Atomically incremented on the SQL side. Contains every Open event whether
    /// the student watched 1 second or finished — so it can exceed 1 even on a
    /// single-completion student (rewatches).
    /// </summary>
    public int OpenCount { get; set; }

    /// <summary>
    /// Cumulative watch time across all sessions and devices, in seconds.
    /// <para>
    /// <c>bigint</c> by deliberate design: a determined student rewatching a
    /// 10-minute video could legitimately accumulate hundreds of hours; <c>int</c>
    /// would overflow at ~68 years of continuous watching but the project
    /// convention is to use <c>bigint</c> for all cumulative-time fields.
    /// </para>
    /// <para>
    /// Each Stop event contributes the server-validated, clamped delta — never
    /// the raw client-supplied value. Two layers of clamping: (1) clamp to
    /// server-elapsed-since-last-event + 5s tolerance, (2) clamp to
    /// <see cref="VideoAsset.DurationSeconds"/>. A single stop cannot represent
    /// more screen-time than the video is long.
    /// </para>
    /// </summary>
    public long TotalWatchSeconds { get; set; }

    /// <summary>
    /// Set on row insert (first-ever Open for this student-video pair) and never
    /// updated thereafter. Drives the "first opened" column in analytics.
    /// </summary>
    public DateTime FirstOpenedAt { get; set; }

    /// <summary>
    /// Server UTC time of the most recent Open or Stop. Last-writer-wins is the
    /// correct semantic — this is a "most recent activity" indicator.
    /// </summary>
    public DateTime LastUpdated { get; set; }

    /// <summary>
    /// Forensic snapshot of <see cref="VideoAsset.DurationSeconds"/> at the
    /// moment this row was first inserted. If the teacher later edits the
    /// underlying YouTube video and its duration changes, this column preserves
    /// the duration the student originally saw — useful when investigating
    /// suspect completion percentages.
    /// </summary>
    public int VideoDurationAtFirstWatch { get; set; }

    /// <summary>
    /// The player position at the most recent Stop event, capped at
    /// <c>DurationSeconds - <see cref="Constants.VideoConstants.ResumeEndOfVideoBufferSeconds"/></c>.
    /// Used to seek the player on the next Open so the student resumes where they
    /// left off. Cap prevents the "instant complete" glitch when a student
    /// finished the video on the previous session.
    /// </summary>
    public int LastResumePositionSeconds { get; set; }
}
