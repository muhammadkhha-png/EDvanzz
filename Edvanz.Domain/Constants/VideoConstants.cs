namespace Edvanz.Domain.Constants;

/// <summary>
/// Constants for the Video Content Management Module (Module 14).
/// Centralizes magic numbers, column-length limits, the module name string, and
/// localization keys so they are compile-time checked and single-sourced.
///
/// Same pattern as <see cref="AttendanceConstants"/> and <see cref="SubscriptionConstants"/>.
/// </summary>
public static class VideoConstants
{
    // ══════════════════════════════════════════════
    // MODULE IDENTITY
    // ══════════════════════════════════════════════

    /// <summary>
    /// The module name as registered in the <c>Modules</c> seed table and emitted
    /// in JWT <c>module</c> claims. This string appears in
    /// <c>[ModulePermission(Module = ModuleName, ...)]</c> attributes on every VCM
    /// endpoint, in <c>IModuleTeacherRepo.IsModuleActiveAsync</c> calls for the
    /// student/parent runtime gate, and in the permission seeder. Change it in
    /// exactly one place: here.
    /// </summary>
    public const string ModuleName = "Videos";

    // ══════════════════════════════════════════════
    // PERMISSION NAMES (seeded under the "Videos" module)
    // ══════════════════════════════════════════════

    /// <summary>
    /// Read-only permission for teachers and assistants. Required by
    /// <c>GET /api/videos/teacher</c> and <c>GET /api/videos/{id}/analytics</c>.
    /// </summary>
    public const string PermissionView = "View";

    /// <summary>
    /// Write permission for teachers and assistants. Required by every
    /// create / scope-change / delete endpoint.
    /// </summary>
    public const string PermissionManageVideos = "ManageVideos";

    // ══════════════════════════════════════════════
    // VALIDATION TOLERANCES (server-side trust boundaries)
    // ══════════════════════════════════════════════

    /// <summary>
    /// Tolerance for accepting a client-supplied <c>videoDurationSeconds</c> against
    /// the value already stored on <c>VideoAsset.DurationSeconds</c>. Expressed as a
    /// fraction (0.05 = ±5%). A duration outside this band is silently rejected and
    /// logged — never returned as an error to the client (Story B trust boundary).
    /// </summary>
    public const double DurationToleranceFraction = 0.05;

    /// <summary>
    /// Slack added to the server-elapsed-time check when validating the client's
    /// <c>deltaSeconds</c> on a Stop event. Allows for honest clock drift, request
    /// latency, and rounding without rejecting the report. Larger values than
    /// elapsed + this tolerance are clamped, never rejected (Story C trust boundary).
    /// </summary>
    public const int DeltaToleranceSeconds = 5;

    /// <summary>
    /// Number of seconds subtracted from <c>DurationSeconds</c> when computing
    /// <c>resumeFromSeconds</c>. Prevents the player from resuming so close to
    /// the end that the student perceives an "instant complete" glitch.
    /// </summary>
    public const int ResumeEndOfVideoBufferSeconds = 5;

    // ══════════════════════════════════════════════
    // COLUMN-LENGTH LIMITS (mirrored in fluent API)
    // ══════════════════════════════════════════════

    /// <summary>Maximum length of <c>VideoAsset.Title</c>. Trimmed in service layer.</summary>
    public const int TitleMaxLength = 200;

    /// <summary>Maximum length of <c>VideoAsset.Description</c>. Optional field.</summary>
    public const int DescriptionMaxLength = 2000;

    /// <summary>
    /// Maximum length of <c>VideoAsset.SourceUrl</c>. Long enough for any realistic
    /// YouTube or Drive URL including query parameters.
    /// </summary>
    public const int SourceUrlMaxLength = 500;

    /// <summary>
    /// Maximum length of <c>VideoAsset.ExternalId</c>. Comfortably accommodates
    /// YouTube videoIds (~11 chars) and Drive fileIds (~33 chars).
    /// </summary>
    public const int ExternalIdMaxLength = 100;

    /// <summary>
    /// Maximum length of <c>VideoWatchEvent.DeviceId</c>. The client sends a
    /// stable per-install UUID; 100 chars is generous enough for any UUID format
    /// while bounding the column for indexing.
    /// </summary>
    public const int DeviceIdMaxLength = 100;

    /// <summary>Maximum length of <c>VideoAssetAudit.Action</c>.</summary>
    public const int AuditActionMaxLength = 20;

    /// <summary>
    /// Maximum length of <c>VideoAssetAudit.SnapshotArchiveUrl</c>. Reserved for a
    /// future cold-storage migration when the inline JSON snapshot grows large.
    /// Always <c>NULL</c> in v1 — the snapshot lives entirely in <c>SnapshotJson</c>.
    /// </summary>
    public const int SnapshotArchiveUrlMaxLength = 500;

    // ══════════════════════════════════════════════
    // LOCALIZATION KEYS
    // ══════════════════════════════════════════════

    /// <summary>
    /// Localization message keys. Names match entries in <c>Messages.en.resx</c> /
    /// <c>Messages.ar.resx</c>. Service code references these constants — never
    /// inline string literals — so a typo is a build failure, not a runtime miss.
    /// </summary>
    public static class Messages
    {
        // ── Success ──────────────────────────────────────────────────────────
        public const string VideoCreated   = "VideoCreated";
        public const string VideoDeleted   = "VideoDeleted";
        public const string ScopesAssigned = "ScopesAssigned";
        public const string ScopesReplaced = "ScopesReplaced";
        public const string ScopeRemoved   = "ScopeRemoved";
        public const string WatchStarted   = "WatchStarted";
        public const string WatchStopped   = "WatchStopped";

        // ── Errors ───────────────────────────────────────────────────────────
        public const string VideoNotFound                = "VideoNotFound";
        public const string VideoNotInScope              = "VideoNotInScope";
        public const string ModuleDeactivated            = "ModuleDeactivated";
        public const string InvalidUrl                   = "InvalidUrl";
        public const string UnsupportedSource            = "UnsupportedSource";
        public const string ScopeTargetNotFoundOrForeign = "ScopeTargetNotFoundOrForeign";
        public const string ScopeShapeInvalid            = "ScopeShapeInvalid";
        public const string ScopeCannotBeEmpty           = "ScopeCannotBeEmpty";
        public const string LastScopeCannotBeRemoved     = "LastScopeCannotBeRemoved";
        public const string NoActiveSession              = "NoActiveSession";
        public const string NotVideoOwner                = "NotVideoOwner";
        public const string ScopeNotFound                = "ScopeNotFound";
    }
}
