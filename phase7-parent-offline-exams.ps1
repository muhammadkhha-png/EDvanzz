<#
    Phase 7 — ParentAssignmentObligationsController (OFFLINE EXAM half only)
    ─────────────────────────────────────────────────────────────────────────
    Read-only parent surface for Module 6's offline-exam list.

    SCOPE NOTE: homework is intentionally NOT included in this script. The
    student side has no homework list endpoint anywhere in the app --
    StudentTeacherHomeService.GetTeacherHomeAsync hardcodes
    `Homework = new HomeHomeworkDto { Visible = vHomework, Count = 0 }` with the
    comment "read surface not built yet -- keys only". There is no existing
    student pattern to mirror, so building one is a standalone design decision
    (new DTO shape, new repo query, decide what "homework list" even returns),
    not a parity mirror -- raised separately rather than invented silently.

    DESIGN NOTE: zero service/interface changes. GetMyOfflineExamsAsync has no
    caller-identity branching and no visibility-flag check inside it (matches
    the same precedent already found for video and online-exam lists --
    StudentVisibilityExamDefault only gates the home-aggregate tile, not this
    list read) -- reused completely unchanged, same reasoning as Phases 5/6.

    Files touched:
      NEW    Edvanz.API/Controllers/ParentAssignmentObligationsController.cs

    No migration. No DI changes -- IExamHomeworkService is already registered;
    the controller is auto-discovered.

    USAGE
    -----
        powershell -ExecutionPolicy Bypass -File .\phase7-parent-offline-exams.ps1

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
$newControllerPath = "$controllersRoot/ParentAssignmentObligationsController.cs"

if (Test-Path $newControllerPath) {
    Write-Host "[SKIP] Already exists -> $newControllerPath"
} else {

$newControllerContent = @'
using Edvanz.Domain.Resources;
using Microsoft.Extensions.Localization;
using Edvanz.API.Attributes;
using Edvanz.Application.IservicesContract;
using Edvanz.Application.ServiceContract;
using Edvanz.Domain.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Edvanz.API.Controllers;

/// <summary>
/// Module 6 (Exams &amp; Homework) — parent-facing READ endpoints.
///
/// SEPARATE CONTROLLER RATIONALE: mirrors <see cref="StudentAssignmentObligationsController"/> —
/// parents carry no module claim, and the service needs (teacherId, teacherStudentId), which this
/// controller resolves via <see cref="ParentScopedApiBaseController.ResolveChildForParentAsync"/>
/// (parent ownership of the child, then Method A/B teacher-link branch, AAM-FR-06.3).
///
/// SCOPE — OFFLINE EXAMS ONLY: the student side currently has NO homework list endpoint
/// anywhere in the app — <c>StudentTeacherHomeService.GetTeacherHomeAsync</c> hardcodes
/// <c>Homework = new HomeHomeworkDto { Visible = vHomework, Count = 0 }</c> with the comment
/// "read surface not built yet — keys only". There is no student pattern to mirror for
/// homework, so none was built here — that is a standalone design decision (new DTO shape,
/// new repo query), not a parity mirror, and is tracked separately rather than invented here.
///
/// REUSES <see cref="IExamHomeworkService.GetMyOfflineExamsAsync"/> COMPLETELY UNCHANGED — no
/// caller-identity branching, no visibility-flag check inside the method itself (matches the
/// same precedent already found for video and online-exam lists: StudentVisibilityExamDefault
/// only gates the home-aggregate tile, not this list read). Offline exams also have no separate
/// result/review endpoint — the list item itself already carries Score/ScorePercentage/MaxGrade/
/// Rank/Status (teacher-graded, not self-service like video/online quizzes), so the list is the
/// whole surface.
///
/// AUTH: [ModulePermission(roles: ["Parent"], roleOnly: true)] — parent role only.
/// </summary>
[Route("api/assignmentobligations/parent")]
[Authorize]
public sealed class ParentAssignmentObligationsController : ParentScopedApiBaseController
{
    private readonly IExamHomeworkService _service;

    public ParentAssignmentObligationsController(
        IExamHomeworkService service,
        ICurrentUserService currentUser,
        IUnitOfWork unitOfWork,
        IStringLocalizer<Messages> localizer)
        : base(currentUser, unitOfWork, localizer)
    {
        _service = service;
    }

    // ──────────────────────────────────────────────────────────────────────
    // CHILD OFFLINE EXAM LIST
    // GET /api/assignmentobligations/parent/children/{childId}/teachers/{teacherId}/exams
    // ──────────────────────────────────────────────────────────────────────
    [HttpGet("children/{childId:long}/teachers/{teacherId:long}/exams")]
    [ModulePermission(roles: new[] { "Parent" }, roleOnly: true)]
    [ProducesResponseType(typeof(Edvanz.Application.Dtos.Result<Edvanz.Application.Dtos.PaginatedResponse<System.Collections.Generic.List<Edvanz.Application.Dtos.ExamHomework.StudentOfflineExamListItemDto>>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(object), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetChildExams(
        [FromRoute] long childId, [FromRoute] long teacherId,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var resolution = await ResolveChildForParentAsync(childId, teacherId);
        if (resolution.ErrorResponse is not null) return resolution.ErrorResponse;

        var result = await _service.GetMyOfflineExamsAsync(
            teacherId, resolution.TeacherStudentId!.Value, resolution.ParentLanguagePreference, page, pageSize);
        return ToResponse(result);
    }
}
'@

Set-ContentWithRetry -Path $newControllerPath -Value ($newControllerContent -replace "`n", "`r`n")
Write-Host "[OK] Created $newControllerPath"

}

Write-Host ""
Write-Host "Phase 7 (offline-exam half) applied. Next steps:"
Write-Host "  1. dotnet build."
Write-Host "  2. No migration, no DI changes needed."
Write-Host "  3. Postman regression: a parent listing a linked child's offline exams (both Method"
Write-Host "     A and Method B), confirm Score/ScorePercentage/MaxGrade/Rank/Status all populate"
Write-Host "     same as the student list does for the same child-as-student."
Write-Host "  4. Homework list is a separate open decision -- see chat."
