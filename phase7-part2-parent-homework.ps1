<#
    Phase 7 (part 2) — Homework List (Student + Parent, built together)
    ─────────────────────────────────────────────────────────────────────────
    Belal's call: build the missing homework list feature now, for BOTH sides
    at once (student had none either -- see Phase 7 part 1's finding). Mirrors
    GetOfflineExamsForStudentPagedAsync's shape/index exactly, filtered to
    AssignmentType.Homework, minus the exam-only leaderboard rank -- homework
    has no cohort-ranking concept, just completion + optional grade.

    Files touched:
      EDIT   Edvanz.Domain/Interfaces/IExamHomeworkRepo.cs
             (+ StudentHomeworkRow projection, + GetHomeworkForStudentPagedAsync declaration)
      EDIT   Edvanz.Infrastructure/Repositories/ExamHomeworkRepo.cs
             (+ GetHomeworkForStudentPagedAsync implementation)
      NEW    Edvanz.Application/Dtos/ExamHomework/StudentHomeworkDtos.cs
             (StudentHomeworkListItemDto)
      EDIT   Edvanz.Application/ServiceContract/IExamHomeworkService.cs
             (+ GetMyHomeworkAsync declaration)
      EDIT   Edvanz.Application/Services/ExamHomeworkService.cs
             (+ GetMyHomeworkAsync implementation, + mapper -- reuses the existing
             ResolveTeacherSubjectAsync helper, does not duplicate it)
      EDIT   Edvanz.API/Controllers/StudentAssignmentObligationsController.cs
             (+ GetMyHomework action)
      EDIT   Edvanz.API/Controllers/ParentAssignmentObligationsController.cs
             (+ GetChildHomework action; class doc comment updated -- the "offline
             exams only" framing from part 1 is now stale)
      EDIT   Edvanz.Domain/Resources/Messages.en.resx  (+ HomeworkRetrieved key)
      EDIT   Edvanz.Domain/Resources/Messages.ar.resx  (+ HomeworkRetrieved key)

    NOT included (flagged, not silently decided): the home-aggregate stub in
    StudentTeacherHomeService.cs (`Homework = new HomeHomeworkDto { Visible =
    vHomework, Count = 0 }`) is left as-is. Wiring a real Count risks guessing
    the wrong semantic (lifetime-total vs. pending-only, matching or diverging
    from HomeExamsDto.TileCount's "upcoming only" convention) without a quick
    confirm on what "Count" should mean -- flagged in the script output.

    Localization note: HomeworkRetrieved's Arabic text is a reasonable but
    UNVERIFIED translation -- I have not seen this repo's actual Arabic resx
    values to calibrate register/dialect precisely. Sanity-check it against
    the existing Messages.ar.resx style before shipping.

    USAGE
    -----
        powershell -ExecutionPolicy Bypass -File .\phase7-part2-parent-homework.ps1

    Requires Phase 7 part 1 (phase7-parent-offline-exams.ps1) to have run first
    -- this script edits ParentAssignmentObligationsController.cs, which that
    script creates.

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

# ═══════════════════════════════════════════════════════════════════════════
# 1. Edvanz.Domain/Interfaces/IExamHomeworkRepo.cs
# ═══════════════════════════════════════════════════════════════════════════

$iRepoPath = "Edvanz.Domain/Interfaces/IExamHomeworkRepo.cs"

# 1a. New projection row, after StudentOfflineExamRow
Replace-InFile -Path $iRepoPath -Label "IExamHomeworkRepo — add StudentHomeworkRow projection" -Find @'
public sealed class StudentOfflineExamRow
{
    public long OccurrenceId { get; set; }
    public long TemplateId { get; set; }
    public string ExamName { get; set; } = null!;
    public string? Notes { get; set; }
    public DateTime DueDate { get; set; }
    public decimal? GradeValue { get; set; }
    public decimal? MaxGradeSnapshot { get; set; }
    public ObligationStatus Status { get; set; }
}
'@ -Replace @'
public sealed class StudentOfflineExamRow
{
    public long OccurrenceId { get; set; }
    public long TemplateId { get; set; }
    public string ExamName { get; set; } = null!;
    public string? Notes { get; set; }
    public DateTime DueDate { get; set; }
    public decimal? GradeValue { get; set; }
    public decimal? MaxGradeSnapshot { get; set; }
    public ObligationStatus Status { get; set; }
}

/// <summary>
/// Student-facing projection for one homework occurrence (Module 6, AssignmentType.Homework).
/// One row per occurrence the student has an obligation for. Unlike exams, homework has no
/// leaderboard/rank concept — just completion status and an optional grade, gated by
/// TrackingModeSnapshot (CompletionOnly never carries a grade even if the column is non-null).
/// </summary>
public sealed class StudentHomeworkRow
{
    public long OccurrenceId { get; set; }
    public long TemplateId { get; set; }
    public string HomeworkName { get; set; } = null!;
    public string? Notes { get; set; }
    public DateTime DueDate { get; set; }
    public decimal? GradeValue { get; set; }
    public decimal? MaxGradeSnapshot { get; set; }
    public HomeworkTrackingMode? TrackingModeSnapshot { get; set; }
    public ObligationStatus Status { get; set; }
}
'@

# 1b. New method declaration, after GetOfflineExamsForStudentPagedAsync's declaration
Replace-InFile -Path $iRepoPath -Label "IExamHomeworkRepo — add GetHomeworkForStudentPagedAsync declaration" -Find @'
    Task<(IReadOnlyList<StudentOfflineExamRow> Items, int TotalCount)> GetOfflineExamsForStudentPagedAsync(
        long teacherId, long teacherStudentId, int page, int pageSize);
'@ -Replace @'
    Task<(IReadOnlyList<StudentOfflineExamRow> Items, int TotalCount)> GetOfflineExamsForStudentPagedAsync(
        long teacherId, long teacherStudentId, int page, int pageSize);

    /// <summary>
    /// Student-facing, paged: homework occurrences (AssignmentType.Homework) the given student
    /// has an obligation for under this teacher, joined to its template for display fields.
    /// One row per occurrence, ordered by DueDate descending. Same query shape and index as
    /// GetOfflineExamsForStudentPagedAsync (IX_StudentAssignmentObligations_StudentHistory),
    /// filtered to Homework instead of Exam.
    /// </summary>
    Task<(IReadOnlyList<StudentHomeworkRow> Items, int TotalCount)> GetHomeworkForStudentPagedAsync(
        long teacherId, long teacherStudentId, int page, int pageSize);
'@

# ═══════════════════════════════════════════════════════════════════════════
# 2. Edvanz.Infrastructure/Repositories/ExamHomeworkRepo.cs
# ═══════════════════════════════════════════════════════════════════════════

$repoImplPath = "Edvanz.Infrastructure/Repositories/ExamHomeworkRepo.cs"

Replace-InFile -Path $repoImplPath -Label "ExamHomeworkRepo — add GetHomeworkForStudentPagedAsync implementation" -Find @'
            .Select(o => new StudentOfflineExamRow
            {
                OccurrenceId = o.OccurrenceId,
                TemplateId = o.Occurrence.TemplateId,
                ExamName = o.Occurrence.Template.Name,
                Notes = o.Occurrence.Template.Notes,
                DueDate = o.Occurrence.DueDate,
                GradeValue = o.GradeValue,
                MaxGradeSnapshot = o.Occurrence.MaxGradeSnapshot,
                Status = o.Status,
            })
            .AsNoTracking()
            .ToListAsync();

        return (items, totalCount);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<StudentExamRankRow>> GetStudentExamRanksAsync(
'@ -Replace @'
            .Select(o => new StudentOfflineExamRow
            {
                OccurrenceId = o.OccurrenceId,
                TemplateId = o.Occurrence.TemplateId,
                ExamName = o.Occurrence.Template.Name,
                Notes = o.Occurrence.Template.Notes,
                DueDate = o.Occurrence.DueDate,
                GradeValue = o.GradeValue,
                MaxGradeSnapshot = o.Occurrence.MaxGradeSnapshot,
                Status = o.Status,
            })
            .AsNoTracking()
            .ToListAsync();

        return (items, totalCount);
    }

    /// <inheritdoc />
    public async Task<(IReadOnlyList<StudentHomeworkRow> Items, int TotalCount)> GetHomeworkForStudentPagedAsync(
        long teacherId, long teacherStudentId, int page, int pageSize)
    {
        var query = _context.StudentAssignmentObligations
            .Where(o => o.TeacherId == teacherId
                     && o.TeacherStudentId == teacherStudentId
                     && o.Occurrence.Template.AssignmentType == AssignmentType.Homework);

        int totalCount = await query.CountAsync();

        var items = await query
            .OrderByDescending(o => o.Occurrence.DueDate)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(o => new StudentHomeworkRow
            {
                OccurrenceId = o.OccurrenceId,
                TemplateId = o.Occurrence.TemplateId,
                HomeworkName = o.Occurrence.Template.Name,
                Notes = o.Occurrence.Template.Notes,
                DueDate = o.Occurrence.DueDate,
                GradeValue = o.GradeValue,
                MaxGradeSnapshot = o.Occurrence.MaxGradeSnapshot,
                TrackingModeSnapshot = o.Occurrence.TrackingModeSnapshot,
                Status = o.Status,
            })
            .AsNoTracking()
            .ToListAsync();

        return (items, totalCount);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<StudentExamRankRow>> GetStudentExamRanksAsync(
'@

# ═══════════════════════════════════════════════════════════════════════════
# 3. NEW FILE — StudentHomeworkDtos.cs
# ═══════════════════════════════════════════════════════════════════════════

$dtoPath = "Edvanz.Application/Dtos/ExamHomework/StudentHomeworkDtos.cs"

if (Test-Path $dtoPath) {
    Write-Host "[SKIP] Already exists -> $dtoPath"
} else {

$dtoContent = @'
using System.Text.Json.Serialization;
using Edvanz.Domain.Enums;

namespace Edvanz.Application.Dtos.ExamHomework;

/// <summary>
/// Student-facing projection for one homework occurrence (Module 6, AssignmentType.Homework).
/// Mirrors StudentOfflineExamListItemDto's shape, adapted for homework's simpler status model
/// (completion-based, optionally graded — no leaderboard rank, unlike exams).
/// </summary>
public sealed class StudentHomeworkListItemDto
{
    public long HomeworkId { get; set; }
    public string HomeworkName { get; set; } = null!;
    public string? Description { get; set; }
    public DateOnly DueDate { get; set; }
    public string? Subject { get; set; }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public ObligationStatus Status { get; set; }

    /// <summary>CompletionOnly or Graded — tells the client whether to expect Grade/MaxGrade at all.</summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public HomeworkTrackingMode? TrackingMode { get; set; }

    /// <summary>Null unless TrackingMode is Graded AND a grade has been entered.</summary>
    public decimal? Grade { get; set; }

    /// <summary>Null for CompletionOnly homework.</summary>
    public decimal? MaxGrade { get; set; }
}
'@

Set-ContentWithRetry -Path $dtoPath -Value ($dtoContent -replace "`n", "`r`n")
Write-Host "[OK] Created $dtoPath"

}

# ═══════════════════════════════════════════════════════════════════════════
# 4. Edvanz.Application/ServiceContract/IExamHomeworkService.cs
# ═══════════════════════════════════════════════════════════════════════════

$iServicePath = "Edvanz.Application/ServiceContract/IExamHomeworkService.cs"

Replace-InFile -Path $iServicePath -Label "IExamHomeworkService — add GetMyHomeworkAsync declaration" -Find @'
    /// <param name="studentLanguage">The calling student's language preference ("ar"/"en") for the
    /// language-aware subject name; resolved from the JWT in the controller.</param>
    Task<Result<PaginatedResponse<List<StudentOfflineExamListItemDto>>>> GetMyOfflineExamsAsync(
        long teacherId, long teacherStudentId, string? studentLanguage, int page, int pageSize);
}
'@ -Replace @'
    /// <param name="studentLanguage">The calling student's language preference ("ar"/"en") for the
    /// language-aware subject name; resolved from the JWT in the controller.</param>
    Task<Result<PaginatedResponse<List<StudentOfflineExamListItemDto>>>> GetMyOfflineExamsAsync(
        long teacherId, long teacherStudentId, string? studentLanguage, int page, int pageSize);

    /// <summary>
    /// Every homework assignment (AssignmentType.Homework) the calling student has an obligation
    /// for under this teacher, paginated, sorted by date descending. Mirrors
    /// GetMyOfflineExamsAsync's shape (same F3 subject resolution), minus the F1 leaderboard
    /// rank — homework has no cohort-ranking concept, just completion + optional grade.
    /// </summary>
    /// <param name="studentLanguage">The calling student's language preference ("ar"/"en") for the
    /// language-aware subject name; resolved from the JWT in the controller.</param>
    Task<Result<PaginatedResponse<List<StudentHomeworkListItemDto>>>> GetMyHomeworkAsync(
        long teacherId, long teacherStudentId, string? studentLanguage, int page, int pageSize);
}
'@

# ═══════════════════════════════════════════════════════════════════════════
# 5. Edvanz.Application/Services/ExamHomeworkService.cs
# ═══════════════════════════════════════════════════════════════════════════

$serviceImplPath = "Edvanz.Application/Services/ExamHomeworkService.cs"

Replace-InFile -Path $serviceImplPath -Label "ExamHomeworkService — add GetMyHomeworkAsync + mapper" -Find @'
        return Result<PaginatedResponse<List<StudentOfflineExamListItemDto>>>.Success(
            response, _localizer, "OfflineExamsRetrieved");
    }

    /// <summary>
    /// F3 — resolves the teacher's subject for student-facing display. Replicates the canonical
'@ -Replace @'
        return Result<PaginatedResponse<List<StudentOfflineExamListItemDto>>>.Success(
            response, _localizer, "OfflineExamsRetrieved");
    }

    /// <inheritdoc />
    public async Task<Result<PaginatedResponse<List<StudentHomeworkListItemDto>>>> GetMyHomeworkAsync(
        long teacherId, long teacherStudentId, string? studentLanguage, int page, int pageSize)
    {
        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 100) pageSize = 20;

        var (rows, totalCount) = await _unitOfWork.ExamHomeworkRepo
            .GetHomeworkForStudentPagedAsync(teacherId, teacherStudentId, page, pageSize);

        // Same F3 subject-resolution pattern as offline exams — resolve ONCE per page, reusing
        // the existing helper rather than duplicating it.
        string? subject = await ResolveTeacherSubjectAsync(teacherId, studentLanguage);

        var dtos = rows.Select(r => MapToStudentHomeworkListItemDto(r, subject)).ToList();

        var response = new PaginatedResponse<List<StudentHomeworkListItemDto>>
        {
            data = dtos,
            page = page,
            pageSize = pageSize,
            totalCount = totalCount,
            totalPages = (int)Math.Ceiling(totalCount / (double)pageSize),
        };

        return Result<PaginatedResponse<List<StudentHomeworkListItemDto>>>.Success(
            response, _localizer, "HomeworkRetrieved");
    }

    /// <summary>
    /// Maps one homework row to its student DTO. Grade/MaxGrade are surfaced only when
    /// TrackingModeSnapshot is Graded — CompletionOnly homework never carries a grade regardless
    /// of what's stored (defensive: GradeValue should already be null for CompletionOnly per
    /// ValidateHomeworkGrade, but the DTO mapper doesn't trust that alone).
    /// </summary>
    private static StudentHomeworkListItemDto MapToStudentHomeworkListItemDto(
        StudentHomeworkRow row, string? subject) => new()
    {
        HomeworkId = row.OccurrenceId,
        HomeworkName = row.HomeworkName,
        Description = row.Notes,
        DueDate = DateOnly.FromDateTime(row.DueDate),
        Subject = subject,
        Status = row.Status,
        TrackingMode = row.TrackingModeSnapshot,
        Grade = row.TrackingModeSnapshot == HomeworkTrackingMode.Graded ? row.GradeValue : null,
        MaxGrade = row.TrackingModeSnapshot == HomeworkTrackingMode.Graded ? row.MaxGradeSnapshot : null,
    };

    /// <summary>
    /// F3 — resolves the teacher's subject for student-facing display. Replicates the canonical
'@

# ═══════════════════════════════════════════════════════════════════════════
# 6. Edvanz.API/Controllers/StudentAssignmentObligationsController.cs
# ═══════════════════════════════════════════════════════════════════════════

$studentControllerPath = "Edvanz.API/Controllers/StudentAssignmentObligationsController.cs"

Replace-InFile -Path $studentControllerPath -Label "StudentAssignmentObligationsController — add GetMyHomework action" -Find @'
        return ToResponse(await _service.GetMyOfflineExamsAsync(
            teacherId, resolution.TeacherStudentId!.Value, resolution.LanguagePreference, page, pageSize));
    }
    // ──────────────────────────────────────────────────────────────────
    // PRIVATE HELPERS — verbatim copy of StudentOnlineExamsController's resolution pattern
    // ──────────────────────────────────────────────────────────────────
'@ -Replace @'
        return ToResponse(await _service.GetMyOfflineExamsAsync(
            teacherId, resolution.TeacherStudentId!.Value, resolution.LanguagePreference, page, pageSize));
    }

    /// <summary>
    /// Lists every homework assignment (AssignmentType.Homework) the calling student has an
    /// obligation for under the given teacher, paginated, sorted by date descending.
    /// </summary>
    /// <param name="teacherId">The teacher whose homework is requested.</param>
    /// <param name="page">1-based page number. Defaults to 1.</param>
    /// <param name="pageSize">Records per page. Defaults to 20, max 100.</param>
    /// <response code="200">Paginated homework list returned.</response>
    /// <response code="401">Caller is not authenticated.</response>
    /// <response code="403">Caller has no active link with this teacher, or their enrollment was removed.</response>
    /// <response code="404">Caller has no student account.</response>
    [HttpGet("teachers/{teacherId:long}/homework")]
    [ModulePermission(roles: new[] { "Student" }, roleOnly: true)]
    [ProducesResponseType(typeof(Edvanz.Application.Dtos.Result<Edvanz.Application.Dtos.PaginatedResponse<System.Collections.Generic.List<Edvanz.Application.Dtos.ExamHomework.StudentHomeworkListItemDto>>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(object), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetMyHomework(
        [FromRoute] long teacherId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var resolution = await ResolveStudentForTeacherAsync(teacherId);
        if (resolution.ErrorResponse is not null) return resolution.ErrorResponse;

        return ToResponse(await _service.GetMyHomeworkAsync(
            teacherId, resolution.TeacherStudentId!.Value, resolution.LanguagePreference, page, pageSize));
    }
    // ──────────────────────────────────────────────────────────────────
    // PRIVATE HELPERS — verbatim copy of StudentOnlineExamsController's resolution pattern
    // ──────────────────────────────────────────────────────────────────
'@

# ═══════════════════════════════════════════════════════════════════════════
# 7. Edvanz.API/Controllers/ParentAssignmentObligationsController.cs
#    (created by Phase 7 part 1 — must have run already)
# ═══════════════════════════════════════════════════════════════════════════

$parentControllerPath = "Edvanz.API/Controllers/ParentAssignmentObligationsController.cs"

if (-not (Test-Path $parentControllerPath)) {
    throw "ParentAssignmentObligationsController.cs not found. Run phase7-parent-offline-exams.ps1 (Phase 7 part 1) first — this script edits the file it creates."
}

# 7a. Class doc comment — the "offline exams only" framing is now stale
Replace-InFile -Path $parentControllerPath -Label "ParentAssignmentObligationsController class doc update" -Find @'
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
'@ -Replace @'
/// SCOPE — OFFLINE EXAMS + HOMEWORK: offline exams reuse
/// <see cref="IExamHomeworkService.GetMyOfflineExamsAsync"/> COMPLETELY UNCHANGED (no
/// caller-identity branching, no visibility-flag check inside the method itself — matches the
/// same precedent already found for video and online-exam lists). Homework is NEW as of this
/// phase — the student side previously had no homework list endpoint anywhere in the app
/// (<c>StudentTeacherHomeService.GetTeacherHomeAsync</c> hardcoded
/// <c>Homework = new HomeHomeworkDto { Visible = vHomework, Count = 0 }</c>, "read surface not
/// built yet"); <see cref="IExamHomeworkService.GetMyHomeworkAsync"/> was built alongside its
/// student twin (<c>StudentAssignmentObligationsController.GetMyHomework</c>) in the same
/// change, not invented parent-side-only. Neither offline exams nor homework has a separate
/// result/review endpoint — teacher-graded, not self-service like video/online quizzes, so each
/// list item already carries the full outcome (Score/MaxGrade/Rank/Status for exams;
/// Grade/MaxGrade/TrackingMode/Status for homework).
'@

# 7b. New action — child homework list
Replace-InFile -Path $parentControllerPath -Label "ParentAssignmentObligationsController — add GetChildHomework action" -Find @'
        var result = await _service.GetMyOfflineExamsAsync(
            teacherId, resolution.TeacherStudentId!.Value, resolution.ParentLanguagePreference, page, pageSize);
        return ToResponse(result);
    }
}
'@ -Replace @'
        var result = await _service.GetMyOfflineExamsAsync(
            teacherId, resolution.TeacherStudentId!.Value, resolution.ParentLanguagePreference, page, pageSize);
        return ToResponse(result);
    }

    // ──────────────────────────────────────────────────────────────────────
    // CHILD HOMEWORK LIST
    // GET /api/assignmentobligations/parent/children/{childId}/teachers/{teacherId}/homework
    // ──────────────────────────────────────────────────────────────────────
    [HttpGet("children/{childId:long}/teachers/{teacherId:long}/homework")]
    [ModulePermission(roles: new[] { "Parent" }, roleOnly: true)]
    [ProducesResponseType(typeof(Edvanz.Application.Dtos.Result<Edvanz.Application.Dtos.PaginatedResponse<System.Collections.Generic.List<Edvanz.Application.Dtos.ExamHomework.StudentHomeworkListItemDto>>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(object), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetChildHomework(
        [FromRoute] long childId, [FromRoute] long teacherId,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var resolution = await ResolveChildForParentAsync(childId, teacherId);
        if (resolution.ErrorResponse is not null) return resolution.ErrorResponse;

        var result = await _service.GetMyHomeworkAsync(
            teacherId, resolution.TeacherStudentId!.Value, resolution.ParentLanguagePreference, page, pageSize);
        return ToResponse(result);
    }
}
'@

# ═══════════════════════════════════════════════════════════════════════════
# 8. Localization — Messages.en.resx / Messages.ar.resx
# ═══════════════════════════════════════════════════════════════════════════

Replace-InFile -Path "Edvanz.Domain/Messages.en.resx" -Label "Messages.en.resx — add HomeworkRetrieved" -Find @'
</root>
'@ -Replace @'
  <data name="HomeworkRetrieved" xml:space="preserve">
    <value>Homework retrieved successfully.</value>
  </data>
</root>
'@

Replace-InFile -Path "Edvanz.Domain/Messages.ar.resx" -Label "Messages.ar.resx — add HomeworkRetrieved" -Find @'
</root>
'@ -Replace @'
  <data name="HomeworkRetrieved" xml:space="preserve">
    <value>تم استرجاع الواجبات بنجاح</value>
  </data>
</root>
'@

Write-Host ""
Write-Host "Phase 7 part 2 applied. Next steps:"
Write-Host "  1. dotnet build."
Write-Host "  2. No migration -- StudentAssignmentObligation/AssignmentOccurrence already carry"
Write-Host "     every field this reads (Status, GradeValue, MaxGradeSnapshot, TrackingModeSnapshot)."
Write-Host "  3. Sanity-check the Arabic HomeworkRetrieved string against your resx's existing"
Write-Host "     tone/dialect -- I have not seen your actual Arabic values to calibrate this."
Write-Host "  4. Postman regression: a student AND a parent (for a linked child, both Method A"
Write-Host "     and Method B) listing homework -- test both CompletionOnly and Graded tracking"
Write-Host "     mode occurrences, confirm Grade/MaxGrade are null for CompletionOnly rows even"
Write-Host "     if GradeValue somehow has a stray value in the DB."
Write-Host "  5. STILL OPEN, not done here: StudentTeacherHomeService's home-tile Homework.Count"
Write-Host "     is still hardcoded to 0. Wiring it needs a quick call on what 'Count' means --"
Write-Host "     lifetime total (GetHomeworkCompletionStatsAsync gives this cheaply) vs."
Write-Host "     upcoming/pending-only (matching HomeExamsDto.TileCount's convention, but no"
Write-Host "     ready-made aggregate exists for that yet)."
