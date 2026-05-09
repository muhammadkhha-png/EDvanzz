using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Edvanz.Domain.Enums;

namespace Edvanz.Application.Dtos.VideoContentManagement;

// ════════════════════════════════════════════════════════════════════════════
// VIDEO CONTENT MANAGEMENT MODULE 14 — DATA TRANSFER OBJECTS
// ════════════════════════════════════════════════════════════════════════════
//
// All request and response DTOs for the VCM module. These are the contract
// between the Presentation layer (controller) and the Application layer
// (service). They never leak Domain entities and are never used inside the
// Domain or Infrastructure layers.
//
// CONVENTIONS observed from the existing codebase:
//   - DTO classes use PascalCase property names (.NET default).
//   - PaginatedResponse fields use camelCase (existing convention — see
//     PaginatedResponse.cs).
//   - Required-on-the-wire fields use [Required] for ASP.NET model binding
//     to surface 400s with field names automatically.
//   - Enum-valued query parameters use JsonStringEnumConverter so Swagger
//     and clients see "YouTube" not "1".
// ════════════════════════════════════════════════════════════════════════════

// ══════════════════════════════════════════════════════════════════════════
// CREATE VIDEO (Story A)
// ══════════════════════════════════════════════════════════════════════════

/// <summary>
/// Request body for <c>POST /api/videos</c>. Story A step 3.
///
/// Trust boundary: <c>SourceUrl</c> is parsed but NOT validated against the
/// upstream provider. Provider validation is documented as a known limitation
/// in the spec — Flutter's player surfaces "video not found" on first play.
/// </summary>
public sealed class CreateVideoRequest
{
    /// <summary>
    /// Display title shown on the student's video card and the teacher's
    /// list. Trimmed in the service layer; max length validated against
    /// <c>VideoConstants.TitleMaxLength</c>.
    /// </summary>
    [Required]
    public string Title { get; set; } = null!;

    /// <summary>
    /// Optional teacher notes. Max length validated against
    /// <c>VideoConstants.DescriptionMaxLength</c>.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// The full URL pasted by the teacher. Must parse to a YouTube or Google
    /// Drive URL — see <see cref="ServiceContract.IVideoUrlParser"/> for the
    /// recognised formats. Service returns <c>InvalidUrl</c> or
    /// <c>UnsupportedSource</c> on parse failure.
    /// </summary>
    [Required]
    public string SourceUrl { get; set; } = null!;
}

/// <summary>
/// Response body for <c>POST /api/videos</c>. The newly-created video has no
/// scope rows yet — Flutter chains a <c>POST /scopes</c> call next. Returning
/// only the id keeps the contract narrow.
/// </summary>
public sealed class CreateVideoResponse
{
    public long VideoAssetId { get; set; }
}

// ══════════════════════════════════════════════════════════════════════════
// SCOPE INPUT — shared by POST /scopes and PUT /scopes
// ══════════════════════════════════════════════════════════════════════════

/// <summary>
/// One scope row to add or replace. Exactly one of
/// <see cref="TeacherStudentId"/>, <see cref="SessionId"/>, or
/// <see cref="SessionGroupId"/> must be populated, matching
/// <see cref="ScopeType"/>. Service returns <c>ScopeShapeInvalid</c> when
/// the shape doesn't match.
/// </summary>
public sealed class VideoScopeInputDto
{
    [Required]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public VideoScopeType ScopeType { get; set; }

    public long? TeacherStudentId { get; set; }
    public long? SessionId { get; set; }
    public long? SessionGroupId { get; set; }
}

/// <summary>
/// Request body for both <c>POST /api/videos/{id}/scopes</c> (append) and
/// <c>PUT /api/videos/{id}/scopes</c> (replace-all).
/// </summary>
public sealed class AssignScopesRequest
{
    [Required]
    public List<VideoScopeInputDto> Scopes { get; set; } = new();
}

/// <summary>
/// Response body for <c>POST /api/videos/{id}/scopes</c>. The append flow is
/// idempotent on duplicates — <c>scopesSkipped</c> reports how many input
/// rows were rejected by the unique index because they already existed.
/// </summary>
public sealed class AppendScopesResponse
{
    public int ScopesAdded { get; set; }
    public int ScopesSkipped { get; set; }
    public int StudentsInScope { get; set; }
}

/// <summary>
/// Response body for <c>PUT /api/videos/{id}/scopes</c>. <c>scopesAdded</c>
/// and <c>scopesRemoved</c> are computed by the service: it deletes all old
/// scopes and inserts the new set in one transaction (Story D).
/// </summary>
public sealed class ReplaceScopesResponse
{
    public int ScopesAdded { get; set; }
    public int ScopesRemoved { get; set; }
    public int StudentsInScope { get; set; }
}

// ══════════════════════════════════════════════════════════════════════════
// TEACHER VIDEO LIST (Story B teacher endpoint, Story F summary card)
// ══════════════════════════════════════════════════════════════════════════

/// <summary>
/// Request DTO for <c>GET /api/videos/teacher</c>. Inherits the shared
/// pagination shape but provides only Search filtering — the spec does not
/// define sort options for the teacher list (newest-first is the default).
/// </summary>
public sealed class TeacherVideoListRequest
{
    private int _page = 1;
    private int _pageSize = 20;

    public int Page
    {
        get => _page;
        set => _page = value < 1 ? 1 : value;
    }

    public int PageSize
    {
        get => _pageSize;
        set => _pageSize = value < 1 ? 20 : value > 100 ? 100 : value;
    }

    public string? Search { get; set; }
}

/// <summary>
/// One row of the teacher's own video list. Mirrors the contract table for
/// endpoint #6 in the spec.
/// </summary>
public sealed class TeacherVideoListItemDto
{
    public long Id { get; set; }
    public string Title { get; set; } = null!;

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public VideoSourceType SourceType { get; set; }

    public int DurationSeconds { get; set; }
    public int StudentsInScope { get; set; }
    public int TotalOpens { get; set; }
    public DateTime CreatedAt { get; set; }
}

// ══════════════════════════════════════════════════════════════════════════
// STUDENT VIDEO LIST (Story B student endpoint)
// ══════════════════════════════════════════════════════════════════════════

/// <summary>
/// Request DTO for <c>GET /api/videos/student</c>. Pagination only —
/// students don't search or sort their list in v1.
/// </summary>
public sealed class StudentVideoListRequest
{
    private int _page = 1;
    private int _pageSize = 20;

    public int Page
    {
        get => _page;
        set => _page = value < 1 ? 1 : value;
    }

    public int PageSize
    {
        get => _pageSize;
        set => _pageSize = value < 1 ? 20 : value > 100 ? 100 : value;
    }
}

/// <summary>
/// One row of the student's accessible video list. Includes the student's
/// own watch state via the analytics LEFT JOIN — drives the "REWATCH" /
/// "NEW" / "OPENED" badges on Flutter's video cards.
/// </summary>
public sealed class StudentVideoListItemDto
{
    public long Id { get; set; }
    public string Title { get; set; } = null!;
    public string? Description { get; set; }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public VideoSourceType SourceType { get; set; }

    /// <summary>
    /// The original watch URL. Flutter uses this for share/copy actions.
    /// The actual player loads the embed URL from
    /// <c>POST /start</c>'s response — never this.
    /// </summary>
    public string SourceUrl { get; set; } = null!;

    public int DurationSeconds { get; set; }
    public DateTime AssignedAt { get; set; }
    public bool HasOpened { get; set; }
    public DateTime? LastOpenedAt { get; set; }
}

// ══════════════════════════════════════════════════════════════════════════
// PARENT VIDEO LIST (Q7(a) decision)
// ══════════════════════════════════════════════════════════════════════════

/// <summary>
/// Request DTO for <c>GET /api/videos/parent</c>. The parent picks which
/// child's videos to view via the <see cref="StudentUserId"/> query
/// parameter; the service validates the parent-child link and the
/// teacher-visibility flag (AAM-FR-04.9) before resolving the list.
///
/// Q7(a) decision: the parent endpoint takes a student id and returns
/// the same shape as the student endpoint, plus watch fields for the
/// parent to monitor progress.
/// </summary>
public sealed class ParentVideoListRequest
{
    private int _page = 1;
    private int _pageSize = 20;

    [Required]
    public long StudentUserId { get; set; }

    public int Page
    {
        get => _page;
        set => _page = value < 1 ? 1 : value;
    }

    public int PageSize
    {
        get => _pageSize;
        set => _pageSize = value < 1 ? 20 : value > 100 ? 100 : value;
    }
}

/// <summary>
/// One row of the parent's video-monitoring list for a specific child.
/// Adds <see cref="TotalWatchSeconds"/> on top of
/// <see cref="StudentVideoListItemDto"/> so the parent sees how much of each
/// video their child has watched. This is read-only — parents have no
/// start/stop actions in v1.
/// </summary>
public sealed class ParentVideoListItemDto
{
    public long Id { get; set; }
    public string Title { get; set; } = null!;
    public string? Description { get; set; }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public VideoSourceType SourceType { get; set; }

    public int DurationSeconds { get; set; }
    public DateTime AssignedAt { get; set; }
    public bool HasOpened { get; set; }
    public DateTime? LastOpenedAt { get; set; }
    public long TotalWatchSeconds { get; set; }
}

// ══════════════════════════════════════════════════════════════════════════
// START / STOP (Story B + Story C)
// ══════════════════════════════════════════════════════════════════════════

/// <summary>
/// Request body for <c>POST /api/videos/{id}/start</c>.
///
/// Trust boundary on <see cref="VideoDurationSeconds"/>: the server stores
/// the value only when (a) no prior duration is known (current = 0) or (b)
/// the report is within ±<c>VideoConstants.DurationToleranceFraction</c> of
/// the stored value. Out-of-band reports are silently ignored.
/// </summary>
public sealed class StartWatchRequest
{
    /// <summary>
    /// Stable per-install UUID generated by Flutter and persisted on-device.
    /// Length-bounded to <c>VideoConstants.DeviceIdMaxLength</c>; never used
    /// for authorization, only for partitioning anchor lookups.
    /// </summary>
    [Required]
    public string DeviceId { get; set; } = null!;

    /// <summary>
    /// The video's total length in seconds, read by Flutter from the
    /// player's metadata. May be zero if the player doesn't yet know.
    /// </summary>
    public int VideoDurationSeconds { get; set; }

    /// <summary>
    /// Optional idempotency key (Q4(b)). When supplied and a row with the
    /// same <c>ClientEventId</c> already exists, the server short-circuits
    /// with the previous response payload instead of inserting a duplicate
    /// Open event.
    /// </summary>
    public Guid? ClientEventId { get; set; }
}

/// <summary>
/// Response body for <c>POST /api/videos/{id}/start</c>. Drives Flutter's
/// secure-WebView load.
/// </summary>
public sealed class StartWatchResponse
{
    public long VideoAssetId { get; set; }

    /// <summary>
    /// The provider-specific embed URL built from <c>SourceType</c> and
    /// <c>ExternalId</c>. Flutter loads THIS into the WebView, not the
    /// original watch URL.
    /// </summary>
    public string EmbedUrl { get; set; } = null!;

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public VideoSourceType SourceType { get; set; }

    /// <summary>
    /// Where Flutter should seek to. Capped at
    /// <c>DurationSeconds - VideoConstants.ResumeEndOfVideoBufferSeconds</c>
    /// to avoid the "instant complete" glitch.
    /// </summary>
    public int ResumeFromSeconds { get; set; }

    /// <summary>
    /// The duration the server believes the video has. May be zero on
    /// first-ever open if the client also reported zero.
    /// </summary>
    public int DurationSeconds { get; set; }
}

/// <summary>
/// Request body for <c>POST /api/videos/{id}/stop</c>.
///
/// Trust boundaries on the deltas:
/// <list type="bullet">
///   <item><see cref="DeltaSeconds"/> is clamped at the server to
///         <c>min(serverElapsedSinceLastEvent + tolerance, DurationSeconds)</c>.</item>
///   <item><see cref="PositionSeconds"/> is bounded to <c>[0, DurationSeconds]</c>.</item>
/// </list>
/// Both clamps happen silently — the server never errors on out-of-bounds
/// values; it just records the sanitised version.
/// </summary>
public sealed class StopWatchRequest
{
    [Required]
    public string DeviceId { get; set; } = null!;

    public int PositionSeconds { get; set; }
    public int DeltaSeconds { get; set; }

    /// <summary>
    /// Optional idempotency key (Q4(b)). See <see cref="StartWatchRequest.ClientEventId"/>.
    /// </summary>
    public Guid? ClientEventId { get; set; }
}

/// <summary>
/// Response body for <c>POST /api/videos/{id}/stop</c>. Reflects the
/// server-validated state so Flutter can update its UI optimistically.
/// </summary>
public sealed class StopWatchResponse
{
    public long TotalWatchSeconds { get; set; }
    public int LastResumePositionSeconds { get; set; }
    public int AcceptedDelta { get; set; }
    public bool DeltaWasClamped { get; set; }
}

// ══════════════════════════════════════════════════════════════════════════
// ANALYTICS (Story F)
// ══════════════════════════════════════════════════════════════════════════

/// <summary>
/// Request DTO for <c>GET /api/videos/{id}/analytics</c>.
/// </summary>
public sealed class VideoAnalyticsRequest
{
    private int _page = 1;
    private int _pageSize = 50;

    public int Page
    {
        get => _page;
        set => _page = value < 1 ? 1 : value;
    }

    public int PageSize
    {
        get => _pageSize;
        set => _pageSize = value < 1 ? 50 : value > 200 ? 200 : value;
    }

    /// <summary>
    /// Optional partial match on student name or student code.
    /// </summary>
    public string? Search { get; set; }
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public VideoAnalyticsSortBy SortBy { get; set; } = VideoAnalyticsSortBy.StudentName;

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public SortDirection SortDirection { get; set; } = SortDirection.Asc;
}

/// <summary>
/// Top-level analytics response. Aggregates appear once at the top; per-row
/// data is paginated below.
/// </summary>
public sealed class VideoAnalyticsResponse
{
    public long VideoAssetId { get; set; }
    public string Title { get; set; } = null!;
    public int DurationSeconds { get; set; }
    public int TotalStudentsInScope { get; set; }
    public int TotalStudentsWatched { get; set; }

    public List<VideoAnalyticsRowDto> Rows { get; set; } = new();

    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
    public int TotalPages { get; set; }
}

/// <summary>
/// One row of the analytics report, mapped from
/// <see cref="Domain.Interfaces.VideoAnalyticsReportRow"/>.
/// </summary>
public sealed class VideoAnalyticsRowDto
{
    public long TeacherStudentId { get; set; }
    public string StudentName { get; set; } = null!;
    public string StudentCode { get; set; } = null!;
    public bool HasOpened { get; set; }
    public int OpenCount { get; set; }
    public long TotalWatchSeconds { get; set; }
    public int VideoDurationSeconds { get; set; }
    public DateTime? FirstOpenedAt { get; set; }
    public DateTime? LastOpenedAt { get; set; }

    /// <summary>0–100 inclusive. <c>null</c> when video duration is unknown.</summary>
    public int? EstimatedCompletionPct { get; set; }

    /// <summary>Uncapped; values &gt; 100 indicate rewatching.</summary>
    public int? RawWatchPct { get; set; }
}
