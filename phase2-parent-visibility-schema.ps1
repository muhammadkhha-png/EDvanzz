<#
    Phase 2 — Parent Visibility Schema (Video + Online Exam + ExamDefault flip)
    ─────────────────────────────────────────────────────────────────────────
    Adds ParentVisibilityVideo and ParentVisibilityOnlineExamDefault to mirror
    the student-side StudentVisibilityVideo / StudentVisibilityOnlineExamDefault,
    and flips ParentVisibilityExamDefault's C# default to true (D1 — locked).
    Threads all three through TeacherConfigurationDto, UpdateTeacherConfigurationDto,
    ParentChildTeacherDto, and every TeacherService mapping point.

    THIS SCRIPT DOES NOT RUN "dotnet ef migrations add" FOR YOU. It applies only
    the C# code changes (entity, DTOs, service mappings, Fluent config), then
    prints the exact command to run yourself — so you can review the generated
    migration diff before committing to it, same as any other EF change.

    NOT INCLUDED: backfilling existing rows' ParentVisibilityExamDefault (0 -> 1).
    That's the open sub-decision from the phase plan — pending your answer, see
    the chat.

    USAGE
    -----
        powershell -ExecutionPolicy Bypass -File .\phase2-parent-visibility-schema.ps1

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

# ═══════════════════════════════════════════════════════════════════════════
# 1. Edvanz.Domain/Entities/TeacherConfiguration.cs
# ═══════════════════════════════════════════════════════════════════════════

$entityPath = "Edvanz.Domain/Entities/TeacherConfiguration.cs"

Replace-InFile -Path $entityPath -Label "TeacherConfiguration entity properties" -Find @'
    /// <summary>
    /// Default visibility for newly created exams in parent accounts.
    /// AAM-BR-10: Per-exam visibility defaults to hidden unless explicitly enabled.
    /// Per-exam overrides are stored in a separate ExamVisibility table (future module).
    /// Default: false (hidden per AAM-BR-10).
    /// </summary>
    public bool ParentVisibilityExamDefault { get; set; } = false;

    /// <summary>
    /// Timestamp of the last configuration update. Null if never modified after initial creation.
    /// </summary>
    public DateTime? UpdatedAt { get; set; }
'@ -Replace @'
    /// <summary>
    /// Default visibility for newly created exams (offline / homework-track) in parent accounts.
    /// Product decision (Phase 2, parent parity): flipped to VISIBLE by default to match the
    /// student-side flip on StudentVisibilityExamDefault (2026-07-18) — a teacher can still hide
    /// it per account by turning this off. Per-exam overrides remain a future ExamVisibility
    /// module. Supersedes the original AAM-BR-10 "hidden by default".
    /// NOTE: this C# default only governs newly-created configuration rows (set explicitly in
    /// TeacherService.InitializeTeacherAsync's seed block) — it has no effect on rows that
    /// already exist in the database.
    /// Default: true (visible).
    /// </summary>
    public bool ParentVisibilityExamDefault { get; set; } = true;

    /// <summary>
    /// Whether parents can see the Videos module.
    /// Added Phase 2 (parent parity) to mirror StudentVisibilityVideo.
    /// Default: true (visible).
    /// </summary>
    public bool ParentVisibilityVideo { get; set; } = true;

    /// <summary>
    /// Default visibility of online exams in parent accounts.
    /// Added Phase 2 (parent parity) to mirror StudentVisibilityOnlineExamDefault.
    /// Default: true (visible).
    /// </summary>
    public bool ParentVisibilityOnlineExamDefault { get; set; } = true;

    /// <summary>
    /// Timestamp of the last configuration update. Null if never modified after initial creation.
    /// </summary>
    public DateTime? UpdatedAt { get; set; }
'@

# ═══════════════════════════════════════════════════════════════════════════
# 2. Edvanz.Application/Dtos/Teacher/TeacherConfigurationDto.cs
# ═══════════════════════════════════════════════════════════════════════════

$configDtoPath = "Edvanz.Application/Dtos/Teacher/TeacherConfigurationDto.cs"

Replace-InFile -Path $configDtoPath -Label "TeacherConfigurationDto fields" -Find @'
    // ─── AAM-FR-04.9 ───
    public bool ParentVisibilityAttendance { get; set; }
    public bool ParentVisibilityPayment { get; set; }
    public bool ParentVisibilityHomework { get; set; }
    public bool ParentVisibilityExamDefault { get; set; }

    public DateTime? UpdatedAt { get; set; }
'@ -Replace @'
    // ─── AAM-FR-04.9 ───
    public bool ParentVisibilityAttendance { get; set; }
    public bool ParentVisibilityPayment { get; set; }
    public bool ParentVisibilityHomework { get; set; }
    public bool ParentVisibilityExamDefault { get; set; }
    public bool ParentVisibilityVideo { get; set; }
    public bool ParentVisibilityOnlineExamDefault { get; set; }

    public DateTime? UpdatedAt { get; set; }
'@

# ═══════════════════════════════════════════════════════════════════════════
# 3. Edvanz.Application/Dtos/Teacher/UpdateTeacherConfigurationDto.cs
# ═══════════════════════════════════════════════════════════════════════════

$updateDtoPath = "Edvanz.Application/Dtos/Teacher/UpdateTeacherConfigurationDto.cs"

Replace-InFile -Path $updateDtoPath -Label "UpdateTeacherConfigurationDto fields" -Find @'
    // ─── AAM-FR-04.9: Parent Visibility ───

    public bool ParentVisibilityAttendance { get; set; } = true;
    public bool ParentVisibilityPayment { get; set; } = true;
    public bool ParentVisibilityHomework { get; set; } = true;
    public bool ParentVisibilityExamDefault { get; set; } = false;
}
'@ -Replace @'
    // ─── AAM-FR-04.9: Parent Visibility ───

    public bool ParentVisibilityAttendance { get; set; } = true;
    public bool ParentVisibilityPayment { get; set; } = true;
    public bool ParentVisibilityHomework { get; set; } = true;
    public bool ParentVisibilityExamDefault { get; set; } = true;
    public bool ParentVisibilityVideo { get; set; } = true;
    public bool ParentVisibilityOnlineExamDefault { get; set; } = true;
}
'@

# ═══════════════════════════════════════════════════════════════════════════
# 4. Edvanz.Application/Dtos/ParentUser/ParentChildDto.cs (ParentChildTeacherDto)
# ═══════════════════════════════════════════════════════════════════════════

$parentChildDtoPath = "Edvanz.Application/Dtos/ParentUser/ParentChildDto.cs"

Replace-InFile -Path $parentChildDtoPath -Label "ParentChildTeacherDto visibility fields" -Find @'
    // ─── Visibility flags from TeacherConfiguration (AAM-FR-04.9) ───

    public bool VisibilityAttendance { get; set; }
    public bool VisibilityPayment { get; set; }
    public bool VisibilityHomework { get; set; }
    public bool VisibilityExamDefault { get; set; }
}
'@ -Replace @'
    // ─── Visibility flags from TeacherConfiguration (AAM-FR-04.9) ───

    public bool VisibilityAttendance { get; set; }
    public bool VisibilityPayment { get; set; }
    public bool VisibilityHomework { get; set; }
    public bool VisibilityExamDefault { get; set; }

    /// <summary>Whether this teacher allows the parent to see the Videos module. Added Phase 2.</summary>
    public bool VisibilityVideo { get; set; }

    /// <summary>Default online-exam visibility for this teacher. Added Phase 2.</summary>
    public bool VisibilityOnlineExamDefault { get; set; }
}
'@

# ═══════════════════════════════════════════════════════════════════════════
# 5. Edvanz.Application/Services/ParentUserService.cs — BuildTeacherDtoFromBatch
# ═══════════════════════════════════════════════════════════════════════════

$parentUserServicePath = "Edvanz.Application/Services/ParentUserService.cs"

# NOTE: the VisibilityExamDefault fallback also changes here, from `?? false` to
# `?? true` — when a teacher somehow has no configuration row at all (edge case),
# the fail-open default should match the new D1 philosophy, not the old one.
Replace-InFile -Path $parentUserServicePath -Label "ParentUserService.BuildTeacherDtoFromBatch" -Find @'
        return new ParentChildTeacherDto
        {
            LinkId = linkId,
            TeacherCode = teacher?.TeacherCode ?? string.Empty,
            TeacherFullName = teacherFullName,
            SubjectName = subjectName,
            LinkedAt = linkedAt,
            IsEnrollmentActive = isEnrollmentActive,
            VisibilityAttendance = config?.ParentVisibilityAttendance ?? true,
            VisibilityPayment = config?.ParentVisibilityPayment ?? true,
            VisibilityHomework = config?.ParentVisibilityHomework ?? true,
            VisibilityExamDefault = config?.ParentVisibilityExamDefault ?? false
        };
'@ -Replace @'
        return new ParentChildTeacherDto
        {
            LinkId = linkId,
            TeacherCode = teacher?.TeacherCode ?? string.Empty,
            TeacherFullName = teacherFullName,
            SubjectName = subjectName,
            LinkedAt = linkedAt,
            IsEnrollmentActive = isEnrollmentActive,
            VisibilityAttendance = config?.ParentVisibilityAttendance ?? true,
            VisibilityPayment = config?.ParentVisibilityPayment ?? true,
            VisibilityHomework = config?.ParentVisibilityHomework ?? true,
            VisibilityExamDefault = config?.ParentVisibilityExamDefault ?? true,
            VisibilityVideo = config?.ParentVisibilityVideo ?? true,
            VisibilityOnlineExamDefault = config?.ParentVisibilityOnlineExamDefault ?? true
        };
'@

# ═══════════════════════════════════════════════════════════════════════════
# 6. Edvanz.Application/Services/TeacherService.cs — three separate spots
# ═══════════════════════════════════════════════════════════════════════════

$teacherServicePath = "Edvanz.Application/Services/TeacherService.cs"

# 6a. InitializeTeacherAsync default-config seed block
Replace-InFile -Path $teacherServicePath -Label "TeacherService.InitializeTeacherAsync seed block" -Find @'
                StudentVisibilityExamDefault = true, // 2026-07-18: offline exams visible by default (student side)
                StudentVisibilityVideo = true,
                ParentVisibilityAttendance = true,
                ParentVisibilityPayment = true,
                StudentVisibilityOnlineExamDefault = true,
                ParentVisibilityHomework = true,
                ParentVisibilityExamDefault = false, // AAM-BR-10: default hidden
                CreateAt = DateTime.UtcNow
            };
'@ -Replace @'
                StudentVisibilityExamDefault = true, // 2026-07-18: offline exams visible by default (student side)
                StudentVisibilityVideo = true,
                ParentVisibilityAttendance = true,
                ParentVisibilityPayment = true,
                StudentVisibilityOnlineExamDefault = true,
                ParentVisibilityHomework = true,
                ParentVisibilityExamDefault = true, // Phase 2: flipped to match student-side parity (was AAM-BR-10 hidden)
                ParentVisibilityVideo = true, // Phase 2: parent parity with StudentVisibilityVideo
                ParentVisibilityOnlineExamDefault = true, // Phase 2: parent parity with StudentVisibilityOnlineExamDefault
                CreateAt = DateTime.UtcNow
            };
'@

# 6b. GetConfigurationAsync dto builder
Replace-InFile -Path $teacherServicePath -Label "TeacherService.GetConfigurationAsync dto builder" -Find @'
            ParentVisibilityAttendance = config.ParentVisibilityAttendance,
            ParentVisibilityPayment = config.ParentVisibilityPayment,
            ParentVisibilityHomework = config.ParentVisibilityHomework,
            ParentVisibilityExamDefault = config.ParentVisibilityExamDefault,
            UpdatedAt = config.UpdatedAt,
'@ -Replace @'
            ParentVisibilityAttendance = config.ParentVisibilityAttendance,
            ParentVisibilityPayment = config.ParentVisibilityPayment,
            ParentVisibilityHomework = config.ParentVisibilityHomework,
            ParentVisibilityExamDefault = config.ParentVisibilityExamDefault,
            ParentVisibilityVideo = config.ParentVisibilityVideo,
            ParentVisibilityOnlineExamDefault = config.ParentVisibilityOnlineExamDefault,
            UpdatedAt = config.UpdatedAt,
'@

# 6c. SaveConfigurationAsync field updates
Replace-InFile -Path $teacherServicePath -Label "TeacherService.SaveConfigurationAsync field updates" -Find @'
            config.ParentVisibilityAttendance = dto.ParentVisibilityAttendance;
            config.ParentVisibilityPayment = dto.ParentVisibilityPayment;
            config.ParentVisibilityHomework = dto.ParentVisibilityHomework;
            config.ParentVisibilityExamDefault = dto.ParentVisibilityExamDefault;
            config.UpdatedAt = DateTime.UtcNow;
'@ -Replace @'
            config.ParentVisibilityAttendance = dto.ParentVisibilityAttendance;
            config.ParentVisibilityPayment = dto.ParentVisibilityPayment;
            config.ParentVisibilityHomework = dto.ParentVisibilityHomework;
            config.ParentVisibilityExamDefault = dto.ParentVisibilityExamDefault;
            config.ParentVisibilityVideo = dto.ParentVisibilityVideo;
            config.ParentVisibilityOnlineExamDefault = dto.ParentVisibilityOnlineExamDefault;
            config.UpdatedAt = DateTime.UtcNow;
'@

# ═══════════════════════════════════════════════════════════════════════════
# 7. Edvanz.Infrastructure/Persistence/EdvanzDbContext.cs — Fluent defaults
# ═══════════════════════════════════════════════════════════════════════════

$dbContextPath = "Edvanz.Infrastructure/Persistence/EdvanzDbContext.cs"

Replace-InFile -Path $dbContextPath -Label "EdvanzDbContext TeacherConfiguration Fluent config" -Find @'
        #region TeacherConfiguration (1:1 with Teacher)
        modelBuilder.Entity<TeacherConfiguration>(entity =>
        {
            entity.ToTable("TeacherConfigurations");

            // Enforce one-to-one: unique index on TeacherId
            entity.HasIndex(tc => tc.TeacherId)
                .IsUnique()
                .HasDatabaseName("IX_TeacherConfigurations_TeacherId");

            entity.HasOne(tc => tc.Teacher)
                .WithOne(t => t.Configuration)
                .HasForeignKey<TeacherConfiguration>(tc => tc.TeacherId)
                .OnDelete(DeleteBehavior.NoAction);
        });
        #endregion
'@ -Replace @'
        #region TeacherConfiguration (1:1 with Teacher)
        modelBuilder.Entity<TeacherConfiguration>(entity =>
        {
            entity.ToTable("TeacherConfigurations");

            // Enforce one-to-one: unique index on TeacherId
            entity.HasIndex(tc => tc.TeacherId)
                .IsUnique()
                .HasDatabaseName("IX_TeacherConfigurations_TeacherId");

            entity.HasOne(tc => tc.Teacher)
                .WithOne(t => t.Configuration)
                .HasForeignKey<TeacherConfiguration>(tc => tc.TeacherId)
                .OnDelete(DeleteBehavior.NoAction);

            // Phase 2 (parent parity): explicit DB-level defaults so the ADD COLUMN
            // migration backfills existing TeacherConfiguration rows with true (visible),
            // not the bool CLR default of false. No sibling visibility column has this
            // configured (they rely purely on the C# object-initializer + explicit
            // service-layer assignment, which only affects NEWLY created rows) — these two
            // are the exception because parity for teachers who registered before this
            // migration specifically requires the existing rows to be backfilled, which
            // only a DB-level default achieves.
            entity.Property(tc => tc.ParentVisibilityVideo)
                .HasDefaultValue(true);

            entity.Property(tc => tc.ParentVisibilityOnlineExamDefault)
                .HasDefaultValue(true);
        });
        #endregion
'@

# ═══════════════════════════════════════════════════════════════════════════
# 8. Locate the EF project + startup project and print the migration command
# ═══════════════════════════════════════════════════════════════════════════

Write-Host ""
Write-Host "Code changes applied. Locating projects for the migration command..."

$infraCsproj = Get-ChildItem -Recurse -Filter "Edvanz.Infrastructure.csproj" -ErrorAction SilentlyContinue | Select-Object -First 1
$apiCsproj   = Get-ChildItem -Recurse -Filter "Edvanz.API.csproj" -ErrorAction SilentlyContinue | Select-Object -First 1

Write-Host ""
Write-Host "Next step — review and run this yourself (not auto-run by this script):"
Write-Host ""

if ($infraCsproj -and $apiCsproj) {
    $infraDir = $infraCsproj.DirectoryName
    $apiDir = $apiCsproj.DirectoryName
    Write-Host "    dotnet ef migrations add Phase2_ParentVisibilityVideoAndOnlineExam --project `"$infraDir`" --startup-project `"$apiDir`""
} else {
    Write-Host "    dotnet ef migrations add Phase2_ParentVisibilityVideoAndOnlineExam --project Edvanz.Infrastructure --startup-project Edvanz.API"
    Write-Host "    (could not auto-locate both .csproj files — adjust --project / --startup-project paths if this fails)"
}

Write-Host ""
Write-Host "Then review the generated migration under Edvanz.Infrastructure/Migrations/ before running:"
Write-Host "    dotnet ef database update --project ... --startup-project ..."
Write-Host ""
Write-Host "Expect to see in the diff: AddColumn ParentVisibilityVideo (bit, default 1), AddColumn ParentVisibilityOnlineExamDefault (bit, default 1). No AlterColumn for ParentVisibilityExamDefault is expected — that column's default was never Fluent-configured, so only the C# seed-code change (already applied) affects it, and only for brand-new rows."
