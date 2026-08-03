<#
    Phase 6 — ParentOnlineExamsController (Sprint 2, final phase)
    ─────────────────────────────────────────────────────────────────────────
    Read-only parent surface for the Online Exam module. D6 (locked): no
    take-screen, answer-submit, exam-submit, self-block, or violation-recording
    -- a parent never starts, saves progress on, or submits their child's exam.

    DESIGN NOTE: zero service/interface changes this phase. Read GetMyExamsAsync,
    GetResultAsync, and GetReviewAsync in full before writing anything -- all
    three are pure reads keyed only by (teacherId, teacherStudentId[, onlineExamId])
    with NO caller-identity branching and NO visibility-flag check (matches the
    Phase 5 finding for video: StudentVisibilityOnlineExamDefault only gates the
    home-aggregate tile, not the list read itself). There is nothing for a
    dedicated parent-named service method to add, so none was added -- same
    reasoning already applied to ParentVideoExamsController's result/review reuse.

    Also noted, not fixed (out of scope): GetMyExamsAsync has no BR-ADM-010
    module-active gate in this module, unlike Video's CheckModuleActiveAsync --
    a pre-existing inconsistency in the Online Exam module, faithfully mirrored
    (not introduced) by reusing the method as-is.

    Files touched:
      NEW    Edvanz.API/Controllers/ParentOnlineExamsController.cs

    No migration. No DI changes -- IStudentOnlineExamService is already
    registered; the controller is auto-discovered.

    USAGE
    -----
        powershell -ExecutionPolicy Bypass -File .\phase6-parent-online-exams.ps1

    Safe to re-run from the top (idempotent).
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

$controllersRoot = "Edvanz.API/Controllers"
$newControllerPath = "$controllersRoot/ParentOnlineExamsController.cs"

if (Test-Path $newControllerPath) {
    Write-Host "[SKIP] Already exists -> $newControllerPath"
} else {

$newControllerContent = @'
using Edvanz.Domain.Resources;
using Microsoft.Extensions.Localization;
using Edvanz.API.Attributes;
using Edvanz.Application.Dtos;
using Edvanz.Application.IservicesContract;
using Edvanz.Application.ServiceContract;
using Edvanz.Domain.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Edvanz.API.Controllers;

/// <summary>
/// Online Exam Module — parent-facing READ endpoints.
///
/// SEPARATE CONTROLLER RATIONALE: mirrors <see cref="StudentOnlineExamsController"/> — parents
/// carry no module claim, and the service needs (teacherId, teacherStudentId), which this
/// controller resolves via <see cref="ParentScopedApiBaseController.ResolveChildForParentAsync"/>
/// (parent ownership of the child, then Method A/B teacher-link branch, AAM-FR-06.3).
///
/// REUSES <see cref="IStudentOnlineExamService"/> COMPLETELY UNCHANGED — GetMyExamsAsync,
/// GetResultAsync, and GetReviewAsync are all pure reads keyed purely by (teacherId,
/// teacherStudentId[, onlineExamId]) with no caller-identity branching (confirmed by reading
/// every one of their bodies before writing this controller) — there is nothing for a
/// dedicated parent-named service method to add, so none was added. Same reasoning already
/// applied to <see cref="ParentVideoExamsController"/>'s result/review reuse (Phase 5).
///
/// NOTE: GetMyExamsAsync has no BR-ADM-010 module-active gate in this module (unlike Video's
/// CheckModuleActiveAsync) — a pre-existing inconsistency, faithfully mirrored here (not
/// introduced, not fixed — out of scope, flagged for a future pass).
///
/// READ-ONLY (D6 — locked decision, parent-parity phase plan): NO take-screen, answer-submit,
/// exam-submit, self-block, or violation-recording. A parent never starts, saves progress on,
/// or submits their child's exam.
///
/// AUTH: [ModulePermission(roles: ["Parent"], roleOnly: true)] — parent role only.
/// </summary>
[Route("api/online-exams/parent")]
[Authorize]
public sealed class ParentOnlineExamsController : ParentScopedApiBaseController
{
    private readonly IStudentOnlineExamService _service;

    public ParentOnlineExamsController(
        IStudentOnlineExamService service,
        ICurrentUserService currentUser,
        IUnitOfWork unitOfWork,
        IStringLocalizer<Messages> localizer)
        : base(currentUser, unitOfWork, localizer)
    {
        _service = service;
    }

    // ──────────────────────────────────────────────────────────────────────
    // CHILD EXAM LIST (upcoming/past split)
    // GET /api/online-exams/parent/children/{childId}/teachers/{teacherId}
    // ──────────────────────────────────────────────────────────────────────
    [HttpGet("children/{childId:long}/teachers/{teacherId:long}")]
    [ModulePermission(roles: new[] { "Parent" }, roleOnly: true)]
    [ProducesResponseType(typeof(Edvanz.Application.Dtos.Result<StudentOnlineExamListDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(object), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetChildExams([FromRoute] long childId, [FromRoute] long teacherId)
    {
        var resolution = await ResolveChildForParentAsync(childId, teacherId);
        if (resolution.ErrorResponse is not null) return resolution.ErrorResponse;

        var result = await _service.GetMyExamsAsync(
            teacherId, resolution.TeacherStudentId!.Value, resolution.ParentLanguagePreference);
        return ToResponse(result);
    }

    // ──────────────────────────────────────────────────────────────────────
    // CHILD EXAM RESULT
    // GET /api/online-exams/parent/children/{childId}/teachers/{teacherId}/{onlineExamId}/result
    // ──────────────────────────────────────────────────────────────────────
    [HttpGet("children/{childId:long}/teachers/{teacherId:long}/{onlineExamId:long}/result")]
    [ModulePermission(roles: new[] { "Parent" }, roleOnly: true)]
    [ProducesResponseType(typeof(Edvanz.Application.Dtos.Result<OnlineExamStatsDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(object), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetChildResult(
        [FromRoute] long childId, [FromRoute] long teacherId, [FromRoute] long onlineExamId)
    {
        var resolution = await ResolveChildForParentAsync(childId, teacherId);
        if (resolution.ErrorResponse is not null) return resolution.ErrorResponse;

        var result = await _service.GetResultAsync(teacherId, resolution.TeacherStudentId!.Value, onlineExamId);
        return ToResponse(result);
    }

    // ──────────────────────────────────────────────────────────────────────
    // CHILD EXAM REVIEW (answers) — matches the student route's own "answers"
    // naming (not "review" — each module keeps its own established suffix).
    // GET /api/online-exams/parent/children/{childId}/teachers/{teacherId}/{onlineExamId}/answers
    // ──────────────────────────────────────────────────────────────────────
    [HttpGet("children/{childId:long}/teachers/{teacherId:long}/{onlineExamId:long}/answers")]
    [ModulePermission(roles: new[] { "Parent" }, roleOnly: true)]
    [ProducesResponseType(typeof(Edvanz.Application.Dtos.Result<OnlineExamReviewDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(object), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetChildReview(
        [FromRoute] long childId, [FromRoute] long teacherId, [FromRoute] long onlineExamId)
    {
        var resolution = await ResolveChildForParentAsync(childId, teacherId);
        if (resolution.ErrorResponse is not null) return resolution.ErrorResponse;

        var result = await _service.GetReviewAsync(teacherId, resolution.TeacherStudentId!.Value, onlineExamId);
        return ToResponse(result);
    }
}
'@

Set-ContentWithRetry -Path $newControllerPath -Value ($newControllerContent -replace "`n", "`r`n")
Write-Host "[OK] Created $newControllerPath"

}

Write-Host ""
Write-Host "Phase 6 applied. Next steps:"
Write-Host "  1. dotnet build."
Write-Host "  2. No migration, no DI changes needed."
Write-Host "  3. Postman regression: new coverage only (nothing existing was touched) -- a parent"
Write-Host "     listing a linked child's exams (both Method A and Method B), reading result and"
Write-Host "     answers/review for a finalized attempt. Confirm no take/submit/block/violation"
Write-Host "     route exists under api/online-exams/parent (404, not 403)."
Write-Host "  4. This closes Sprint 2 (Phases 5-6). CLAUDE.md still doesn't mention either new"
Write-Host "     parent surface -- worth a combined docs pass covering both Phase 5 and 6 when"
Write-Host "     you have a moment; not blocking."
