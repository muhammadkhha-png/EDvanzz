<#
    Phase 3 — Consolidate AttendanceViewerType + PaymentViewerType -> ContentViewerType
    ─────────────────────────────────────────────────────────────────────────
    D3 (locked decision): one shared ContentViewerType {Student=1, Parent=2}.
    AttendanceViewerType and PaymentViewerType were structurally identical
    (same two members, same byte backing, same purpose) and duplicated
    per-module. This consolidates them into one enum in the same namespace
    (Edvanz.Domain.Enums), so no using-directive changes are needed anywhere
    -- only the type name and enum-member references change.

    Files touched:
      NEW    Edvanz.Domain/Enums/ContentViewerType.cs
      DELETE Edvanz.Domain/Enums/AttendanceViewerType.cs
      DELETE Edvanz.Domain/Enums/PaymentViewerType.cs
      EDIT   Edvanz.Application/ServiceContract/IAttendanceService.cs
      EDIT   Edvanz.Application/Services/AttendanceService.cs
      EDIT   Edvanz.Application/ServiceContract/IPaymentService.cs
      EDIT   Edvanz.Application/Services/PaymentService.cs
      EDIT   Edvanz.API/Controllers/StudentAttendanceController.cs
      EDIT   Edvanz.API/Controllers/ParentAttendanceController.cs
      EDIT   Edvanz.API/Controllers/StudentPaymentController.cs
      EDIT   Edvanz.API/Controllers/ParentPaymentController.cs
      EDIT   Edvanz.Application/Services/StudentTeacherHomeService.cs

    NOT touched (D2 — parent-side-only scope from Phase 1 carries forward;
    this is a type-rename, not a controller refactor, so it's not really at
    issue here, but noting for completeness): no student-side controller
    duplication is being extracted, only the enum reference is updated in place.

    Zero behaviour change: ContentViewerType.Student == 1, ContentViewerType.Parent == 2,
    identical to both enums being replaced, so no data/serialization impact.

    USAGE
    -----
        powershell -ExecutionPolicy Bypass -File .\phase3-content-viewer-type.ps1

    Safe to re-run from the top (idempotent — skips any block already applied).
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

    $normContent = $content -replace "`r`n", "`n"
    $normFind    = $Find    -replace "`r`n", "`n"
    $normReplace = $Replace -replace "`r`n", "`n"

    if ($normContent.Contains($normReplace) -and -not $normContent.Contains($normFind)) {
        Write-Host "[SKIP] $Label already applied -> $Path"
        return
    }

    $occurrences = ([regex]::Matches($normContent, [regex]::Escape($normFind))).Count

    if ($occurrences -eq 0) {
        throw "[$Label] Anchor NOT FOUND in $Path. The file has likely drifted from what this script expects (or has the same pre-existing encoding corruption seen in ParentUserController.cs). Aborting without modifying it — paste the current file content back to Claude to regenerate this block."
    }
    if ($occurrences -gt 1) {
        throw "[$Label] Anchor matched $occurrences times in $Path (expected exactly 1). Refusing to guess which one. Aborting without modifying it."
    }

    $updated = $normContent.Replace($normFind, $normReplace)
    $updated = $updated -replace "`n", "`r`n"

    Set-ContentWithRetry -Path $Path -Value $updated
    Write-Host "[OK] $Label -> $Path"
}

function Remove-FileIfExists {
    param(
        [Parameter(Mandatory)] [string]$Path,
        [Parameter(Mandatory)] [string]$Label
    )

    if (-not (Test-Path $Path)) {
        Write-Host "[SKIP] $Label already removed -> $Path"
        return
    }

    Remove-Item -Path $Path -Force
    Write-Host "[OK] Removed $Label -> $Path"
}

# ═══════════════════════════════════════════════════════════════════════════
# 1. NEW FILE — ContentViewerType.cs
# ═══════════════════════════════════════════════════════════════════════════

$newEnumPath = "Edvanz.Domain/Enums/ContentViewerType.cs"

if (Test-Path $newEnumPath) {
    Write-Host "[SKIP] Already exists -> $newEnumPath"
} else {

$newEnumContent = @'
namespace Edvanz.Domain.Enums;

/// <summary>
/// Identifies who is viewing a student's data through the student/parent read
/// surfaces (attendance, payment, and — as later phases wire them up — video,
/// online exam, offline exam, homework), so the owning service enforces the
/// correct <c>TeacherConfiguration</c> visibility flag pair
/// (<c>StudentVisibility*</c> vs <c>ParentVisibility*</c>).
///
/// Phase 3 (parent parity) consolidation: replaces the formerly-separate
/// <c>AttendanceViewerType</c> and <c>PaymentViewerType</c>, which were
/// structurally identical (same two members, same byte backing, same purpose)
/// and duplicated per-module rather than shared. One enum, one place to look,
/// one place to extend when a new content module gains a parent surface.
/// Byte-backed with locked values per project convention.
/// </summary>
public enum ContentViewerType : byte
{
    /// <summary>Student viewing their own data. Gated by the module's StudentVisibility* flag.</summary>
    Student = 1,

    /// <summary>Parent viewing a linked child's data. Gated by the module's ParentVisibility* flag.</summary>
    Parent = 2
}
'@

Set-ContentWithRetry -Path $newEnumPath -Value ($newEnumContent -replace "`n", "`r`n")
Write-Host "[OK] Created $newEnumPath"

}

# ═══════════════════════════════════════════════════════════════════════════
# 2. Edvanz.Application/ServiceContract/IAttendanceService.cs
# ═══════════════════════════════════════════════════════════════════════════

$iAttendancePath = "Edvanz.Application/ServiceContract/IAttendanceService.cs"

Replace-InFile -Path $iAttendancePath -Label "IAttendanceService viewer signatures" -Find @'
    /// <summary>
    /// Gets attendance data for a student viewing their own attendance.
    /// Gated by TeacherConfiguration visibility settings.
    /// </summary>
    Task<Result<MonthlyAttendanceSummaryDto>> GetStudentViewAttendanceAsync(
        long teacherId, long teacherStudentId, StudentTimelineMonthRequest request, AttendanceViewerType viewer);

    /// <summary>
    /// Gets attendance summary for a student from the student/parent perspective.
    /// </summary>
    Task<Result<StudentAttendanceSummaryDto>> GetStudentViewAttendanceSummaryAsync(
        long teacherId, long teacherStudentId, AttendanceViewerType viewer);
'@ -Replace @'
    /// <summary>
    /// Gets attendance data for a student viewing their own attendance.
    /// Gated by TeacherConfiguration visibility settings.
    /// </summary>
    Task<Result<MonthlyAttendanceSummaryDto>> GetStudentViewAttendanceAsync(
        long teacherId, long teacherStudentId, StudentTimelineMonthRequest request, ContentViewerType viewer);

    /// <summary>
    /// Gets attendance summary for a student from the student/parent perspective.
    /// </summary>
    Task<Result<StudentAttendanceSummaryDto>> GetStudentViewAttendanceSummaryAsync(
        long teacherId, long teacherStudentId, ContentViewerType viewer);
'@

# ═══════════════════════════════════════════════════════════════════════════
# 3. Edvanz.Application/Services/AttendanceService.cs — two spots
# ═══════════════════════════════════════════════════════════════════════════

$attendanceServicePath = "Edvanz.Application/Services/AttendanceService.cs"

# 3a. The two public methods
Replace-InFile -Path $attendanceServicePath -Label "AttendanceService public viewer methods" -Find @'
    /// <inheritdoc />
    public async Task<Result<MonthlyAttendanceSummaryDto>> GetStudentViewAttendanceAsync(
        long teacherId, long teacherStudentId, StudentTimelineMonthRequest request, AttendanceViewerType viewer)
    {
        var config = await _unitOfWork.Users.GetConfigurationByTeacherIdAsync(teacherId);
        if (!IsAttendanceVisibleTo(config, viewer))
            return Result<MonthlyAttendanceSummaryDto>.Failure(
                _localizer, AttendanceConstants.Messages.AttendanceVisibilityDisabled, HttpStatusCode.Forbidden);

        return await GetStudentTimelineMonthAsync(teacherId, teacherStudentId, request);
    }
    /// <inheritdoc />
    public async Task<Result<StudentAttendanceSummaryDto>> GetStudentViewAttendanceSummaryAsync(
        long teacherId, long teacherStudentId, AttendanceViewerType viewer)
    {
        var config = await _unitOfWork.Users.GetConfigurationByTeacherIdAsync(teacherId);
        if (!IsAttendanceVisibleTo(config, viewer))
            return Result<StudentAttendanceSummaryDto>.Failure(
                _localizer, AttendanceConstants.Messages.AttendanceVisibilityDisabled, HttpStatusCode.Forbidden);

        return await GetStudentAttendanceSummaryAsync(teacherId, teacherStudentId);
    }
'@ -Replace @'
    /// <inheritdoc />
    public async Task<Result<MonthlyAttendanceSummaryDto>> GetStudentViewAttendanceAsync(
        long teacherId, long teacherStudentId, StudentTimelineMonthRequest request, ContentViewerType viewer)
    {
        var config = await _unitOfWork.Users.GetConfigurationByTeacherIdAsync(teacherId);
        if (!IsAttendanceVisibleTo(config, viewer))
            return Result<MonthlyAttendanceSummaryDto>.Failure(
                _localizer, AttendanceConstants.Messages.AttendanceVisibilityDisabled, HttpStatusCode.Forbidden);

        return await GetStudentTimelineMonthAsync(teacherId, teacherStudentId, request);
    }
    /// <inheritdoc />
    public async Task<Result<StudentAttendanceSummaryDto>> GetStudentViewAttendanceSummaryAsync(
        long teacherId, long teacherStudentId, ContentViewerType viewer)
    {
        var config = await _unitOfWork.Users.GetConfigurationByTeacherIdAsync(teacherId);
        if (!IsAttendanceVisibleTo(config, viewer))
            return Result<StudentAttendanceSummaryDto>.Failure(
                _localizer, AttendanceConstants.Messages.AttendanceVisibilityDisabled, HttpStatusCode.Forbidden);

        return await GetStudentAttendanceSummaryAsync(teacherId, teacherStudentId);
    }
'@

# 3b. The private gate helper
Replace-InFile -Path $attendanceServicePath -Label "AttendanceService.IsAttendanceVisibleTo" -Find @'
    /// <summary>
    /// Per-viewer visibility gate (AAM-FR-04.8 vs AAM-FR-04.9 — independent toggles).
    /// Each caller is checked against only its own flag. Fail-closed when no
    /// configuration row exists (preserves the previous deny-on-null behavior).
    /// </summary>
    private static bool IsAttendanceVisibleTo(TeacherConfiguration? config, AttendanceViewerType viewer)
    {
        if (config is null) return false;
        return viewer switch
        {
            AttendanceViewerType.Student => config.StudentVisibilityAttendance,
            AttendanceViewerType.Parent => config.ParentVisibilityAttendance,
            _ => false
        };
    }
}
'@ -Replace @'
    /// <summary>
    /// Per-viewer visibility gate (AAM-FR-04.8 vs AAM-FR-04.9 — independent toggles).
    /// Each caller is checked against only its own flag. Fail-closed when no
    /// configuration row exists (preserves the previous deny-on-null behavior).
    /// </summary>
    private static bool IsAttendanceVisibleTo(TeacherConfiguration? config, ContentViewerType viewer)
    {
        if (config is null) return false;
        return viewer switch
        {
            ContentViewerType.Student => config.StudentVisibilityAttendance,
            ContentViewerType.Parent => config.ParentVisibilityAttendance,
            _ => false
        };
    }
}
'@

# ═══════════════════════════════════════════════════════════════════════════
# 4. Edvanz.Application/ServiceContract/IPaymentService.cs
# ═══════════════════════════════════════════════════════════════════════════

$iPaymentPath = "Edvanz.Application/ServiceContract/IPaymentService.cs"

Replace-InFile -Path $iPaymentPath -Label "IPaymentService viewer signature" -Find @'
    /// <summary>
    /// Builds the full student/parent "Payment" tracking screen in one call: header, the
    /// single Upcoming month, the Paid section, and the Overdue section — pivoted on the
    /// teacher's local current month. Gated by the caller-specific visibility flag
    /// (<see cref="Domain.Enums.PaymentViewerType"/> → StudentVisibilityPayment /
    /// ParentVisibilityPayment); returns 403 when disabled (fail-closed on missing config).
    /// </summary>
    Task<Result<StudentPaymentTrackingDto>> GetStudentPaymentTrackingAsync(
        long teacherId, long teacherStudentId, PaymentViewerType viewer);
'@ -Replace @'
    /// <summary>
    /// Builds the full student/parent "Payment" tracking screen in one call: header, the
    /// single Upcoming month, the Paid section, and the Overdue section — pivoted on the
    /// teacher's local current month. Gated by the caller-specific visibility flag
    /// (<see cref="Domain.Enums.ContentViewerType"/> → StudentVisibilityPayment /
    /// ParentVisibilityPayment); returns 403 when disabled (fail-closed on missing config).
    /// </summary>
    Task<Result<StudentPaymentTrackingDto>> GetStudentPaymentTrackingAsync(
        long teacherId, long teacherStudentId, ContentViewerType viewer);
'@

# ═══════════════════════════════════════════════════════════════════════════
# 5. Edvanz.Application/Services/PaymentService.cs — two spots
# ═══════════════════════════════════════════════════════════════════════════

$paymentServicePath = "Edvanz.Application/Services/PaymentService.cs"

# 5a. The public method
Replace-InFile -Path $paymentServicePath -Label "PaymentService.GetStudentPaymentTrackingAsync signature" -Find @'
    /// <inheritdoc />
    public async Task<Result<StudentPaymentTrackingDto>> GetStudentPaymentTrackingAsync(
        long teacherId, long teacherStudentId, PaymentViewerType viewer)
    {
        // Visibility gate — caller-specific, fail-closed when the config row is missing.
        var config = await _unitOfWork.Users.GetConfigurationByTeacherIdAsync(teacherId);
        if (!IsPaymentVisibleTo(config, viewer))
'@ -Replace @'
    /// <inheritdoc />
    public async Task<Result<StudentPaymentTrackingDto>> GetStudentPaymentTrackingAsync(
        long teacherId, long teacherStudentId, ContentViewerType viewer)
    {
        // Visibility gate — caller-specific, fail-closed when the config row is missing.
        var config = await _unitOfWork.Users.GetConfigurationByTeacherIdAsync(teacherId);
        if (!IsPaymentVisibleTo(config, viewer))
'@

# 5b. The private gate helper
Replace-InFile -Path $paymentServicePath -Label "PaymentService.IsPaymentVisibleTo" -Find @'
    /// <summary>
    /// Caller-specific payment visibility, fail-closed on missing config.
    /// Mirrors <c>AttendanceService.IsAttendanceVisibleTo</c>.
    /// </summary>
    private static bool IsPaymentVisibleTo(TeacherConfiguration? config, PaymentViewerType viewer)
    {
        if (config is null) return false;
        return viewer switch
        {
            PaymentViewerType.Student => config.StudentVisibilityPayment,
            PaymentViewerType.Parent => config.ParentVisibilityPayment,
            _ => false
        };
    }
'@ -Replace @'
    /// <summary>
    /// Caller-specific payment visibility, fail-closed on missing config.
    /// Mirrors <c>AttendanceService.IsAttendanceVisibleTo</c>.
    /// </summary>
    private static bool IsPaymentVisibleTo(TeacherConfiguration? config, ContentViewerType viewer)
    {
        if (config is null) return false;
        return viewer switch
        {
            ContentViewerType.Student => config.StudentVisibilityPayment,
            ContentViewerType.Parent => config.ParentVisibilityPayment,
            _ => false
        };
    }
'@

# ═══════════════════════════════════════════════════════════════════════════
# 6. Edvanz.API/Controllers/StudentAttendanceController.cs — three spots
# ═══════════════════════════════════════════════════════════════════════════

$studentAttendanceControllerPath = "Edvanz.API/Controllers/StudentAttendanceController.cs"

# 6a. Class doc comment
Replace-InFile -Path $studentAttendanceControllerPath -Label "StudentAttendanceController doc comment" -Find @'
/// their own attendance, only under a teacher they're actually linked to
/// (REQ-ATT-NFR-003). Teacher-controlled visibility (AAM-FR-04.8) is enforced in
/// the service via AttendanceViewerType.Student.
/// </summary>
'@ -Replace @'
/// their own attendance, only under a teacher they're actually linked to
/// (REQ-ATT-NFR-003). Teacher-controlled visibility (AAM-FR-04.8) is enforced in
/// the service via ContentViewerType.Student.
/// </summary>
'@

# 6b. GetMyAttendanceSummary call site
Replace-InFile -Path $studentAttendanceControllerPath -Label "StudentAttendanceController.GetMyAttendanceSummary call" -Find @'
        var result = await _attendanceService.GetStudentViewAttendanceSummaryAsync(
            teacherId, resolution.TeacherStudentId!.Value, AttendanceViewerType.Student);
'@ -Replace @'
        var result = await _attendanceService.GetStudentViewAttendanceSummaryAsync(
            teacherId, resolution.TeacherStudentId!.Value, ContentViewerType.Student);
'@

# 6c. GetMyAttendanceMonth call site
Replace-InFile -Path $studentAttendanceControllerPath -Label "StudentAttendanceController.GetMyAttendanceMonth call" -Find @'
        var result = await _attendanceService.GetStudentViewAttendanceAsync(
            teacherId, resolution.TeacherStudentId!.Value, request, AttendanceViewerType.Student);
'@ -Replace @'
        var result = await _attendanceService.GetStudentViewAttendanceAsync(
            teacherId, resolution.TeacherStudentId!.Value, request, ContentViewerType.Student);
'@

# ═══════════════════════════════════════════════════════════════════════════
# 7. Edvanz.API/Controllers/ParentAttendanceController.cs — three spots
# ═══════════════════════════════════════════════════════════════════════════

$parentAttendanceControllerPath = "Edvanz.API/Controllers/ParentAttendanceController.cs"

# 7a. Class doc comment
Replace-InFile -Path $parentAttendanceControllerPath -Label "ParentAttendanceController doc comment" -Find @'
/// A parent reads attendance only for their own children, only under teachers actually
/// linked to that child (REQ-ATT-NFR-003). Teacher-controlled parent visibility
/// (AAM-FR-04.9) is enforced in the service via AttendanceViewerType.Parent.
/// </summary>
'@ -Replace @'
/// A parent reads attendance only for their own children, only under teachers actually
/// linked to that child (REQ-ATT-NFR-003). Teacher-controlled parent visibility
/// (AAM-FR-04.9) is enforced in the service via ContentViewerType.Parent.
/// </summary>
'@

# 7b. GetChildAttendanceSummary call site
Replace-InFile -Path $parentAttendanceControllerPath -Label "ParentAttendanceController.GetChildAttendanceSummary call" -Find @'
        var result = await _attendanceService.GetStudentViewAttendanceSummaryAsync(
            teacherId, resolution.TeacherStudentId!.Value, AttendanceViewerType.Parent);
'@ -Replace @'
        var result = await _attendanceService.GetStudentViewAttendanceSummaryAsync(
            teacherId, resolution.TeacherStudentId!.Value, ContentViewerType.Parent);
'@

# 7c. GetChildAttendanceMonth call site
Replace-InFile -Path $parentAttendanceControllerPath -Label "ParentAttendanceController.GetChildAttendanceMonth call" -Find @'
        var result = await _attendanceService.GetStudentViewAttendanceAsync(
            teacherId, resolution.TeacherStudentId!.Value, request, AttendanceViewerType.Parent);
'@ -Replace @'
        var result = await _attendanceService.GetStudentViewAttendanceAsync(
            teacherId, resolution.TeacherStudentId!.Value, request, ContentViewerType.Parent);
'@

# ═══════════════════════════════════════════════════════════════════════════
# 8. Edvanz.API/Controllers/StudentPaymentController.cs — two spots
# ═══════════════════════════════════════════════════════════════════════════

$studentPaymentControllerPath = "Edvanz.API/Controllers/StudentPaymentController.cs"

# 8a. Class doc comment
Replace-InFile -Path $studentPaymentControllerPath -Label "StudentPaymentController doc comment" -Find @'
/// under a teacher they're actually linked to. Teacher-controlled visibility
/// (StudentVisibilityPayment) is enforced in the service via PaymentViewerType.Student.
/// </summary>
'@ -Replace @'
/// under a teacher they're actually linked to. Teacher-controlled visibility
/// (StudentVisibilityPayment) is enforced in the service via ContentViewerType.Student.
/// </summary>
'@

# 8b. GetMyPaymentTracking call site
Replace-InFile -Path $studentPaymentControllerPath -Label "StudentPaymentController.GetMyPaymentTracking call" -Find @'
        var result = await _paymentService.GetStudentPaymentTrackingAsync(
            teacherId, resolution.TeacherStudentId!.Value, PaymentViewerType.Student);
'@ -Replace @'
        var result = await _paymentService.GetStudentPaymentTrackingAsync(
            teacherId, resolution.TeacherStudentId!.Value, ContentViewerType.Student);
'@

# ═══════════════════════════════════════════════════════════════════════════
# 9. Edvanz.API/Controllers/ParentPaymentController.cs — two spots
# ═══════════════════════════════════════════════════════════════════════════

$parentPaymentControllerPath = "Edvanz.API/Controllers/ParentPaymentController.cs"

# 9a. Class doc comment
Replace-InFile -Path $parentPaymentControllerPath -Label "ParentPaymentController doc comment" -Find @'
/// Teacher-controlled parent visibility (ParentVisibilityPayment) is enforced in the
/// service via PaymentViewerType.Parent.
/// </summary>
'@ -Replace @'
/// Teacher-controlled parent visibility (ParentVisibilityPayment) is enforced in the
/// service via ContentViewerType.Parent.
/// </summary>
'@

# 9b. GetChildPaymentTracking call site
Replace-InFile -Path $parentPaymentControllerPath -Label "ParentPaymentController.GetChildPaymentTracking call" -Find @'
        var result = await _paymentService.GetStudentPaymentTrackingAsync(
            teacherId, resolution.TeacherStudentId!.Value, PaymentViewerType.Parent);
'@ -Replace @'
        var result = await _paymentService.GetStudentPaymentTrackingAsync(
            teacherId, resolution.TeacherStudentId!.Value, ContentViewerType.Parent);
'@

# ═══════════════════════════════════════════════════════════════════════════
# 10. Edvanz.Application/Services/StudentTeacherHomeService.cs — two spots
# ═══════════════════════════════════════════════════════════════════════════

$homeServicePath = "Edvanz.Application/Services/StudentTeacherHomeService.cs"

# 10a. BuildAttendanceAsync
Replace-InFile -Path $homeServicePath -Label "StudentTeacherHomeService attendance call" -Find @'
            var result = await _attendanceService.GetStudentViewAttendanceAsync(
                teacherId, teacherStudentId, request, AttendanceViewerType.Student);
'@ -Replace @'
            var result = await _attendanceService.GetStudentViewAttendanceAsync(
                teacherId, teacherStudentId, request, ContentViewerType.Student);
'@

# 10b. BuildPaymentAsync
Replace-InFile -Path $homeServicePath -Label "StudentTeacherHomeService payment call" -Find @'
            var result = await _paymentService.GetStudentPaymentTrackingAsync(
                teacherId, teacherStudentId, PaymentViewerType.Student);
'@ -Replace @'
            var result = await _paymentService.GetStudentPaymentTrackingAsync(
                teacherId, teacherStudentId, ContentViewerType.Student);
'@

# ═══════════════════════════════════════════════════════════════════════════
# 11. DELETE the two old enum files — LAST, only after every reference above
#     has been repointed to ContentViewerType. If the script aborted partway
#     through steps 2-10, these still exist, so a partial run still compiles
#     against the OLD enums until you finish the remaining edits by hand.
# ═══════════════════════════════════════════════════════════════════════════

Remove-FileIfExists -Path "Edvanz.Domain/Enums/AttendanceViewerType.cs" -Label "AttendanceViewerType.cs"
Remove-FileIfExists -Path "Edvanz.Domain/Enums/PaymentViewerType.cs" -Label "PaymentViewerType.cs"

Write-Host ""
Write-Host "Phase 3 applied. Next steps:"
Write-Host "  1. dotnet build — should be clean; if anything still references"
Write-Host "     AttendanceViewerType or PaymentViewerType, the build will name the file."
Write-Host "  2. No migration needed — this is a C# type rename only, ContentViewerType.Student"
Write-Host "     == 1 and ContentViewerType.Parent == 2, identical byte values to both enums removed."
Write-Host "  3. Postman regression: same attendance/payment endpoints as Phase 1 (both roles,"
Write-Host "     both link methods) — responses should be byte-identical to before this phase."
Write-Host "  4. CLAUDE.md still says 'AttendanceViewerType' / 'PaymentViewerType' in a few places"
Write-Host "     (prose, not compiled) — worth a doc pass when you have a moment; not blocking."
