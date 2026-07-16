namespace Edvanz.Domain.Interfaces;

// ════════════════════════════════════════════════════════════════════════════
// VIDEO CONTENT MANAGEMENT MODULE 14 — REPOSITORY PROJECTION TYPES
// ════════════════════════════════════════════════════════════════════════════
//
// Query projections returned by IVideoAssetRepo. Living in the same namespace
// as the interface (Edvanz.Domain.Interfaces) but in a sibling file keeps the
// interface readable while preserving the project's "projections live in the
// Domain layer alongside the repo" convention (see TrackingViewRow,
// AbsenceReportRow, ScopeCountAggregate in IExamHomeworkRepo for the same
// pattern, which used to be in-file but is being progressively split out).
//
// These are NOT DTOs — they are intermediate types between the database
// projection and the Application service's mapping to client-facing DTOs.
// Repos return these; services map them to PaginatedResponse<...DTO>.
// ════════════════════════════════════════════════════════════════════════════

/// <summary>
/// Projection row for the teacher's video list (Story B). Includes the
/// per-video aggregates that the list cards display so the service doesn't
/// need a second query to compose them.
/// </summary>
public sealed class TeacherVideoListRow
{
    public long Id { get; set; }
    public string Title { get; set; } = null!;
    public string? Description { get; set; }
    public string SourceUrl { get; set; } = null!;
    public Enums.VideoSourceType SourceType { get; set; }
    public int DurationSeconds { get; set; }
    public int StudentsInScope { get; set; }
    public int TotalOpens { get; set; }

    /// <summary>
    /// Distinct students who have opened the video at least once (G-ANL-3).
    /// Not the same as <see cref="TotalOpens"/>, which counts re-opens.
    /// </summary>
    public int SeenStudentCount { get; set; }

    /// <summary>= <see cref="StudentsInScope"/> - <see cref="SeenStudentCount"/> (G-ANL-3).</summary>
    public int UnseenStudentCount { get; set; }

    /// <summary>Publish state (Draft/Published), shown on the list card.</summary>
    public Enums.VideoStatus Status { get; set; }

    /// <summary>Scheduled-publish timestamp, or null — lets the card distinguish Scheduled from Published.</summary>
    public DateTime? PublishDate { get; set; }

    /// <summary>
    /// Registry file id (<c>FileObject.Id</c>) of the video's cover photo, or null. The service
    /// batch-resolves these to the opaque <c>PublicId</c> + gated URL for the list card (no
    /// per-row query) — same pattern as <see cref="StudentVideoListRow.VideoPhotoFileId"/>.
    /// </summary>
    public long? VideoPhotoFileId { get; set; }

    /// <summary>Number of questions in the video's exam (0 when the video has no exam).</summary>
    public int QuestionsNumber { get; set; }

    /// <summary>Number of live (Attached) attachments on the video.</summary>
    public int AttachmentsNumber { get; set; }

    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// Projection row for a teacher's unit list (Track C / G-UNIT, S2). Rolls up
/// child video counts and distinct seen/unseen students across the unit's
/// videos in one grouped query — same rationale as
/// <see cref="TeacherVideoListRow"/>'s per-video aggregates.
/// </summary>
public sealed class TeacherVideoUnitListRow
{
    public long Id { get; set; }
    public string Title { get; set; } = null!;
    public string? Description { get; set; }
    public int VideoCount { get; set; }
    public int SeenStudentCount { get; set; }
    public int UnseenStudentCount { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// Projection row for the student's accessible video list. Includes the
/// per-(video, student) state from <c>VideoAnalytics</c> via LEFT JOIN —
/// drives the "REWATCH" / "NEW" / "OPENED" badges on Flutter's video cards.
/// </summary>
public sealed class StudentVideoListRow
{
    public long Id { get; set; }
    public string Title { get; set; } = null!;
    public string? Description { get; set; }
    public Enums.VideoSourceType SourceType { get; set; }
    public string SourceUrl { get; set; } = null!;
    public int DurationSeconds { get; set; }
    public DateTime AssignedAt { get; set; }
    public bool HasOpened { get; set; }
    public DateTime? LastOpenedAt { get; set; }

    /// <summary>
    /// Registry file id (<c>FileObject.Id</c>) of the video's cover photo, or null. The service
    /// batch-resolves these to gated URLs for the student card (no per-row query).
    /// </summary>
    public long? VideoPhotoFileId { get; set; }
}

/// <summary>
/// Snapshot returned from the Open UPSERT — used by the service layer to
/// build the Start response without an extra SELECT.
/// </summary>
public sealed class VideoAnalyticsSnapshot
{
    public int OpenCount { get; set; }
    public long TotalWatchSeconds { get; set; }
    public int LastResumePositionSeconds { get; set; }
}

/// <summary>
/// Snapshot returned from the atomic Stop increment — what the service layer
/// returns to Flutter so the local UI can update optimistically.
/// </summary>
public sealed class VideoAnalyticsIncrementResult
{
    public long TotalWatchSeconds { get; set; }
    public int LastResumePositionSeconds { get; set; }
}

/// <summary>
/// One row of the teacher analytics report. Mirrors the spec's Story F JSON
/// shape, computed in SQL via projection (no entity materialization).
/// </summary>
public sealed class VideoAnalyticsReportRow
{
    public long TeacherStudentId { get; set; }
    public string StudentName { get; set; } = null!;
    public string StudentCode { get; set; } = null!;

    /// <summary>
    /// The student's current session name (Track D1 §5 open question), read
    /// from their active <c>StudentSessionAssignment</c>. Null when the
    /// student has no active session assignment.
    /// </summary>
    public string? SessionName { get; set; }

    public bool HasOpened { get; set; }
    public int OpenCount { get; set; }
    public long TotalWatchSeconds { get; set; }
    public int VideoDurationSeconds { get; set; }
    public DateTime? FirstOpenedAt { get; set; }
    public DateTime? LastOpenedAt { get; set; }

    /// <summary>
    /// Capped at 100 server-side. <c>NULL</c> when video duration is 0
    /// (unknown — no student has opened it yet).
    /// </summary>
    public int? EstimatedCompletionPct { get; set; }

    /// <summary>
    /// Uncapped; values &gt; 100 indicate rewatching. <c>NULL</c> when video
    /// duration is 0.
    /// </summary>
    public int? RawWatchPct { get; set; }
}

/// <summary>
/// Top-of-report aggregates that don't change with pagination.
/// </summary>
public sealed class VideoAnalyticsAggregates
{
    public int TotalStudentsInScope { get; set; }
    public int TotalStudentsWatched { get; set; }

    /// <summary>= <see cref="TotalStudentsInScope"/> - <see cref="TotalStudentsWatched"/> (G-ANL-2).</summary>
    public int UnseenCount { get; set; }

    /// <summary>
    /// Students whose <c>EstimatedCompletionPct</c> meets
    /// <c>VideoConstants.CompletionThresholdPercent</c> (G-ANL-1).
    /// </summary>
    public int CompletedCount { get; set; }
}

/// <summary>
/// One row of the analytics audit snapshot — feeds the JSON serializer in the
/// delete flow. <see cref="StudentName"/> and <see cref="StudentCode"/> are
/// denormalized at snapshot time so the audit row remains readable even if
/// the student is later permanently purged.
/// </summary>
public sealed class VideoAnalyticsAuditRow
{
    public long TeacherStudentId { get; set; }
    public string StudentName { get; set; } = null!;
    public string StudentCode { get; set; } = null!;
    public int OpenCount { get; set; }
    public long TotalWatchSeconds { get; set; }
    public DateTime FirstOpenedAt { get; set; }
    public DateTime LastUpdated { get; set; }
    public int LastResumePositionSeconds { get; set; }
}
