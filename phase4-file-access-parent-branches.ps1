<#
    Phase 4 — FileAccessService Parent Branches
    ─────────────────────────────────────────────────────────────────────────
    Closes the blocker the original audit flagged: IsReadAuthorizedAsync had no
    Parent branch at all, so every video thumbnail/attachment/question image and
    online-exam question image would 403 for a parent even after Phases 5-7 wire
    up parent video/exam endpoints. NationalIdImage intentionally untouched --
    owner+admin only, no parent branch, per phase scope.

    DESIGN NOTE (deviates from the original phase-doc wording "2 new repo
    methods" -- flagged, not silently substituted): implemented with ZERO new
    repository methods. The parent-side resolution (JWT -> ParentUser -> active
    children -> whichever are linked to this teacher, Method A or B) is composed
    entirely from IUserRepo methods that already exist (the same ones
    ParentScopedApiBaseController uses). The actual scope check then reuses the
    EXISTING IsStudentInVideoScopeAsync / IsQuestionImageAssignedToStudentAsync
    in a loop over the parent's resolved children -- since it's the literal same
    method call, the Published/PublishDate gate and exam-assignment logic are
    inherited automatically, never re-derived. No new EF query, no migration.

    A parent can have more than one child linked to the same teacher (siblings/
    twins) -- hence a LIST of teacherStudentIds, not a single nullable id like
    the student-side resolver.

    Files touched:
      EDIT   Edvanz.Application/Services/FileAccessService.cs

    Zero behaviour change for existing callers: teacher-tenant and student reads
    short-circuit before the new parent branch is ever evaluated (C# && / early
    return), so this is purely additive -- no added latency, no risk to the two
    caller types already working.

    USAGE
    -----
        powershell -ExecutionPolicy Bypass -File .\phase4-file-access-parent-branches.ps1

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

$fileAccessServicePath = "Edvanz.Application/Services/FileAccessService.cs"

# ═══════════════════════════════════════════════════════════════════════════
# 1. Class-level doc comment — reflect the new parent branches
# ═══════════════════════════════════════════════════════════════════════════

Replace-InFile -Path $fileAccessServicePath -Label "FileAccessService class doc comment" -Find @'
/// Central file-registry authorization + lifecycle service. See <see cref="IFileAccessService"/>.
///
/// Authorization order for a gated read (fail-closed): owner → SuperAdmin → category policy. The
/// teacher tenant authorizes by the denormalized <see cref="FileObject.TeacherId"/> column (set
/// server-side from the uploader's JWT — never client-supplied, §4.4). Student branches: the
/// online-exam image checks live exam assignment; the video categories (photo / attachment /
/// video-exam question image) check live membership of the OWNING VIDEO's scope, which also
/// enforces the Published + PublishDate gate. The national-ID image has no resource policy
/// (owner + admin only).
/// </summary>
'@ -Replace @'
/// Central file-registry authorization + lifecycle service. See <see cref="IFileAccessService"/>.
///
/// Authorization order for a gated read (fail-closed): owner → SuperAdmin → category policy. The
/// teacher tenant authorizes by the denormalized <see cref="FileObject.TeacherId"/> column (set
/// server-side from the uploader's JWT — never client-supplied, §4.4). Student AND parent
/// branches (Phase 4, parent parity): the online-exam image checks live exam assignment; the
/// video categories (photo / attachment / video-exam question image) check live membership of
/// the OWNING VIDEO's scope, which also enforces the Published + PublishDate gate. A parent's
/// check runs across every teacherStudentId reachable through their active children under the
/// file's teacher (almost always 0 or 1, but never assumed to be exactly one — nothing stops a
/// parent having two children with the same teacher), delegating to the SAME canonical scope
/// predicates the student path uses, so the Published gate and exam-assignment logic are
/// inherited automatically, never re-derived. The national-ID image has no resource policy
/// (owner + admin only) for either caller type.
/// </summary>
'@

# ═══════════════════════════════════════════════════════════════════════════
# 2. IsReadAuthorizedAsync — add the parent fallback to both gated categories
# ═══════════════════════════════════════════════════════════════════════════

Replace-InFile -Path $fileAccessServicePath -Label "FileAccessService.IsReadAuthorizedAsync switch" -Find @'
        // 3. Category policy.
        switch (file.Category)
        {
            case FileCategory.VideoPhoto:
            case FileCategory.VideoAttachment:
            case FileCategory.VideoExamQuestionImage:
                if (await IsSameTeacherTenantAsync(file))
                    return true;
                return await IsScopedStudentOfVideoAsync(file);

            case FileCategory.OnlineExamQuestionImage:
                if (await IsSameTeacherTenantAsync(file))
                    return true;
                return await IsAssignedStudentAsync(file);

            case FileCategory.NationalIdImage:
            default:
                // National-ID has no resource policy (owner + admin already handled above);
                // any unhandled category is denied.
                return false;
        }
    }
'@ -Replace @'
        // 3. Category policy.
        switch (file.Category)
        {
            case FileCategory.VideoPhoto:
            case FileCategory.VideoAttachment:
            case FileCategory.VideoExamQuestionImage:
                if (await IsSameTeacherTenantAsync(file))
                    return true;
                if (await IsScopedStudentOfVideoAsync(file))
                    return true;
                return await IsScopedParentOfVideoAsync(file);

            case FileCategory.OnlineExamQuestionImage:
                if (await IsSameTeacherTenantAsync(file))
                    return true;
                if (await IsAssignedStudentAsync(file))
                    return true;
                return await IsAssignedParentAsync(file);

            case FileCategory.NationalIdImage:
            default:
                // National-ID has no resource policy (owner + admin already handled above);
                // any unhandled category is denied. Same for parents — intentionally no parent
                // branch here; a parent never needs to read the sign-up ID image.
                return false;
        }
    }
'@

# ═══════════════════════════════════════════════════════════════════════════
# 3. New private helpers — inserted after ResolveBoundTeacherStudentIdAsync,
#    before ResolveCallerTeacherIdAsync's doc comment
# ═══════════════════════════════════════════════════════════════════════════

Replace-InFile -Path $fileAccessServicePath -Label "FileAccessService parent-branch helpers" -Find @'
        var link = await _unitOfWork.Users.GetActiveStudentTeacherLinkAsync(studentUser.Id, teacherId);
        if (link is null || link.LinkStatus != LinkStatus.Active || link.TeacherStudentId is null)
            return null;

        return link.TeacherStudentId;
    }

    /// <summary>
    /// Resolves the caller's teacher id from the JWT — teacher lookup, or assistant → owning tutor.
'@ -Replace @'
        var link = await _unitOfWork.Users.GetActiveStudentTeacherLinkAsync(studentUser.Id, teacherId);
        if (link is null || link.LinkStatus != LinkStatus.Active || link.TeacherStudentId is null)
            return null;

        return link.TeacherStudentId;
    }

    /// <summary>
    /// True when the caller is a PARENT whose child(ren) the file's OWNING VIDEO is scoped to.
    /// Applies to the same three categories as <see cref="IsScopedStudentOfVideoAsync"/>. Resolves
    /// every teacherStudentId reachable by the calling parent under the file's teacher tenant, then
    /// checks each against the SAME canonical <c>IsStudentInVideoScopeAsync</c> predicate the
    /// student path uses — so the Published + PublishDate gate is inherited, not re-derived.
    /// </summary>
    private async Task<bool> IsScopedParentOfVideoAsync(FileObject file)
    {
        if (file.TeacherId is null)
            return false;

        var teacherStudentIds = await ResolveChildTeacherStudentIdsForParentAsync(file.TeacherId.Value);
        if (teacherStudentIds.Count == 0)
            return false;

        long? videoAssetId = file.Category == FileCategory.VideoAttachment
            ? file.VideoAssetId
            : await _unitOfWork.VideoAssetsRepo.GetOwningVideoAssetIdForFileAsync(
                  file.Id, file.Category, file.TeacherId.Value);
        if (videoAssetId is null)
            return false;

        foreach (var teacherStudentId in teacherStudentIds)
        {
            if (await _unitOfWork.VideoAssetsRepo.IsStudentInVideoScopeAsync(
                    teacherStudentId, videoAssetId.Value, file.TeacherId.Value))
                return true;
        }

        return false;
    }

    /// <summary>
    /// True when the caller is a PARENT with a child in the file's exam's live assigned set.
    /// Mirrors <see cref="IsAssignedStudentAsync"/> — resolves every teacherStudentId reachable by
    /// the calling parent under the file's teacher tenant, then checks each against the SAME
    /// canonical <c>IsQuestionImageAssignedToStudentAsync</c> predicate the student path uses.
    /// </summary>
    private async Task<bool> IsAssignedParentAsync(FileObject file)
    {
        if (file.TeacherId is null)
            return false;

        var teacherStudentIds = await ResolveChildTeacherStudentIdsForParentAsync(file.TeacherId.Value);
        if (teacherStudentIds.Count == 0)
            return false;

        foreach (var teacherStudentId in teacherStudentIds)
        {
            if (await _unitOfWork.OnlineExamsRepo.IsQuestionImageAssignedToStudentAsync(
                    file.Id, file.TeacherId.Value, teacherStudentId))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Resolves every teacherStudentId a PARENT can legitimately reach under the given teacher
    /// tenant — across ALL of the parent's active children, whichever are linked (Method A via
    /// StudentTeacherLink, Method B via ParentChildTeacherLink) to this teacher. Almost always 0
    /// or 1 entries; a list because nothing stops a parent having more than one child tutored by
    /// the same teacher (siblings/twins). Composed entirely from existing IUserRepo methods — the
    /// same ones ParentScopedApiBaseController uses — no new repository query needed. Mirrors
    /// ResolveBoundTeacherStudentIdAsync's Method A/B branch, but for a caller that may resolve to
    /// several ids instead of one.
    /// </summary>
    private async Task<List<long>> ResolveChildTeacherStudentIdsForParentAsync(long teacherId)
    {
        long? userId = _currentUser.UserId;
        if (userId is null)
            return new List<long>();

        var parentUser = await _unitOfWork.Users.GetActiveParentUserByUserIdAsync(userId.Value);
        if (parentUser is null)
            return new List<long>();

        var children = await _unitOfWork.Users.GetActiveChildrenAsync(parentUser.Id);
        if (children.Count == 0)
            return new List<long>();

        var teacherStudentIds = new List<long>();

        foreach (var child in children)
        {
            if (child.LinkMethod == ChildLinkMethod.StudentAccount)
            {
                if (child.StudentUserId is null)
                    continue;

                var studentLink = await _unitOfWork.Users
                    .GetActiveStudentTeacherLinkAsync(child.StudentUserId.Value, teacherId);
                if (studentLink is not null
                    && studentLink.LinkStatus == LinkStatus.Active
                    && studentLink.TeacherStudentId is not null)
                    teacherStudentIds.Add(studentLink.TeacherStudentId.Value);
            }
            else
            {
                var parentLink = await _unitOfWork.Users
                    .GetActiveParentChildTeacherLinkAsync(child.Id, teacherId);
                if (parentLink is not null
                    && parentLink.LinkStatus == LinkStatus.Active
                    && parentLink.TeacherStudentId is not null)
                    teacherStudentIds.Add(parentLink.TeacherStudentId.Value);
            }
        }

        return teacherStudentIds;
    }

    /// <summary>
    /// Resolves the caller's teacher id from the JWT — teacher lookup, or assistant → owning tutor.
'@

Write-Host ""
Write-Host "Phase 4 applied. Next steps:"
Write-Host "  1. dotnet build — this file needs no new usings (ChildLinkMethod/LinkStatus already"
Write-Host "     live in Edvanz.Domain.Enums, already imported here for the student branch)."
Write-Host "  2. No migration — zero schema changes."
Write-Host "  3. Postman regression: existing teacher and student file reads should be untouched"
Write-Host "     (they short-circuit before the new parent branch ever runs). New coverage to spot-"
Write-Host "     check once Phase 5+ ships a parent video/exam endpoint: a parent reading a"
Write-Host "     VideoPhoto/VideoAttachment/VideoExamQuestionImage/OnlineExamQuestionImage file"
Write-Host "     for a Published, in-scope child should now succeed instead of 403; a Draft or"
Write-Host "     out-of-scope video's files should still 403 for that parent."
Write-Host "  4. CLAUDE.md §5.5 documents the pre-Phase-4 category policies verbatim (teacher-tenant"
Write-Host "     OR student only) — now stale on the parent point. Flagging, not editing here."
