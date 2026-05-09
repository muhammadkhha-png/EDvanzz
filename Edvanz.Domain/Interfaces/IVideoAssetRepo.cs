using Edvanz.Domain.Entities;
using Edvanz.Domain.Enums;

namespace Edvanz.Domain.Interfaces;

/// <summary>
/// Extended repository interface for the Video Content Management Module
/// (Module 14). Centralizes every domain-specific query for the five VCM
/// entities: <c>VideoAsset</c>, <c>VideoScope</c>, <c>VideoAnalytics</c>,
/// <c>VideoWatchEvent</c>, and <c>VideoAssetAudit</c>.
///
/// ARCHITECTURAL NOTE (same rationale as <c>IUserRepo</c>, <c>IAttendanceRepo</c>,
/// <c>IPaymentRepo</c>, <c>IExamHomeworkRepo</c>):
/// All expression-based queries are encapsulated here in named methods. The
/// Application layer never builds raw predicates. If a query changes, you edit
/// ONE method in this repo — not every service that uses it.
///
/// Inherits <see cref="IGenericRepo{T, Tkey}"/> over <see cref="VideoAsset"/>
/// for basic CRUD on the primary entity. Other entities are accessed via the
/// named methods below.
///
/// QUERY PROJECTION TYPES — declared in the sibling file
/// <c>VideoRepoProjections.cs</c> within this same namespace:
/// <see cref="TeacherVideoListRow"/>, <see cref="StudentVideoListRow"/>,
/// <see cref="VideoAnalyticsSnapshot"/>, <see cref="VideoAnalyticsIncrementResult"/>,
/// <see cref="VideoAnalyticsReportRow"/>, <see cref="VideoAnalyticsAggregates"/>,
/// <see cref="VideoAnalyticsAuditRow"/>.
///
/// CONCURRENCY NOTE — atomic UPSERT path:
/// The Stop event flow uses <see cref="IncrementWatchAtomicAsync"/>, which
/// executes a conditional <c>ExecuteUpdateAsync</c> with SQL-side increment
/// rather than a tracked read-modify-write. This makes simultaneous Stop
/// events from multiple devices (Story C concurrency case) correctly add
/// their deltas without an in-application lock. SQL Server's row-level X
/// lock serializes the two updates with sub-millisecond contention.
///
/// IDEMPOTENCY NOTE — ClientEventId path:
/// The Open and Stop event inserts use <see cref="HasEventWithClientIdAsync"/>
/// to detect retried events before insertion. The unique filtered index
/// <c>UX_VWE_ClientEventId</c> is the safety net at the DB level.
/// </summary>
public interface IVideoAssetRepo : IGenericRepo<VideoAsset, long>
{
    // ══════════════════════════════════════════════════════════════════════
    // VIDEO ASSET — WRITE PATH
    // ══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Adds a new video to the change tracker. The service layer composes
    /// the asset + initial scope rows + first analytics-row defaults inside a
    /// single Unit of Work transaction (Story A).
    /// </summary>
    Task AddVideoAsync(VideoAsset video);

    /// <summary>
    /// Hard-deletes a video. Cascades through the composite FK to remove
    /// scopes, analytics, and watch events. The service layer must persist
    /// the JSON snapshot to <see cref="VideoAssetAudit"/> in the SAME
    /// transaction before invoking this (REQ-VCM-BR-03).
    /// </summary>
    Task DeleteVideoAsync(VideoAsset video);

    /// <summary>
    /// Conditionally updates <see cref="VideoAsset.DurationSeconds"/> when
    /// the client-supplied duration is acceptable per the trust-boundary rule
    /// in <c>VideoConstants.DurationToleranceFraction</c>:
    /// <list type="bullet">
    ///   <item>Current value = 0 → first-watch claim is accepted as-is.</item>
    ///   <item>Current value &gt; 0 and reported value within ±5% → accepted
    ///         (last-writer-wins on the column).</item>
    ///   <item>Otherwise → silently ignored. Service layer logs and proceeds.</item>
    /// </list>
    /// Implemented as <c>ExecuteUpdateAsync</c> with the tolerance check in the
    /// WHERE clause so the update is one round trip.
    /// </summary>
    /// <returns><c>true</c> if the row was updated; <c>false</c> if the report
    /// was out of tolerance and silently ignored.</returns>
    Task<bool> TryUpdateDurationWithinToleranceAsync(
        long videoAssetId,
        int reportedDurationSeconds,
        double toleranceFraction);

    // ══════════════════════════════════════════════════════════════════════
    // VIDEO ASSET — READ PATH
    // ══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Fetches a video by id, scoped to the teacher who owns it. Returns
    /// <c>null</c> if the row does not exist or belongs to a different
    /// teacher. Tracked: caller may mutate <c>DurationSeconds</c> indirectly
    /// or pass to <c>DeleteVideoAsync</c>.
    /// </summary>
    Task<VideoAsset?> GetVideoByIdAndTeacherAsync(long videoAssetId, long teacherId);

    /// <summary>
    /// Fetches a video together with its scope rows eagerly loaded. Used by
    /// the delete flow (snapshot-building) and the "manage access" view.
    /// AsNoTracking — caller does not mutate.
    /// </summary>
    Task<VideoAsset?> GetVideoWithScopesAsync(long videoAssetId, long teacherId);

    /// <summary>
    /// Paged list of a teacher's own videos for the Teacher Videos screen.
    /// Story B endpoint <c>GET /api/videos/teacher</c>. Backed by
    /// <c>IX_VideoAssets_TeacherId_CreatedAt</c> (newest first).
    ///
    /// The repo also returns the per-video <c>StudentsInScope</c> and
    /// <c>TotalOpens</c> aggregates so the service layer doesn't need to
    /// post-process — single round trip via projection.
    /// </summary>
    /// <param name="teacherId">Tenant scope (multi-tenant isolation).</param>
    /// <param name="search">Optional partial-match filter on
    /// <c>VideoAsset.Title</c> using <c>EF.Functions.Like</c>.</param>
    /// <param name="page">1-based page number (clamped by caller).</param>
    /// <param name="pageSize">Page size (clamped by caller).</param>
    Task<(IReadOnlyList<TeacherVideoListRow> Items, int TotalCount)>
        GetTeacherVideosPagedAsync(
            long teacherId,
            string? search,
            int page,
            int pageSize);

    /// <summary>
    /// Paged list of videos visible to a specific student. Story B endpoint
    /// <c>GET /api/videos/student</c>. Resolves access through the union of
    /// the three scope-target indexes filtered by the student's session and
    /// session group memberships.
    ///
    /// Includes the per-(video, student) analytics state — <c>HasOpened</c>,
    /// <c>LastOpenedAt</c> — via a LEFT JOIN to <c>VideoAnalytics</c> so the
    /// student's video-list cards can show watch indicators in one round trip.
    /// </summary>
    /// <param name="teacherId">The teacher whose videos are being listed.
    /// Caller (service) supplies this from the <c>StudentTeacherLink</c>
    /// resolution; the repo trusts it.</param>
    /// <param name="teacherStudentId">The student whose access is checked.</param>
    Task<(IReadOnlyList<StudentVideoListRow> Items, int TotalCount)>
        GetVisibleVideosForStudentAsync(
            long teacherId,
            long teacherStudentId,
            int page,
            int pageSize);

    // ══════════════════════════════════════════════════════════════════════
    // SCOPE — WRITE PATH
    // ══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Adds a batch of scope rows for an existing video. Service layer is
    /// responsible for validating each scope's target belongs to the same
    /// teacher (via <see cref="IsScopeTargetOwnedByTeacherAsync"/>) before
    /// calling.
    /// </summary>
    Task AddScopesAsync(IEnumerable<VideoScope> scopes);

    /// <summary>
    /// Hard-deletes every scope row for a given video. Used by the
    /// PUT-replace-all flow (Story D), wrapped in the service layer's
    /// transaction. Implemented as <c>ExecuteDeleteAsync</c> for a single
    /// round trip — no in-memory loading.
    /// </summary>
    Task DeleteAllScopesForVideoAsync(long videoAssetId);

    /// <summary>
    /// Hard-deletes a single scope row, scoped to the teacher. Used by the
    /// DELETE-single-scope endpoint (Story D, endpoint #4). Returns
    /// <c>false</c> if the row does not exist or belongs to a different
    /// teacher.
    /// </summary>
    Task<bool> DeleteScopeByIdAndTeacherAsync(long scopeId, long teacherId);

    /// <summary>
    /// Returns the count of scope rows for a video. Used by the
    /// "last scope cannot be removed" check in DELETE-single-scope.
    /// </summary>
    Task<int> CountScopesForVideoAsync(long videoAssetId);

    // ══════════════════════════════════════════════════════════════════════
    // SCOPE — VALIDATION (called by IVideoScopeResolver)
    // ══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Verifies that a scope target row (student / session / session-group)
    /// belongs to the given teacher. Returns <c>false</c> if the target id
    /// does not exist or belongs to a different teacher.
    ///
    /// Used by the service layer to reject foreign-tenant scope targets with
    /// <c>ScopeTargetNotFoundOrForeign</c> before any write happens.
    ///
    /// One method, three branches by <see cref="VideoScopeType"/>, so the
    /// resolver doesn't need three separate calls. The repo dispatches
    /// internally.
    /// </summary>
    Task<bool> IsScopeTargetOwnedByTeacherAsync(
        long teacherId,
        VideoScopeType scopeType,
        long targetId);

    // ══════════════════════════════════════════════════════════════════════
    // ACCESS CHECK — student-facing endpoints
    // ══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Returns <c>true</c> when the given student is in the resolved scope of
    /// the given video. Resolves the union of (a) IndividualStudent scopes
    /// targeting this student directly, (b) Session scopes whose session
    /// contains this student, (c) SessionGroup scopes whose group contains
    /// this student's session.
    ///
    /// This is the BR-VCM-01 enforcement gate called on every Start and Stop
    /// request before any analytics write. Backed by the three filtered
    /// scope-target indexes; expected sub-millisecond cost.
    /// </summary>
    Task<bool> IsStudentInVideoScopeAsync(
        long teacherStudentId,
        long videoAssetId,
        long teacherId);

    // ══════════════════════════════════════════════════════════════════════
    // ANALYTICS — WRITE PATH (atomic UPSERT for multi-device safety)
    // ══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Atomically increments <c>OpenCount</c> on the analytics row for the
    /// given (video, student) pair, OR inserts a new row when none exists.
    ///
    /// First-time open path — when no row exists yet:
    /// <list type="bullet">
    ///   <item><c>OpenCount = 1</c>, <c>FirstOpenedAt = utcNow</c>,
    ///         <c>VideoDurationAtFirstWatch = durationSeconds</c>.</item>
    ///   <item><c>TotalWatchSeconds = 0</c>, <c>LastResumePositionSeconds = 0</c>.</item>
    /// </list>
    ///
    /// Subsequent-open path — when a row exists:
    /// <list type="bullet">
    ///   <item><c>OpenCount = OpenCount + 1</c> via SQL-side increment.</item>
    ///   <item><c>LastUpdated = utcNow</c>.</item>
    ///   <item><c>TotalWatchSeconds</c> and <c>LastResumePositionSeconds</c>
    ///         are NOT touched here — those mutate only on Stop.</item>
    /// </list>
    ///
    /// Returns the resulting analytics state so the service can compute the
    /// resume position for the response.
    /// </summary>
    Task<VideoAnalyticsSnapshot> UpsertAnalyticsOnOpenAsync(
        long videoAssetId,
        long teacherStudentId,
        long teacherId,
        int videoDurationSeconds,
        DateTime utcNow);

    /// <summary>
    /// Atomic SQL-side UPDATE that adds the validated delta to
    /// <c>TotalWatchSeconds</c>, sets <c>LastResumePositionSeconds</c>, and
    /// stamps <c>LastUpdated</c>. Implemented via <c>ExecuteUpdateAsync</c>
    /// with no read-modify-write in application code.
    ///
    /// Concurrency: two simultaneous Stop calls from phone + tablet both run
    /// this UPDATE on the same row. SQL Server takes a row-level X lock; the
    /// second waits ~1ms; both deltas are correctly added.
    /// <c>LastResumePositionSeconds</c> and <c>LastUpdated</c> are last-writer-
    /// wins, which is the correct semantic for "current state" fields.
    ///
    /// Returns the resulting <c>TotalWatchSeconds</c> and
    /// <c>LastResumePositionSeconds</c> so the service can return them in the
    /// stop response without an extra SELECT.
    /// </summary>
    /// <returns>The analytics state after the increment, or <c>null</c> if no
    /// row matched the (video, student) key — caller treats this as
    /// <c>NoActiveSession</c> per Story C.</returns>
    Task<VideoAnalyticsIncrementResult?> IncrementWatchAtomicAsync(
        long videoAssetId,
        long teacherStudentId,
        long acceptedDeltaSeconds,
        int positionSeconds,
        DateTime utcNow);

    // ══════════════════════════════════════════════════════════════════════
    // ANALYTICS — READ PATH
    // ══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Paged teacher analytics report for one video (Story F endpoint
    /// <c>GET /api/videos/{id}/analytics</c>). Returns one row per student in
    /// the resolved scope, including students who have never opened the video
    /// (LEFT JOIN to <c>VideoAnalytics</c>).
    ///
    /// Aggregates (<c>totalStudentsInScope</c>, <c>totalStudentsWatched</c>)
    /// are computed by separate count queries — see
    /// <see cref="GetAnalyticsAggregatesAsync"/> — because they don't change
    /// with pagination.
    /// </summary>
    /// <param name="teacherId">Tenant scope.</param>
    /// <param name="videoAssetId">Target video.</param>
    /// <param name="search">Optional partial-match filter on
    /// <c>StudentName</c> or <c>StudentCode</c>.</param>
    /// <param name="sortBy">Column to sort by — see
    /// <see cref="VideoAnalyticsSortBy"/>.</param>
    /// <param name="sortDirection">Asc or Desc.</param>
    /// <param name="page">1-based.</param>
    /// <param name="pageSize">Clamped by caller.</param>
    Task<(IReadOnlyList<VideoAnalyticsReportRow> Items, int TotalCount)>
        GetAnalyticsRowsForTeacherAsync(
            long teacherId,
            long videoAssetId,
            string? search,
            VideoAnalyticsSortBy sortBy,
            SortDirection sortDirection,
            int page,
            int pageSize);

    /// <summary>
    /// Top-of-report aggregates: total students in scope, total students who
    /// have opened the video at least once. Two scalar SELECTs in one round
    /// trip via tuple result.
    /// </summary>
    Task<VideoAnalyticsAggregates> GetAnalyticsAggregatesAsync(
        long teacherId,
        long videoAssetId);

    /// <summary>
    /// Materializes everything needed by the delete-snapshot serializer:
    /// every <see cref="VideoAnalytics"/> row joined with the student's name
    /// and code (denormalized into the snapshot so it survives student
    /// purge).
    /// </summary>
    Task<IReadOnlyList<VideoAnalyticsAuditRow>> GetAnalyticsForAuditSnapshotAsync(
        long videoAssetId);

    // ══════════════════════════════════════════════════════════════════════
    // WATCH EVENT — WRITE PATH
    // ══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Inserts a single watch event row (Open or Stop). The unique filtered
    /// index <c>UX_VWE_ClientEventId</c> is the DB-level idempotency safety
    /// net; the service layer should call
    /// <see cref="HasEventWithClientIdAsync"/> first to detect retries
    /// without raising an exception.
    /// </summary>
    Task AddWatchEventAsync(VideoWatchEvent watchEvent);

    // ══════════════════════════════════════════════════════════════════════
    // WATCH EVENT — READ PATH
    // ══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Returns the <c>EventUtc</c> of the most recent prior event for the
    /// given (student, video, device) tuple. This is the "anchor" event that
    /// drives delta validation on Stop (Story C). Returns <c>null</c> if no
    /// prior event exists — caller treats this as <c>NoActiveSession</c>.
    ///
    /// Backed by <c>IX_VWE_Student_Video_Device_TimeDesc</c>: single seek +
    /// TOP 1, sub-millisecond.
    /// </summary>
    Task<DateTime?> GetLastEventUtcForDeviceAsync(
        long teacherStudentId,
        long videoAssetId,
        string deviceId);

    /// <summary>
    /// Returns <c>true</c> when an event with the same
    /// <c>ClientEventId</c> has already been recorded. Used by the service
    /// layer to short-circuit retried events with the previous response
    /// payload, instead of inserting a duplicate row and waiting for the
    /// unique index to throw.
    /// </summary>
    Task<bool> HasEventWithClientIdAsync(Guid clientEventId);

    // ══════════════════════════════════════════════════════════════════════
    // AUDIT — WRITE PATH
    // ══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Inserts the audit row that captures a deleted video's snapshot. Always
    /// called inside the same transaction as the cascade-delete of the
    /// underlying VCM rows (Story E). If the surrounding transaction rolls
    /// back, the audit row rolls back too — no orphan audits.
    /// </summary>
    Task AddAuditAsync(VideoAssetAudit audit);

    // ══════════════════════════════════════════════════════════════════════
    // INTEGRATION HOOKS (called by Teacher / Student permanent-purge flows)
    // ══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Hard-deletes every <see cref="VideoAsset"/> belonging to the given
    /// teacher, plus all VCM cascade-children (audits are kept by
    /// configuration). Used by the admin "permanent-purge teacher" flow,
    /// which the DB cascade does NOT cover (Phase 2.2 cascade-graph decision:
    /// <c>Teachers → VideoAssets = NO_ACTION</c>).
    ///
    /// Service layer wraps this in a transaction and writes audit rows for
    /// every video before calling.
    /// </summary>
    Task DeleteAllVideosForTeacherAsync(long teacherId);
}
