<#
    Phase 1 — Extract ParentScopedApiBaseController
    ─────────────────────────────────────────────────────────────────────────
    Pure refactor. Zero behaviour change. Consolidates the copy-pasted
    ResolveParentUserIdAsync / ResolveChildForParentAsync / ChildResolution /
    NotFoundError / ForbiddenError logic from ParentUserController,
    ParentAttendanceController and ParentPaymentController into one shared
    abstract base class, modelled on ModuleSixApiBaseController.

    Per D2 (locked decision): parent-side only. Student-side duplication
    (StudentAttendanceController, StudentVideosController, etc.) is
    deliberately untouched.

    USAGE
    -----
    Run from the repo root (the folder containing Edvanz.API.sln / the
    Edvanz.API project folder):

        pwsh ./phase1-parent-scoped-base-controller.ps1

    or from Git Bash:

        pwsh.exe ./phase1-parent-scoped-base-controller.ps1

    WHY NOT A GIT PATCH
    --------------------
    git apply / git am match on exact context lines including line-ending
    bytes; a CRLF checkout vs an LF-authored patch fails outright. This
    script instead loads each target file as raw text, normalizes both the
    file content and the anchor text to LF for matching, performs a literal
    (non-regex) single-occurrence replacement, then restores CRLF before
    writing back — so line-ending mismatches can't break the apply step.

    SAFETY
    ------
    - Each anchor must match EXACTLY ONCE in the target file, or the script
      throws and leaves that file untouched (whole-script aborts on first
      failure — no partial application of a single file's edits).
    - The new file (ParentScopedApiBaseController.cs) is only written if it
      does not already exist.
    - Back up or commit your working tree before running, as usual.
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

    $content = Get-Content -Path $Path -Raw

    # Normalize to LF for matching so CRLF-vs-LF authoring differences never
    # cause a false "anchor not found".
    $normContent = $content -replace "`r`n", "`n"
    $normFind    = $Find    -replace "`r`n", "`n"
    $normReplace = $Replace -replace "`r`n", "`n"

    # Idempotency: if this exact edit was already applied (replacement text is
    # present and the original anchor is gone), skip instead of erroring. Makes
    # the whole script safely re-runnable from the top after a partial failure.
    if ($normContent.Contains($normReplace) -and -not $normContent.Contains($normFind)) {
        Write-Host "[SKIP] $Label already applied -> $Path"
        return
    }

    $occurrences = ([regex]::Matches($normContent, [regex]::Escape($normFind))).Count

    if ($occurrences -eq 0) {
        throw "[$Label] Anchor NOT FOUND in $Path. The file has likely drifted from what this script expects. Aborting without modifying it — paste the current file content back to Claude to regenerate this block."
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
# 1. NEW FILE — ParentScopedApiBaseController.cs
# ═══════════════════════════════════════════════════════════════════════════

$newFilePath = Join-Path $controllersRoot "ParentScopedApiBaseController.cs"

if (Test-Path $newFilePath) {
    Write-Host "[SKIP] Already exists -> $newFilePath"
} else {

$newFileContent = @'
using System.Net;
using Edvanz.Application.IservicesContract;
using Edvanz.Domain.Enums;
using Edvanz.Domain.Interfaces;
using Edvanz.Domain.Resources;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;

namespace Edvanz.API.Controllers;

/// <summary>
/// Shared base for the parent-facing controllers (<see cref="ParentUserController"/>,
/// <see cref="ParentAttendanceController"/>, <see cref="ParentPaymentController"/>).
/// Centralizes JWT-to-parent resolution and the child/teacher-link resolution branch
/// (AAM-FR-06.3, Method A / Method B) that was previously copy-pasted across all three
/// controllers.
///
/// SCOPE (D2 — locked decision, parent-parity phase plan): this consolidation is
/// parent-side ONLY. The equivalent student-side duplication (StudentAttendanceController,
/// StudentVideosController, StudentOnlineExamsController, StudentAssignmentObligationsController)
/// is deliberately left untouched — separate ticket.
///
/// Inherits <see cref="ApiBaseController"/> so <c>ToResponse&lt;T&gt;</c> and the inherited
/// <c>[ApiController] / [Route("api/[controller]")]</c> attributes still apply.
/// </summary>
public abstract class ParentScopedApiBaseController : ApiBaseController
{
    private readonly ICurrentUserService _currentUser;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IStringLocalizer<Messages> _localizer;

    protected ParentScopedApiBaseController(
        ICurrentUserService currentUser,
        IUnitOfWork unitOfWork,
        IStringLocalizer<Messages> localizer)
    {
        _currentUser = currentUser;
        _unitOfWork = unitOfWork;
        _localizer = localizer;
    }

    /// <summary>
    /// Resolves the acting parent's <c>ParentUser.Id</c> from the JWT
    /// (<c>User.Id</c> → active <c>ParentUser</c>). Returns null when the caller is not an
    /// active parent — any route <c>{parentUserId}</c> segment is deliberately never consulted.
    /// </summary>
    protected async Task<long?> ResolveParentUserIdAsync()
    {
        long? userId = _currentUser.UserId;
        if (userId is null) return null;

        var parentUser = await _unitOfWork.Users.GetActiveParentUserByUserIdAsync(userId.Value);
        return parentUser?.Id;
    }

    /// <summary>
    /// Resolves the named child's TeacherStudent.Id under the named teacher, for the calling
    /// parent. Verifies parent ownership of the child, then branches on link method
    /// (AAM-FR-06.3). Returns either the resolved id or a 401/403/404 result.
    /// </summary>
    protected async Task<ChildResolution> ResolveChildForParentAsync(long childId, long teacherId)
    {
        long? userId = _currentUser.UserId;
        if (userId is null)
            return ChildResolution.Error(Unauthorized());

        var parentUser = await _unitOfWork.Users.GetActiveParentUserByUserIdAsync(userId.Value);
        if (parentUser is null)
            return ChildResolution.Error(NotFoundError("ParentUserNotFound"));

        var child = await _unitOfWork.Users.GetActiveChildAsync(parentUser.Id, childId);
        if (child is null)
            return ChildResolution.Error(NotFoundError("ChildNotFound"));

        // Method A — child has a StudentUser account: reuse the student-teacher link.
        if (child.LinkMethod == ChildLinkMethod.StudentAccount)
        {
            if (child.StudentUserId is null)
                return ChildResolution.Error(ForbiddenError("ChildEnrollmentRemoved"));

            var link = await _unitOfWork.Users
                .GetActiveStudentTeacherLinkAsync(child.StudentUserId.Value, teacherId);
            if (link is null || link.LinkStatus != LinkStatus.Active)
                return ChildResolution.Error(ForbiddenError("TeacherLinkNotFound"));
            if (link.TeacherStudentId is null)
                return ChildResolution.Error(ForbiddenError("StudentEnrollmentRemoved"));

            return ChildResolution.Ok(link.TeacherStudentId.Value);
        }

        // Method B — manual profile: teacher link lives on ParentChildTeacherLink.
        var parentLink = await _unitOfWork.Users
            .GetActiveParentChildTeacherLinkAsync(child.Id, teacherId);
        if (parentLink is null || parentLink.LinkStatus != LinkStatus.Active)
            return ChildResolution.Error(ForbiddenError("TeacherLinkNotFound"));
        if (parentLink.TeacherStudentId is null)
            return ChildResolution.Error(ForbiddenError("StudentEnrollmentRemoved"));

        return ChildResolution.Ok(parentLink.TeacherStudentId.Value);
    }

    /// <summary>
    /// Returns the calling user's id straight from the JWT, or null if unresolvable.
    /// Used by endpoints that must read the id BEFORE a ParentUser record exists —
    /// i.e. before <see cref="ResolveParentUserIdAsync"/> can succeed (parent
    /// self-initialization).
    /// </summary>
    protected long? GetCurrentUserId() => _currentUser.UserId;

    protected IActionResult ParentNotResolved() =>
        new ObjectResult(new { success = false, message = "Parent could not be resolved from token." })
        { StatusCode = StatusCodes.Status404NotFound };

    protected IActionResult NotFoundError(string message) =>
        new ObjectResult(new { success = false, code = message, message = _localizer[message].Value })
        {
            StatusCode = (int)HttpStatusCode.NotFound,
        };

    protected IActionResult ForbiddenError(string message) =>
        new ObjectResult(new { success = false, code = message, message = _localizer[message].Value })
        {
            StatusCode = (int)HttpStatusCode.Forbidden,
        };

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
}
'@

Set-ContentWithRetry -Path $newFilePath -Value ($newFileContent -replace "`n", "`r`n")
Write-Host "[OK] Created $newFilePath"

}

# ═══════════════════════════════════════════════════════════════════════════
# 2. ParentUserController.cs
# ═══════════════════════════════════════════════════════════════════════════

$parentUserPath = Join-Path $controllersRoot "ParentUserController.cs"

# 2a. usings — add Edvanz.Domain.Resources + Microsoft.Extensions.Localization
Replace-InFile -Path $parentUserPath -Label "ParentUserController usings" -Find @'
using Edvanz.API.Attributes;
using Edvanz.Application.Dtos.ParentUser;
using Edvanz.Application.IservicesContract;
using Edvanz.Application.ServiceContract;
using Edvanz.Domain.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Edvanz.API.Controllers;
'@ -Replace @'
using Edvanz.API.Attributes;
using Edvanz.Application.Dtos.ParentUser;
using Edvanz.Application.IservicesContract;
using Edvanz.Application.ServiceContract;
using Edvanz.Domain.Interfaces;
using Edvanz.Domain.Resources;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;

namespace Edvanz.API.Controllers;
'@

# 2b. class declaration, fields, constructor, and the two now-inherited helpers
#
# NOTE: the Find anchor below matches mojibake ("â†’" / "â€”") that is actually
# present in this file's doc comments on disk -- a prior non-BOM encoding pass
# corrupted the original "→" / "—" characters. The Replace text uses correct
# Unicode, so this edit heals those two lines as a side effect. The remaining
# corruption further down this file (endpoint-header box-drawing comments) is
# OUT OF SCOPE for Phase 1 and is left untouched -- flagged separately.
Replace-InFile -Path $parentUserPath -Label "ParentUserController class/ctor" -Find @'
[Authorize]
public class ParentUserController : ApiBaseController
{
    private readonly IParentUserService _parentUserService;
    private readonly ICurrentUserService _currentUser;
    private readonly IUnitOfWork _unitOfWork;

    public ParentUserController(
        IParentUserService parentUserService,
        ICurrentUserService currentUser,
        IUnitOfWork unitOfWork)
    {
        _parentUserService = parentUserService;
        _currentUser = currentUser;
        _unitOfWork = unitOfWork;
    }

    /// <summary>
    /// Resolves the acting parent's <c>ParentUser.Id</c> from the JWT
    /// (<c>User.Id</c> â†’ active <c>ParentUser</c>). Returns null when the caller is not an
    /// active parent â€” the route <c>{parentUserId}</c> is deliberately never consulted.
    /// </summary>
    private async Task<long?> ResolveParentUserIdAsync()
    {
        long? userId = _currentUser.UserId;
        if (userId is null) return null;

        var parentUser = await _unitOfWork.Users.GetActiveParentUserByUserIdAsync(userId.Value);
        return parentUser?.Id;
    }

    private IActionResult ParentNotResolved() =>
        new ObjectResult(new { success = false, message = "Parent could not be resolved from token." })
        { StatusCode = StatusCodes.Status404NotFound };

'@ -Replace @'
[Authorize]
public class ParentUserController : ParentScopedApiBaseController
{
    private readonly IParentUserService _parentUserService;

    public ParentUserController(
        IParentUserService parentUserService,
        ICurrentUserService currentUser,
        IUnitOfWork unitOfWork,
        IStringLocalizer<Messages> localizer)
        : base(currentUser, unitOfWork, localizer)
    {
        _parentUserService = parentUserService;
    }

'@

# 2c. InitializeParentUser — switch the pre-ParentUser-record identity read
#     from the removed private field to the inherited accessor.
Replace-InFile -Path $parentUserPath -Label "ParentUserController.InitializeParentUser" -Find @'
        long? userId = _currentUser.UserId;
        if (userId is null) return UserNotResolved();
        dto.UserId = userId.Value;
'@ -Replace @'
        long? userId = GetCurrentUserId();
        if (userId is null) return UserNotResolved();
        dto.UserId = userId.Value;
'@

# ═══════════════════════════════════════════════════════════════════════════
# 3. ParentAttendanceController.cs
# ═══════════════════════════════════════════════════════════════════════════

$parentAttendancePath = Join-Path $controllersRoot "ParentAttendanceController.cs"

# 3a. class declaration, fields, constructor
Replace-InFile -Path $parentAttendancePath -Label "ParentAttendanceController class/ctor" -Find @'
[Route("api/attendance/parent")]
[Authorize]
public sealed class ParentAttendanceController : ApiBaseController
{
    private readonly IAttendanceService _attendanceService;
    private readonly ICurrentUserService _currentUser;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IStringLocalizer<Messages> _localizer;

    public ParentAttendanceController(
        IAttendanceService attendanceService,
        ICurrentUserService currentUser,
        IUnitOfWork unitOfWork, IStringLocalizer<Messages> localizer)
    {
        _attendanceService = attendanceService;
        _currentUser = currentUser;
        _unitOfWork = unitOfWork;
        _localizer = localizer;
    }
'@ -Replace @'
[Route("api/attendance/parent")]
[Authorize]
public sealed class ParentAttendanceController : ParentScopedApiBaseController
{
    private readonly IAttendanceService _attendanceService;

    public ParentAttendanceController(
        IAttendanceService attendanceService,
        ICurrentUserService currentUser,
        IUnitOfWork unitOfWork, IStringLocalizer<Messages> localizer)
        : base(currentUser, unitOfWork, localizer)
    {
        _attendanceService = attendanceService;
    }
'@

# 3b. remove the now-duplicated private helpers block (ResolveChildForParentAsync,
#     NotFoundError, ForbiddenError, ChildResolution) — all inherited from the new base.
Replace-InFile -Path $parentAttendancePath -Label "ParentAttendanceController private helpers removal" -Find @'
    // ──────────────────────────────────────────────────────────────────────
    // PRIVATE HELPERS
    // Same shape as StudentAttendanceController / StudentVideosController, plus the
    // Method-A/B branch. With three copies of this resolution pattern now in play,
    // extracting a shared CallerScopedApiBaseController (error helpers + resolution
    // struct + the StudentTeacherLink sub-step) is worth a dedicated refactor pass.
    // ──────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Resolves the named child's TeacherStudent.Id under the named teacher, for the
    /// calling parent. Verifies parent ownership of the child, then branches on link
    /// method (AAM-FR-06.3). Returns either the resolved id or a 401/403/404 result.
    /// </summary>
    private async Task<ChildResolution> ResolveChildForParentAsync(long childId, long teacherId)
    {
        long? userId = _currentUser.UserId;
        if (userId is null)
            return ChildResolution.Error(Unauthorized());

        var parentUser = await _unitOfWork.Users.GetActiveParentUserByUserIdAsync(userId.Value);
        if (parentUser is null)
            return ChildResolution.Error(NotFoundError("ParentUserNotFound"));

        var child = await _unitOfWork.Users.GetActiveChildAsync(parentUser.Id, childId);
        if (child is null)
            return ChildResolution.Error(NotFoundError("ChildNotFound"));

        // Method A — child has a StudentUser account: reuse the student-teacher link.
        if (child.LinkMethod == ChildLinkMethod.StudentAccount)
        {
            if (child.StudentUserId is null)
                return ChildResolution.Error(ForbiddenError("ChildEnrollmentRemoved"));

            var link = await _unitOfWork.Users
                .GetActiveStudentTeacherLinkAsync(child.StudentUserId.Value, teacherId);
            if (link is null || link.LinkStatus != LinkStatus.Active)
                return ChildResolution.Error(ForbiddenError("TeacherLinkNotFound"));
            if (link.TeacherStudentId is null)
                return ChildResolution.Error(ForbiddenError("StudentEnrollmentRemoved"));

            return ChildResolution.Ok(link.TeacherStudentId.Value);
        }

        // Method B — manual profile: teacher link lives on ParentChildTeacherLink.
        var parentLink = await _unitOfWork.Users
            .GetActiveParentChildTeacherLinkAsync(child.Id, teacherId);
        if (parentLink is null || parentLink.LinkStatus != LinkStatus.Active)
            return ChildResolution.Error(ForbiddenError("TeacherLinkNotFound"));
        if (parentLink.TeacherStudentId is null)
            return ChildResolution.Error(ForbiddenError("StudentEnrollmentRemoved"));

        return ChildResolution.Ok(parentLink.TeacherStudentId.Value);
    }

    private IActionResult NotFoundError(string message) =>
        new ObjectResult(new { success = false, code = message, message = _localizer[message].Value })
        {
            StatusCode = (int)HttpStatusCode.NotFound,
        };

    private IActionResult ForbiddenError(string message) =>
        new ObjectResult(new { success = false, code = message, message = _localizer[message].Value })
        {
            StatusCode = (int)HttpStatusCode.Forbidden,
        };

    private readonly struct ChildResolution
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
}
'@ -Replace @'
}
'@

# ═══════════════════════════════════════════════════════════════════════════
# 4. ParentPaymentController.cs
# ═══════════════════════════════════════════════════════════════════════════

$parentPaymentPath = Join-Path $controllersRoot "ParentPaymentController.cs"

# 4a. class declaration, fields, constructor
Replace-InFile -Path $parentPaymentPath -Label "ParentPaymentController class/ctor" -Find @'
[Route("api/payment/parent")]
[Authorize]
public sealed class ParentPaymentController : ApiBaseController
{
    private readonly IPaymentService _paymentService;
    private readonly ICurrentUserService _currentUser;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IStringLocalizer<Messages> _localizer;

    public ParentPaymentController(
        IPaymentService paymentService,
        ICurrentUserService currentUser,
        IUnitOfWork unitOfWork, IStringLocalizer<Messages> localizer)
    {
        _paymentService = paymentService;
        _currentUser = currentUser;
        _unitOfWork = unitOfWork;
        _localizer = localizer;
    }
'@ -Replace @'
[Route("api/payment/parent")]
[Authorize]
public sealed class ParentPaymentController : ParentScopedApiBaseController
{
    private readonly IPaymentService _paymentService;

    public ParentPaymentController(
        IPaymentService paymentService,
        ICurrentUserService currentUser,
        IUnitOfWork unitOfWork, IStringLocalizer<Messages> localizer)
        : base(currentUser, unitOfWork, localizer)
    {
        _paymentService = paymentService;
    }
'@

# 4b. remove the now-duplicated private helpers block
Replace-InFile -Path $parentPaymentPath -Label "ParentPaymentController private helpers removal" -Find @'
    // ──────────────────────────────────────────────────────────────────────
    // PRIVATE HELPERS
    // Same shape as ParentAttendanceController, plus the Method-A/B branch. A shared
    // CallerScopedApiBaseController is the pending refactor to remove the duplication.
    // ──────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Resolves the named child's TeacherStudent.Id under the named teacher, for the calling
    /// parent. Verifies parent ownership of the child, then branches on link method
    /// (AAM-FR-06.3). Returns either the resolved id or a 401/403/404 result.
    /// </summary>
    private async Task<ChildResolution> ResolveChildForParentAsync(long childId, long teacherId)
    {
        long? userId = _currentUser.UserId;
        if (userId is null)
            return ChildResolution.Error(Unauthorized());

        var parentUser = await _unitOfWork.Users.GetActiveParentUserByUserIdAsync(userId.Value);
        if (parentUser is null)
            return ChildResolution.Error(NotFoundError("ParentUserNotFound"));

        var child = await _unitOfWork.Users.GetActiveChildAsync(parentUser.Id, childId);
        if (child is null)
            return ChildResolution.Error(NotFoundError("ChildNotFound"));

        // Method A — child has a StudentUser account: reuse the student-teacher link.
        if (child.LinkMethod == ChildLinkMethod.StudentAccount)
        {
            if (child.StudentUserId is null)
                return ChildResolution.Error(ForbiddenError("ChildEnrollmentRemoved"));

            var link = await _unitOfWork.Users
                .GetActiveStudentTeacherLinkAsync(child.StudentUserId.Value, teacherId);
            if (link is null || link.LinkStatus != LinkStatus.Active)
                return ChildResolution.Error(ForbiddenError("TeacherLinkNotFound"));
            if (link.TeacherStudentId is null)
                return ChildResolution.Error(ForbiddenError("StudentEnrollmentRemoved"));

            return ChildResolution.Ok(link.TeacherStudentId.Value);
        }

        // Method B — manual profile: teacher link lives on ParentChildTeacherLink.
        var parentLink = await _unitOfWork.Users
            .GetActiveParentChildTeacherLinkAsync(child.Id, teacherId);
        if (parentLink is null || parentLink.LinkStatus != LinkStatus.Active)
            return ChildResolution.Error(ForbiddenError("TeacherLinkNotFound"));
        if (parentLink.TeacherStudentId is null)
            return ChildResolution.Error(ForbiddenError("StudentEnrollmentRemoved"));

        return ChildResolution.Ok(parentLink.TeacherStudentId.Value);
    }

    private IActionResult NotFoundError(string message) =>
        new ObjectResult(new { success = false, code = message, message = _localizer[message].Value })
        {
            StatusCode = (int)HttpStatusCode.NotFound,
        };

    private IActionResult ForbiddenError(string message) =>
        new ObjectResult(new { success = false, code = message, message = _localizer[message].Value })
        {
            StatusCode = (int)HttpStatusCode.Forbidden,
        };

    private readonly struct ChildResolution
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
}
'@ -Replace @'
}
'@

Write-Host ""
Write-Host "Phase 1 applied cleanly. Next steps:"
Write-Host "  1. dotnet build"
Write-Host "  2. Postman regression: parent attendance summary/month + payment tracking," `
            "both a Method A and a Method B child."
Write-Host "  3. Confirm every response (success + error codes/status) is byte-identical to before."
