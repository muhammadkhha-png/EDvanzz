<#
    Phase 5 — ParentVideosController + ParentVideoExamsController (Sprint 2)
    ─────────────────────────────────────────────────────────────────────────
    Read-only parent surface for Module 14 (Video Content Management).
    D6 (locked): no start/stop-watch, no take/submit/retry -- a parent never
    triggers a VideoAnalytics write or sees an unfinalized answer key.

    Files touched:
      EDIT   Edvanz.API/Controllers/ParentScopedApiBaseController.cs
             (ChildResolution gains ParentLanguagePreference)
      EDIT   Edvanz.Application/ServiceContract/IVideoService.cs
             (+3 method declarations: GetParentVideosAsync / GetParentUnitsAsync /
             GetParentVideosInUnitAsync)
      EDIT   Edvanz.Application/Services/VideoService.cs
             (+3 implementations, mirroring their student twins exactly;
             BuildStudentVideoPageAsync renamed BuildVideoListPageAsync now that a
             second caller family uses it; class doc comment updated -- the
             "PARENT ENDPOINT -- DEFERRED (Q1(c))" note is no longer true)
      NEW    Edvanz.API/Controllers/ParentVideosController.cs
      NEW    Edvanz.API/Controllers/ParentVideoExamsController.cs

    NOT touched: IStudentVideoExamService / StudentVideoExamService. GetResultAsync
    and GetReviewAsync are reused completely unchanged from the new
    ParentVideoExamsController -- both take only (teacherId, teacherStudentId,
    videoAssetId), no caller-identity branch exists to wrap.

    No migration -- zero schema changes. No new repo methods -- every parent
    method calls the exact same repo query the student twin calls
    (GetVisibleVideosForStudentAsync / GetVisibleVideosForStudentInUnitAsync /
    GetStudentVisibleUnitsAsync), just with the child's teacherStudentId.

    USAGE
    -----
        powershell -ExecutionPolicy Bypass -File .\phase5-parent-videos.ps1

    Safe to re-run from the top (idempotent -- skips any block already applied).
#>

$ErrorActionPreference = "Stop"

function Set-ContentWithRetry {
    param(
        [Parameter(Mandatory)] [string]$Path,
        [Parameter(Mandatory)] [string]$Value
    )

    $maxAttempts = 6
    $delayMs = 200

    for ($attempt = 1; $attempt -le $maxAttempts; $attempt++) {
        try {
            Set-Content -Path $Path -Value $Value -NoNewline -Encoding UTF8
            return
        }
        catch [System.IO.IOException] {
            if ($attempt -eq $maxAttempts) {
                throw "Could not write $Path after $maxAttempts attempts — file stayed locked by another process (OneDrive sync, antivirus real-time scan, or an editor). Close anything that might have it open and re-run; the script is safe to re-run from the top. Original error: $($_.Exception.Message)"
            }
            Write-Host "  [retry] $Path locked, attempt $attempt/$maxAttempts, waiting ${delayMs}ms..."
            Start-Sleep -Milliseconds $delayMs
            $delayMs = $delayMs * 2
        }
    }
}

function Replace-InFile {
    param(
        [Parameter(Mandatory)] [string]$Path,
        [Parameter(Mandatory)] [string]$Find,
        [Parameter(Mandatory)] [string]$Replace,
        [Parameter(Mandatory)] [string]$Label
    )

    if (-not (Test-Path $Path)) {
        throw "[$Label] File not found: $Path"
    }

    # Force UTF-8 on read -- Windows PowerShell 5.1 only auto-detects UTF-8 when a
    # BOM is present; without one it silently falls back to the system codepage and
    # corrupts multi-byte characters (arrows, em-dashes) before comparison even runs.
    $content = Get-Content -Path $Path -Raw -Encoding UTF8

    $normContent = $content -replace "`r`n", "`n"
    $normFind    = $Find    -replace "`r`n", "`n"
    $normReplace = $Replace -replace "`r`n", "`n"

    if ($normContent.Contains($normReplace) -and -not $normContent.Contains($normFind)) {
        Write-Host "[SKIP] $Label already applied -> $Path"
        return
    }

    $occurrences = ([regex]::Matches($normContent, [regex]::Escape($normFind))).Count

    if ($occurrences -eq 0) {
        throw "[$Label] Anchor NOT FOUND in $Path. The file has likely drifted from what this script expects (or has pre-existing encoding corruption, as seen before in this repo). Aborting without modifying it — paste the current file content back to Claude to regenerate this block."
    }
    if ($occurrences -gt 1) {
        throw "[$Label] Anchor matched $occurrences times in $Path (expected exactly 1). Refusing to guess which one. Aborting without modifying it."
    }

    $updated = $normContent.Replace($normFind, $normReplace)
    $updated = $updated -replace "`n", "`r`n"

    Set-ContentWithRetry -Path $Path -Value $updated
    Write-Host "[OK] $Label -> $Path"
}

$controllersRoot = "Edvanz.API/Controllers"

# ═══════════════════════════════════════════════════════════════════════════
# 1. ParentScopedApiBaseController.cs — ChildResolution gains the parent's
#    own language preference (needed for the teacher-subject display language)
# ═══════════════════════════════════════════════════════════════════════════

$baseControllerPath = "$controllersRoot/ParentScopedApiBaseController.cs"

Replace-InFile -Path $baseControllerPath -Label "ChildResolution struct — add ParentLanguagePreference" -Find @'
    protected readonly struct ChildResolution
    {
        public long? TeacherStudentId { get; }
        public IActionResult? ErrorResponse { get; }

        private ChildResolution(long? id, IActionResult? error)
        {
            TeacherStudentId = id;
            ErrorResponse = error;
        }

        public static ChildResolution Ok(long teacherStudentId) => new(teacherStudentId, null);
        public static ChildResolution Error(IActionResult response) => new(null, response);
    }
'@ -Replace @'
    protected readonly struct ChildResolution
    {
        public long? TeacherStudentId { get; }

        /// <summary>The resolved PARENT's own language preference ("ar"/"en") — NOT the
        /// child's. Added Phase 5 (parent parity) for language-aware content such as the
        /// teacher's subject display name.</summary>
        public string? ParentLanguagePreference { get; }
        public IActionResult? ErrorResponse { get; }

        private ChildResolution(long? id, string? parentLanguagePreference, IActionResult? error)
        {
            TeacherStudentId = id;
            ParentLanguagePreference = parentLanguagePreference;
            ErrorResponse = error;
        }

        public static ChildResolution Ok(long teacherStudentId, string? parentLanguagePreference)
            => new(teacherStudentId, parentLanguagePreference, null);
        public static ChildResolution Error(IActionResult response) => new(null, null, response);
    }
'@

Replace-InFile -Path $baseControllerPath -Label "ResolveChildForParentAsync Method A Ok(...) call" -Find @'
            return ChildResolution.Ok(link.TeacherStudentId.Value);
'@ -Replace @'
            return ChildResolution.Ok(link.TeacherStudentId.Value, parentUser.LanguagePreference);
'@

Replace-InFile -Path $baseControllerPath -Label "ResolveChildForParentAsync Method B Ok(...) call" -Find @'
        return ChildResolution.Ok(parentLink.TeacherStudentId.Value);
'@ -Replace @'
        return ChildResolution.Ok(parentLink.TeacherStudentId.Value, parentUser.LanguagePreference);
'@

# ═══════════════════════════════════════════════════════════════════════════
# 2. IVideoService.cs — three new interface declarations
# ═══════════════════════════════════════════════════════════════════════════

$iVideoServicePath = "Edvanz.Application/ServiceContract/IVideoService.cs"

Replace-InFile -Path $iVideoServicePath -Label "IVideoService — add parent read-flow declarations" -Find @'
    /// <summary>
    /// V3 drill-down — the student's visible videos within one unit (same enriched shape as
    /// <see cref="GetStudentVideosAsync"/>). Runs the runtime module-active gate.
    /// </summary>
    Task<Result<PaginatedResponse<List<StudentVideoListItemDto>>>> GetStudentVideosInUnitAsync(
        long teacherId, long teacherStudentId, long unitId, StudentVideoListRequest request, string? studentLanguage);

    /// <summary>
    /// Records the student's Open event and returns the embed URL + resume
'@ -Replace @'
    /// <summary>
    /// V3 drill-down — the student's visible videos within one unit (same enriched shape as
    /// <see cref="GetStudentVideosAsync"/>). Runs the runtime module-active gate.
    /// </summary>
    Task<Result<PaginatedResponse<List<StudentVideoListItemDto>>>> GetStudentVideosInUnitAsync(
        long teacherId, long teacherStudentId, long unitId, StudentVideoListRequest request, string? studentLanguage);

    // ══════════════════════════════════════════════════════════════════════
    // PARENT READ FLOWS (Phase 5, parent parity)
    // ══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Paged list of videos visible to a linked child, for the calling parent. Story 7b —
    /// mirrors <see cref="GetStudentVideosAsync"/> exactly (same module-active gate, same
    /// underlying scope query, same enriched DTO including the child's own watch state) — the
    /// only difference is who the caller is. Includes NO write capability: there is no parent
    /// equivalent of <see cref="StartWatchAsync"/> / <see cref="StopWatchAsync"/> (D6 — parent
    /// surfaces are read-only, never trigger a VideoAnalytics write).
    /// </summary>
    /// <param name="teacherId">The teacher whose videos are being listed.</param>
    /// <param name="teacherStudentId">The linked child's TeacherStudent.Id under that teacher,
    /// resolved by the controller (AAM-FR-06.3 Method A/B).</param>
    /// <param name="parentLanguage">The PARENT's own language preference (not the child's) —
    /// governs which language the teacher's subject name displays in.</param>
    Task<Result<PaginatedResponse<List<StudentVideoListItemDto>>>>
        GetParentVideosAsync(
            long teacherId, long teacherStudentId, StudentVideoListRequest request, string? parentLanguage);

    /// <summary>
    /// The units a linked child can see under a teacher, for the calling parent. Mirrors
    /// <see cref="GetStudentUnitsAsync"/> exactly.
    /// </summary>
    Task<Result<List<StudentVideoUnitDto>>> GetParentUnitsAsync(
        long teacherId, long teacherStudentId, string? parentLanguage);

    /// <summary>
    /// Unit drill-down for a linked child's visible videos, for the calling parent. Mirrors
    /// <see cref="GetStudentVideosInUnitAsync"/> exactly.
    /// </summary>
    Task<Result<PaginatedResponse<List<StudentVideoListItemDto>>>> GetParentVideosInUnitAsync(
        long teacherId, long teacherStudentId, long unitId, StudentVideoListRequest request, string? parentLanguage);

    /// <summary>
    /// Records the student's Open event and returns the embed URL + resume
'@

# ═══════════════════════════════════════════════════════════════════════════
# 3. VideoService.cs — class doc, rename shared builder, add 3 implementations
# ═══════════════════════════════════════════════════════════════════════════

$videoServicePath = "Edvanz.Application/Services/VideoService.cs"

# 3a. Class doc comment — the "deferred" note is no longer true
Replace-InFile -Path $videoServicePath -Label "VideoService class doc comment" -Find @'
/// PARENT ENDPOINT — DEFERRED (Q1(c)):
/// v1 ships without a parent endpoint. The student endpoint plus the
/// existing ParentChild → StudentTeacherLink / ParentChildTeacherLink data
/// model is sufficient to add it later without DB changes.
/// </summary>
'@ -Replace @'
/// PARENT ENDPOINT (Phase 5, parent parity — implemented 2026-08):
/// GetParentVideosAsync / GetParentUnitsAsync / GetParentVideosInUnitAsync mirror their
/// student twins exactly (same module-active gate, same scope query, same shared
/// BuildVideoListPageAsync mapper) — no DB changes were needed, confirming the original
/// Q1(c) deferral note. Quiz result/review reuse IStudentVideoExamService.GetResultAsync /
/// GetReviewAsync unchanged, since both are pure reads keyed by (teacherId,
/// teacherStudentId, videoAssetId) with no caller-identity branch to wrap.
/// </summary>
'@

# 3b. Rename the shared builder — doc comment + signature
Replace-InFile -Path $videoServicePath -Label "Rename BuildStudentVideoPageAsync -> BuildVideoListPageAsync" -Find @'
    /// <summary>
    /// Shared mapper for the student video list and the V3 unit drill-down. Batch-resolves the
    /// page's cover photos + attachments (never per-row) and resolves the teacher's subject ONCE
    /// (all rows share one teacher), then maps rows to the enriched DTO (V2 quiz info, V4 watch
    /// status, subject).
    /// </summary>
    private async Task<PaginatedResponse<List<StudentVideoListItemDto>>> BuildStudentVideoPageAsync(
'@ -Replace @'
    /// <summary>
    /// Shared mapper for the student AND parent video lists and the V3 unit drill-down.
    /// Batch-resolves the page's cover photos + attachments (never per-row) and resolves the
    /// teacher's subject ONCE (all rows share one teacher), then maps rows to the enriched DTO
    /// (V2 quiz info, V4 watch status, subject). Caller-neutral by design — a parent's child and
    /// a student are the same teacherStudentId shape to this method; it never inspects who's
    /// asking. Renamed from BuildStudentVideoPageAsync (Phase 5, parent parity) once a second
    /// caller family started using it.
    /// </summary>
    private async Task<PaginatedResponse<List<StudentVideoListItemDto>>> BuildVideoListPageAsync(
'@

# 3c. Call site inside GetStudentVideosAsync
Replace-InFile -Path $videoServicePath -Label "GetStudentVideosAsync call site rename" -Find @'
        var response = await BuildStudentVideoPageAsync(
            teacherId, rows, totalCount, request.Page, request.PageSize, studentLanguage);
        return Result<PaginatedResponse<List<StudentVideoListItemDto>>>.Success(response, _localizer);
    }

    /// <inheritdoc />
    public async Task<Result<PaginatedResponse<List<StudentVideoListItemDto>>>> GetStudentVideosInUnitAsync(
'@ -Replace @'
        var response = await BuildVideoListPageAsync(
            teacherId, rows, totalCount, request.Page, request.PageSize, studentLanguage);
        return Result<PaginatedResponse<List<StudentVideoListItemDto>>>.Success(response, _localizer);
    }

    /// <inheritdoc />
    public async Task<Result<PaginatedResponse<List<StudentVideoListItemDto>>>> GetStudentVideosInUnitAsync(
'@

# 3d. Call site inside GetStudentVideosInUnitAsync
Replace-InFile -Path $videoServicePath -Label "GetStudentVideosInUnitAsync call site rename" -Find @'
        var response = await BuildStudentVideoPageAsync(
            teacherId, rows, totalCount, request.Page, request.PageSize, studentLanguage);
        return Result<PaginatedResponse<List<StudentVideoListItemDto>>>.Success(response, _localizer);
    }

    /// <inheritdoc />
    public async Task<Result<List<StudentVideoUnitDto>>> GetStudentUnitsAsync(
'@ -Replace @'
        var response = await BuildVideoListPageAsync(
            teacherId, rows, totalCount, request.Page, request.PageSize, studentLanguage);
        return Result<PaginatedResponse<List<StudentVideoListItemDto>>>.Success(response, _localizer);
    }

    /// <inheritdoc />
    public async Task<Result<List<StudentVideoUnitDto>>> GetStudentUnitsAsync(
'@

# 3e. Insert the three new PARENT method implementations, after GetStudentUnitsAsync,
#     before the (now renamed) shared builder's doc comment.
Replace-InFile -Path $videoServicePath -Label "VideoService — add parent read-flow implementations" -Find @'
        return Result<List<StudentVideoUnitDto>>.Success(items, _localizer);
    }

    /// <summary>
    /// Shared mapper for the student AND parent video lists and the V3 unit drill-down.
'@ -Replace @'
        return Result<List<StudentVideoUnitDto>>.Success(items, _localizer);
    }

    // ══════════════════════════════════════════════════════════════════════
    // PARENT — VIDEO LIST (Phase 5, parent parity)
    // ══════════════════════════════════════════════════════════════════════

    /// <inheritdoc />
    public async Task<Result<PaginatedResponse<List<StudentVideoListItemDto>>>>
        GetParentVideosAsync(
            long teacherId, long teacherStudentId, StudentVideoListRequest request, string? parentLanguage)
    {
        var moduleGate = await CheckModuleActiveAsync<PaginatedResponse<List<StudentVideoListItemDto>>>(teacherId);
        if (moduleGate is not null) return moduleGate;

        var (rows, totalCount) = await _unitOfWork.VideoAssetsRepo
            .GetVisibleVideosForStudentAsync(teacherId, teacherStudentId, request.Page, request.PageSize);

        var response = await BuildVideoListPageAsync(
            teacherId, rows, totalCount, request.Page, request.PageSize, parentLanguage);
        return Result<PaginatedResponse<List<StudentVideoListItemDto>>>.Success(response, _localizer);
    }

    /// <inheritdoc />
    public async Task<Result<PaginatedResponse<List<StudentVideoListItemDto>>>> GetParentVideosInUnitAsync(
        long teacherId, long teacherStudentId, long unitId, StudentVideoListRequest request, string? parentLanguage)
    {
        var moduleGate = await CheckModuleActiveAsync<PaginatedResponse<List<StudentVideoListItemDto>>>(teacherId);
        if (moduleGate is not null) return moduleGate;

        var (rows, totalCount) = await _unitOfWork.VideoAssetsRepo
            .GetVisibleVideosForStudentInUnitAsync(teacherId, teacherStudentId, unitId, request.Page, request.PageSize);

        var response = await BuildVideoListPageAsync(
            teacherId, rows, totalCount, request.Page, request.PageSize, parentLanguage);
        return Result<PaginatedResponse<List<StudentVideoListItemDto>>>.Success(response, _localizer);
    }

    /// <inheritdoc />
    public async Task<Result<List<StudentVideoUnitDto>>> GetParentUnitsAsync(
        long teacherId, long teacherStudentId, string? parentLanguage)
    {
        var moduleGate = await CheckModuleActiveAsync<List<StudentVideoUnitDto>>(teacherId);
        if (moduleGate is not null) return moduleGate;

        var units = await _unitOfWork.VideoAssetsRepo
            .GetStudentVisibleUnitsAsync(teacherId, teacherStudentId);

        string subject = ResolveTeacherSubject(
            await _unitOfWork.VideoAssetsRepo.GetTeacherSubjectAsync(teacherId), parentLanguage);

        var items = units.Select(u => new StudentVideoUnitDto
        {
            Id = u.Id,
            Title = u.Title,
            Description = u.Description,
            VideoCount = u.VideoCount,
            QuizCount = u.QuizVideoCount,
            Subject = subject,
        }).ToList();

        return Result<List<StudentVideoUnitDto>>.Success(items, _localizer);
    }

    /// <summary>
    /// Shared mapper for the student AND parent video lists and the V3 unit drill-down.
'@

# ═══════════════════════════════════════════════════════════════════════════
# 4. NEW FILE — ParentVideosController.cs
# ═══════════════════════════════════════════════════════════════════════════

$parentVideosControllerPath = "$controllersRoot/ParentVideosController.cs"

if (Test-Path $parentVideosControllerPath) {
    Write-Host "[SKIP] Already exists -> $parentVideosControllerPath"
} else {

$parentVideosControllerContent = @'
using Edvanz.API.Attributes;
using Edvanz.Application.Dtos.VideoContentManagement;
using Edvanz.Application.IservicesContract;
using Edvanz.Application.ServiceContract;
using Edvanz.Domain.Interfaces;
using Edvanz.Domain.Resources;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;

namespace Edvanz.API.Controllers;

/// <summary>
/// Video Content Management Module (Module 14) — parent-facing READ endpoints.
///
/// SEPARATE CONTROLLER RATIONALE: mirrors <see cref="StudentVideosController"/> — parents carry
/// no module claim, and the service needs (teacherId, teacherStudentId), which this controller
/// resolves via <see cref="ParentScopedApiBaseController.ResolveChildForParentAsync"/> (parent
/// ownership of the child, then Method A/B teacher-link branch, AAM-FR-06.3).
///
/// READ-ONLY (D6 — locked decision, parent-parity phase plan): list / units / unit drill-down
/// only. No start/stop-watch — a parent never triggers a VideoAnalytics write. The child's own
/// watch state (HasOpened, LastOpenedAt, WatchStatus) is still visible on each row — a parent
/// can see THAT their child watched something, they just can't watch it themselves through this
/// surface or influence the child's own watch analytics.
///
/// AUTH: [ModulePermission(roles: ["Parent"], roleOnly: true)] — parent role only.
///
/// SECURITY: route childId and teacherId are untrusted — see
/// <see cref="ParentScopedApiBaseController.ResolveChildForParentAsync"/> for the full resolution
/// chain. Teacher-controlled parent visibility (ParentVisibilityVideo) governs the home-aggregate
/// tile only (Phase 8) — like the student endpoints it mirrors, this list itself is gated purely
/// by module-active + video scope, not by the visibility flag (established precedent, see
/// VideoService — GetStudentVideosAsync has never checked StudentVisibilityVideo either).
/// </summary>
[Route("api/videos/parent")]
[Authorize]
public sealed class ParentVideosController : ParentScopedApiBaseController
{
    private readonly IVideoService _service;

    public ParentVideosController(
        IVideoService service,
        ICurrentUserService currentUser,
        IUnitOfWork unitOfWork,
        IStringLocalizer<Messages> localizer)
        : base(currentUser, unitOfWork, localizer)
    {
        _service = service;
    }

    // ──────────────────────────────────────────────────────────────────────
    // CHILD VIDEO LIST
    // GET /api/videos/parent/children/{childId}/teachers/{teacherId}
    // ──────────────────────────────────────────────────────────────────────
    [HttpGet("children/{childId:long}/teachers/{teacherId:long}")]
    [ModulePermission(roles: new[] { "Parent" }, roleOnly: true)]
    [ProducesResponseType(typeof(Edvanz.Application.Dtos.Result<Edvanz.Application.Dtos.PaginatedResponse<System.Collections.Generic.List<StudentVideoListItemDto>>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(object), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetChildVideos(
        [FromRoute] long childId, [FromRoute] long teacherId, [FromQuery] StudentVideoListRequest request)
    {
        var resolution = await ResolveChildForParentAsync(childId, teacherId);
        if (resolution.ErrorResponse is not null) return resolution.ErrorResponse;

        var result = await _service.GetParentVideosAsync(
            teacherId, resolution.TeacherStudentId!.Value, request, resolution.ParentLanguagePreference);
        return ToResponse(result);
    }

    // ──────────────────────────────────────────────────────────────────────
    // CHILD VIDEO UNITS
    // GET /api/videos/parent/children/{childId}/teachers/{teacherId}/units
    // ──────────────────────────────────────────────────────────────────────
    [HttpGet("children/{childId:long}/teachers/{teacherId:long}/units")]
    [ModulePermission(roles: new[] { "Parent" }, roleOnly: true)]
    [ProducesResponseType(typeof(Edvanz.Application.Dtos.Result<System.Collections.Generic.List<StudentVideoUnitDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(object), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetChildUnits([FromRoute] long childId, [FromRoute] long teacherId)
    {
        var resolution = await ResolveChildForParentAsync(childId, teacherId);
        if (resolution.ErrorResponse is not null) return resolution.ErrorResponse;

        var result = await _service.GetParentUnitsAsync(
            teacherId, resolution.TeacherStudentId!.Value, resolution.ParentLanguagePreference);
        return ToResponse(result);
    }

    // ──────────────────────────────────────────────────────────────────────
    // CHILD VIDEOS IN A UNIT
    // GET /api/videos/parent/children/{childId}/teachers/{teacherId}/units/{unitId}/videos
    // ──────────────────────────────────────────────────────────────────────
    [HttpGet("children/{childId:long}/teachers/{teacherId:long}/units/{unitId:long}/videos")]
    [ModulePermission(roles: new[] { "Parent" }, roleOnly: true)]
    [ProducesResponseType(typeof(Edvanz.Application.Dtos.Result<Edvanz.Application.Dtos.PaginatedResponse<System.Collections.Generic.List<StudentVideoListItemDto>>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(object), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetChildVideosInUnit(
        [FromRoute] long childId, [FromRoute] long teacherId, [FromRoute] long unitId,
        [FromQuery] StudentVideoListRequest request)
    {
        var resolution = await ResolveChildForParentAsync(childId, teacherId);
        if (resolution.ErrorResponse is not null) return resolution.ErrorResponse;

        var result = await _service.GetParentVideosInUnitAsync(
            teacherId, resolution.TeacherStudentId!.Value, unitId, request, resolution.ParentLanguagePreference);
        return ToResponse(result);
    }
}
'@

Set-ContentWithRetry -Path $parentVideosControllerPath -Value ($parentVideosControllerContent -replace "`n", "`r`n")
Write-Host "[OK] Created $parentVideosControllerPath"

}

# ═══════════════════════════════════════════════════════════════════════════
# 5. NEW FILE — ParentVideoExamsController.cs
# ═══════════════════════════════════════════════════════════════════════════

$parentVideoExamsControllerPath = "$controllersRoot/ParentVideoExamsController.cs"

if (Test-Path $parentVideoExamsControllerPath) {
    Write-Host "[SKIP] Already exists -> $parentVideoExamsControllerPath"
} else {

$parentVideoExamsControllerContent = @'
using Edvanz.API.Attributes;
using Edvanz.Application.Dtos.VideoContentManagement;
using Edvanz.Application.IservicesContract;
using Edvanz.Application.ServiceContract;
using Edvanz.Domain.Interfaces;
using Edvanz.Domain.Resources;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;

namespace Edvanz.API.Controllers;

/// <summary>
/// Video Content Management Module (Module 14) — parent-facing VIDEO-QUIZ READ endpoints.
///
/// Separate controller from <see cref="ParentVideosController"/> (which owns list/units),
/// mirroring the student split (<see cref="StudentVideoExamsController"/> vs
/// <see cref="StudentVideosController"/>). Owns ONLY the quiz result + review reads for a
/// video's VideoExam — reuses <see cref="IStudentVideoExamService.GetResultAsync"/> and
/// <see cref="IStudentVideoExamService.GetReviewAsync"/> UNCHANGED: both are pure reads keyed
/// purely by (teacherId, teacherStudentId, videoAssetId) with no caller-identity branching, so
/// there is nothing for a dedicated parent-named service method to add — the interface stays
/// exactly as it is.
///
/// READ-ONLY (D6 — locked decision): NO take-screen, submit, or retry. A parent never sees the
/// answer key before their child has attempted the quiz (review only reveals correctness once
/// the child's OWN attempt is finalized, same rule as the student path), and a parent can never
/// start or reset the child's attempt.
///
/// AUTH: [ModulePermission(roles: ["Parent"], roleOnly: true)] — parent role only.
/// </summary>
[Route("api/videos/parent")]
[Authorize]
public sealed class ParentVideoExamsController : ParentScopedApiBaseController
{
    private readonly IStudentVideoExamService _service;

    public ParentVideoExamsController(
        IStudentVideoExamService service,
        ICurrentUserService currentUser,
        IUnitOfWork unitOfWork,
        IStringLocalizer<Messages> localizer)
        : base(currentUser, unitOfWork, localizer)
    {
        _service = service;
    }

    // ──────────────────────────────────────────────────────────────────────
    // CHILD VIDEO-QUIZ RESULT
    // GET /api/videos/parent/children/{childId}/teachers/{teacherId}/videos/{videoAssetId}/exam/result
    // ──────────────────────────────────────────────────────────────────────
    [HttpGet("children/{childId:long}/teachers/{teacherId:long}/videos/{videoAssetId:long}/exam/result")]
    [ModulePermission(roles: new[] { "Parent" }, roleOnly: true)]
    [ProducesResponseType(typeof(Edvanz.Application.Dtos.Result<VideoExamStatsDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(object), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetChildResult(
        [FromRoute] long childId, [FromRoute] long teacherId, [FromRoute] long videoAssetId)
    {
        var resolution = await ResolveChildForParentAsync(childId, teacherId);
        if (resolution.ErrorResponse is not null) return resolution.ErrorResponse;

        var result = await _service.GetResultAsync(teacherId, resolution.TeacherStudentId!.Value, videoAssetId);
        return ToResponse(result);
    }

    // ──────────────────────────────────────────────────────────────────────
    // CHILD VIDEO-QUIZ REVIEW
    // GET /api/videos/parent/children/{childId}/teachers/{teacherId}/videos/{videoAssetId}/exam/review
    // ──────────────────────────────────────────────────────────────────────
    [HttpGet("children/{childId:long}/teachers/{teacherId:long}/videos/{videoAssetId:long}/exam/review")]
    [ModulePermission(roles: new[] { "Parent" }, roleOnly: true)]
    [ProducesResponseType(typeof(Edvanz.Application.Dtos.Result<VideoExamReviewDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(object), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetChildReview(
        [FromRoute] long childId, [FromRoute] long teacherId, [FromRoute] long videoAssetId)
    {
        var resolution = await ResolveChildForParentAsync(childId, teacherId);
        if (resolution.ErrorResponse is not null) return resolution.ErrorResponse;

        var result = await _service.GetReviewAsync(teacherId, resolution.TeacherStudentId!.Value, videoAssetId);
        return ToResponse(result);
    }
}
'@

Set-ContentWithRetry -Path $parentVideoExamsControllerPath -Value ($parentVideoExamsControllerContent -replace "`n", "`r`n")
Write-Host "[OK] Created $parentVideoExamsControllerPath"

}

Write-Host ""
Write-Host "Phase 5 applied. Next steps:"
Write-Host "  1. dotnet build."
Write-Host "  2. No migration, no DI registration changes needed -- IVideoService and"
Write-Host "     IStudentVideoExamService are already registered; controllers are auto-discovered."
Write-Host "  3. Postman regression: existing student video/quiz endpoints untouched (verify"
Write-Host "     BuildVideoListPageAsync rename didn't break anything -- same body, new name)."
Write-Host "  4. New coverage to test: a parent listing videos/units/videos-in-unit for a linked"
Write-Host "     child (both Method A and Method B), and a parent reading quiz result/review for"
Write-Host "     a child's finalized attempt. Confirm: no start/stop/take/submit/retry route exists"
Write-Host "     under api/videos/parent (404, not 403 -- the route itself shouldn't exist)."
Write-Host "  5. CLAUDE.md doesn't yet mention the parent video surface -- worth a note when you"
Write-Host "     do a docs pass; not blocking."
