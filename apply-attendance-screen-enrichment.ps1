<#
.SYNOPSIS
    Attendance Student List  -  Payment/Debt + Attendance-History Enrichment.
    Anchor-based find/replace, not a git patch  -  safe against CRLF checkout mismatches.

.DESCRIPTION
    Implements the confirmed plan for GET /api/Attendance/sessions/{sessionId}/students
    (Endpoint 3, GetAttendanceStudentListAsync / AttendanceStudentListDto):

      1. TeacherConfiguration  -  two new teacher-facing display toggles, both default false:
           - ShowPaymentInfoOnAttendanceScreen
           - ShowAttendanceHistoryOnAttendanceScreen
         Wired through UpdateTeacherConfigurationDto / TeacherConfigurationDto / TeacherService
         (init defaults, save mapping, get mapping)  -  same pattern as every existing flag.

      2. Payment enrichment (IPaymentRepo.GetPaymentInfoForAttendanceBatchAsync, new)  -  reuses
         the exact CLAUDE.md §7.4 arrears rules and UnpaidPeriodRef type already used by the
         Unpaid Students Overview. hasUnpaidLastMonth is a direct existence check against last
         calendar month's PaymentPeriod window (not inferred from the tail count). Called from
         AttendanceService (new IPaymentRepo batch method, per your confirmed layering choice),
         never from AttendanceRepo directly.

      3. Attendance-history enrichment (IAttendanceRepo.GetAttendanceHistoryCountsBatchAsync,
         new)  -  CourseAbsences is scoped strictly to the student's CURRENT active
         StudentSessionAssignment (confirmed Option A), distinct from the lifetime
         StudentAbsenceCounter.TotalAbsences already on the DTO. CurrentMonthAbsences is scoped
         to the teacher's current local calendar month. WasAbsentLastSession/LastAbsenceDate/
         LastAbsenceSessionName are left exactly as they are today (unconditional  -
         REQ-ATT-028/029/060 absence-alert warning, a different feature from this toggle).

      4. Both enrichments are OPTIONAL and additive on AttendanceStudentRowDto
         (PaymentInfo / HistoryInfo, both nullable nested DTOs)  -  null means "config disabled",
         a populated-but-zeroed object means "enabled, nothing owed / no absences". Existing
         fields, pagination, and counts are byte-for-byte unchanged. The batch lookups are only
         called when the corresponding config flag is true and the page is non-empty  -  when
         disabled, zero extra queries run.

      5. FormatUnpaidPeriodLabel was private inside PaymentService  -  extracted to a new shared
         static helper (Edvanz.Application.Extensions.PaymentLabelFormatter) so AttendanceService
         can reuse the exact same label rule instead of duplicating it. PaymentService's own call
         site is repointed at the shared helper and the now-dead private copy is removed.

      6. Confirmed: Attendance.Take permission alone is sufficient to see the payment info when
         the teacher has the flag on  -  no additional Payment.ViewHistory gate added.

    NOT included here (you'll need to do this yourself after applying):
      - EF Core migration for the two new TeacherConfigurations columns. Per your own rule this
        script does NOT hand-author Designer.cs/ModelSnapshot.cs. Run, from the repo root, after
        applying this script and confirming it builds:
            dotnet ef migrations add AddAttendanceScreenVisibilityFlags --project Edvanz.Infrastructure --startup-project Edvanz.API
        Both new columns are non-nullable bit columns with a CLR default of false on an entity
        that already has exactly one row per teacher (TeacherConfigurations, unique index on
        TeacherId, always created at InitializeTeacherAsync)  -  a plain ADD COLUMN ... DEFAULT 0
        is all EF should generate; no data backfill needed.
      - Postman collection updates / API docs.

.NOTES
    Run from the repo root (muhammadkhha-png/EDvanzz, branch master_integration). Idempotent: if
    an anchor is already gone (i.e. already applied), that step is skipped with a warning, not a
    failure.

    Saved with a UTF-8 BOM on purpose: this file, and the files it edits, contain non-ASCII
    characters (em-dashes, section signs) in comments this script never touches. Windows
    PowerShell 5.1 does not reliably assume UTF-8 for a BOM-less script or a BOM-less
    Get-Content read  -  without the BOM here, and -Encoding UTF8 below, those bytes get silently
    reinterpreted under the console's codepage and the script (or the files it writes) end up
    corrupted. If you ever re-save this file, keep it UTF-8 with BOM.
#>

$ErrorActionPreference = 'Stop'

function Apply-Edit {
    param(
        [string]$Path,
        [string]$Old,
        [string]$New,
        [string]$Description
    )

    if (-not (Test-Path $Path)) {
        throw "File not found: $Path"
    }

    $content = Get-Content -Path $Path -Raw -Encoding UTF8

    $count = ([regex]::Matches($content, [regex]::Escape($Old))).Count

    if ($count -eq 0) {
        Write-Warning "SKIP  [$Description]  -  anchor not found in $Path (already applied, or file has diverged  -  check manually)."
        return
    }
    if ($count -gt 1) {
        throw "ABORT [$Description]  -  anchor found $count times in $Path, expected exactly 1. Refusing to guess which one."
    }

    $updated = $content.Replace($Old, $New)

    # OneDrive (and sometimes an AV scanner) transiently locks files under a synced Desktop
    # path right after a write. Retry with backoff instead of failing the whole run on a
    # lock that clears itself within a second or two.
    $maxAttempts = 6
    $delayMs = 400
    for ($attempt = 1; $attempt -le $maxAttempts; $attempt++) {
        try {
            Set-Content -Path $Path -Value $updated -NoNewline -Encoding UTF8
            break
        }
        catch [System.IO.IOException] {
            if ($attempt -eq $maxAttempts) {
                throw "ABORT [$Description]  -  $Path stayed locked after $maxAttempts attempts. Close it in Visual Studio (and let OneDrive finish syncing) and re-run  -  edits already applied are skipped automatically."
            }
            Write-Warning "RETRY [$Description]  -  $Path is locked (attempt $attempt/$maxAttempts), waiting..."
            Start-Sleep -Milliseconds $delayMs
            $delayMs = $delayMs * 2
        }
    }

    Write-Host "OK    [$Description]  -  applied to $Path"
}

function New-FileIfMissing {
    param(
        [string]$Path,
        [string]$Content,
        [string]$Description
    )

    if (Test-Path $Path) {
        Write-Warning "SKIP  [$Description]  -  $Path already exists (already applied, or a naming collision  -  check manually)."
        return
    }

    $dir = Split-Path -Path $Path -Parent
    if (-not (Test-Path $dir)) {
        New-Item -ItemType Directory -Path $dir -Force | Out-Null
    }

    $maxAttempts = 6
    $delayMs = 400
    for ($attempt = 1; $attempt -le $maxAttempts; $attempt++) {
        try {
            Set-Content -Path $Path -Value $Content -NoNewline -Encoding UTF8
            break
        }
        catch [System.IO.IOException] {
            if ($attempt -eq $maxAttempts) {
                throw "ABORT [$Description]  -  $Path stayed locked after $maxAttempts attempts."
            }
            Write-Warning "RETRY [$Description]  -  $Path is locked (attempt $attempt/$maxAttempts), waiting..."
            Start-Sleep -Milliseconds $delayMs
            $delayMs = $delayMs * 2
        }
    }

    Write-Host "OK    [$Description]  -  created $Path"
}

# =====================================================================================
# File paths (relative to repo root)
# =====================================================================================

$teacherConfigEntityPath   = Join-Path $PSScriptRoot 'Edvanz.Domain\Entities\TeacherConfiguration.cs'
$teacherConfigDtoPath      = Join-Path $PSScriptRoot 'Edvanz.Application\Dtos\Teacher\TeacherConfigurationDto.cs'
$updateTeacherConfigDtoPath = Join-Path $PSScriptRoot 'Edvanz.Application\Dtos\Teacher\UpdateTeacherConfigurationDto.cs'
$teacherServicePath        = Join-Path $PSScriptRoot 'Edvanz.Application\Services\TeacherService.cs'
$iAttendanceRepoPath       = Join-Path $PSScriptRoot 'Edvanz.Domain\Interfaces\IAttendanceRepo.cs'
$attendanceRepoPath        = Join-Path $PSScriptRoot 'Edvanz.Infrastructure\Repositories\AttendanceRepo.cs'
$iPaymentRepoPath          = Join-Path $PSScriptRoot 'Edvanz.Domain\Interfaces\IPaymentRepo.cs'
$paymentRepoPath           = Join-Path $PSScriptRoot 'Edvanz.Infrastructure\Repositories\PaymentRepo.cs'
$paymentServicePath        = Join-Path $PSScriptRoot 'Edvanz.Application\Services\PaymentService.cs'
$attendanceDtosPath        = Join-Path $PSScriptRoot 'Edvanz.Application\Dtos\Attendance\AttendanceDtos.cs'
$attendanceServicePath     = Join-Path $PSScriptRoot 'Edvanz.Application\Services\AttendanceService.cs'

$newPaymentInfoRowPath     = Join-Path $PSScriptRoot 'Edvanz.Domain\Models\AttendanceScreenPaymentInfoRow.cs'
$newLabelFormatterPath     = Join-Path $PSScriptRoot 'Edvanz.Application\Extensions\PaymentLabelFormatter.cs'

# =====================================================================================
# Edit 1 of 17  -  TeacherConfiguration.cs: add the two new flags to the entity.
# =====================================================================================

$edit1Old = @'
    public bool IsDeviceLockEnabled { get; set; } = false;

    /// <summary>
    /// Timestamp of the last configuration update. Null if never modified after initial creation.
    /// </summary>
    public DateTime? UpdatedAt { get; set; }
'@

$edit1New = @'
    public bool IsDeviceLockEnabled { get; set; } = false;

    // ─── Attendance Screen Enrichment (teacher-facing, distinct from Student/ParentVisibility*) ───

    /// <summary>
    /// Whether the Take/Edit Attendance student list (GET .../sessions/{sessionId}/students)
    /// includes each student's payment/debt snapshot (unpaid-last-month flag, unpaid months
    /// count, outstanding amount, unpaid month labels). Judged through the current cutoff month
    /// per CLAUDE.md §7.4. When false, the payment lookup is skipped entirely (no extra query).
    /// Default: false.
    /// </summary>
    public bool ShowPaymentInfoOnAttendanceScreen { get; set; } = false;

    /// <summary>
    /// Whether the Take/Edit Attendance student list includes each student's course-scoped
    /// absence count (current active StudentSessionAssignment only — distinct from the lifetime
    /// StudentAbsenceCounter.TotalAbsences) and current-calendar-month absence count. Does NOT
    /// gate WasAbsentLastSession/LastAbsenceDate/LastAbsenceSessionName, which stay unconditional
    /// (REQ-ATT-028/029/060 absence-alert warning, unrelated to this display preference).
    /// Default: false.
    /// </summary>
    public bool ShowAttendanceHistoryOnAttendanceScreen { get; set; } = false;

    /// <summary>
    /// Timestamp of the last configuration update. Null if never modified after initial creation.
    /// </summary>
    public DateTime? UpdatedAt { get; set; }
'@

Apply-Edit -Path $teacherConfigEntityPath -Old $edit1Old -New $edit1New `
    -Description '1/17 TeacherConfiguration entity: add the two new flags'

# =====================================================================================
# Edit 2 of 17  -  TeacherConfigurationDto.cs: mirror the two new flags (output DTO).
# =====================================================================================

$edit2Old = @'
    // ─── Device Lock ───
    public bool IsDeviceLockEnabled { get; set; }

    public DateTime? UpdatedAt { get; set; }
}
'@

$edit2New = @'
    // ─── Device Lock ───
    public bool IsDeviceLockEnabled { get; set; }

    // ─── Attendance Screen Enrichment ───
    public bool ShowPaymentInfoOnAttendanceScreen { get; set; }
    public bool ShowAttendanceHistoryOnAttendanceScreen { get; set; }

    public DateTime? UpdatedAt { get; set; }
}
'@

Apply-Edit -Path $teacherConfigDtoPath -Old $edit2Old -New $edit2New `
    -Description '2/17 TeacherConfigurationDto: add the two new flags'

# =====================================================================================
# Edit 3 of 17  -  UpdateTeacherConfigurationDto.cs: mirror the two new flags (input DTO).
# =====================================================================================

$edit3Old = @'
    // ─── Device Lock ───

    /// <summary>
    /// When true, each linked student is bound to the first device they use to open this
    /// teacher and can only open the teacher from that device afterwards (per teacher).
    /// Default: false.
    /// </summary>
    public bool IsDeviceLockEnabled { get; set; } = false;
}
'@

$edit3New = @'
    // ─── Device Lock ───

    /// <summary>
    /// When true, each linked student is bound to the first device they use to open this
    /// teacher and can only open the teacher from that device afterwards (per teacher).
    /// Default: false.
    /// </summary>
    public bool IsDeviceLockEnabled { get; set; } = false;

    // ─── Attendance Screen Enrichment ───

    /// <summary>
    /// Show each student's payment/debt snapshot on the Take/Edit Attendance student list.
    /// Default: false.
    /// </summary>
    public bool ShowPaymentInfoOnAttendanceScreen { get; set; } = false;

    /// <summary>
    /// Show each student's course-scoped and current-month absence counts on the Take/Edit
    /// Attendance student list. Default: false.
    /// </summary>
    public bool ShowAttendanceHistoryOnAttendanceScreen { get; set; } = false;
}
'@

Apply-Edit -Path $updateTeacherConfigDtoPath -Old $edit3Old -New $edit3New `
    -Description '3/17 UpdateTeacherConfigurationDto: add the two new flags'

# =====================================================================================
# Edit 4 of 17  -  TeacherService.cs: explicit defaults at teacher initialization.
# =====================================================================================

$edit4Old = @'
                ParentVisibilityVideo = true,
                CreateAt = DateTime.UtcNow
            };
'@

$edit4New = @'
                ParentVisibilityVideo = true,
                ShowPaymentInfoOnAttendanceScreen = false,
                ShowAttendanceHistoryOnAttendanceScreen = false,
                CreateAt = DateTime.UtcNow
            };
'@

Apply-Edit -Path $teacherServicePath -Old $edit4Old -New $edit4New `
    -Description '4/17 TeacherService: init defaults for the two new flags'

# =====================================================================================
# Edit 5 of 17  -  TeacherService.cs: map the two new flags on save (SaveConfigurationAsync).
# =====================================================================================

$edit5Old = @'
            config.IsDeviceLockEnabled = dto.IsDeviceLockEnabled;
            config.UpdatedAt = DateTime.UtcNow;
'@

$edit5New = @'
            config.IsDeviceLockEnabled = dto.IsDeviceLockEnabled;
            config.ShowPaymentInfoOnAttendanceScreen = dto.ShowPaymentInfoOnAttendanceScreen;
            config.ShowAttendanceHistoryOnAttendanceScreen = dto.ShowAttendanceHistoryOnAttendanceScreen;
            config.UpdatedAt = DateTime.UtcNow;
'@

Apply-Edit -Path $teacherServicePath -Old $edit5Old -New $edit5New `
    -Description '5/17 TeacherService: map the two new flags on save'

# =====================================================================================
# Edit 6 of 17  -  TeacherService.cs: map the two new flags on read (GetConfigurationAsync).
# =====================================================================================

$edit6Old = @'
            IsDeviceLockEnabled = config.IsDeviceLockEnabled,
            UpdatedAt = config.UpdatedAt,
'@

$edit6New = @'
            IsDeviceLockEnabled = config.IsDeviceLockEnabled,
            ShowPaymentInfoOnAttendanceScreen = config.ShowPaymentInfoOnAttendanceScreen,
            ShowAttendanceHistoryOnAttendanceScreen = config.ShowAttendanceHistoryOnAttendanceScreen,
            UpdatedAt = config.UpdatedAt,
'@

Apply-Edit -Path $teacherServicePath -Old $edit6Old -New $edit6New `
    -Description '6/17 TeacherService: map the two new flags on read'

# =====================================================================================
# Edit 7 of 17  -  IAttendanceRepo.cs: add GetAttendanceHistoryCountsBatchAsync signature.
# =====================================================================================

$edit7Old = @'
    Task<IReadOnlyList<SessionMonthStudentCounts>> GetSessionMonthAttendanceCountsAsync(
        long sessionId, DateTime monthStart, DateTime monthEndExclusive,
        IReadOnlyCollection<long> teacherStudentIds);
}
'@

$edit7New = @'
    Task<IReadOnlyList<SessionMonthStudentCounts>> GetSessionMonthAttendanceCountsAsync(
        long sessionId, DateTime monthStart, DateTime monthEndExclusive,
        IReadOnlyCollection<long> teacherStudentIds);

    // ══════════════════════════════════════════════
    // ATTENDANCE SCREEN HISTORY ENRICHMENT (ShowAttendanceHistoryOnAttendanceScreen)
    // ══════════════════════════════════════════════

    /// <summary>
    /// Batched, per-student attendance-history snapshot for the Take/Edit Attendance list's
    /// optional history enrichment — bounded to the caller's page of
    /// <paramref name="teacherStudentIds"/>, never the full roster.
    ///
    /// <see cref="AttendanceHistoryCountsRow.CourseAbsences"/> is scoped strictly to the
    /// student's CURRENT active <c>StudentSessionAssignment</c> — distinct from
    /// <c>StudentAbsenceCounter.TotalAbsences</c>, which is lifetime across every session the
    /// student has ever been assigned to (BR-ATT-004) and is never used here. A student with no
    /// currently active assignment is simply absent from the returned dictionary — the caller
    /// defaults such rows to zero.
    ///
    /// <see cref="AttendanceHistoryCountsRow.CurrentMonthAbsences"/> counts Absent records across
    /// ALL of the student's sessions (not just their current one) in
    /// [<paramref name="monthStart"/>, <paramref name="monthEndExclusive"/>), matching the
    /// teacher-local-month convention used elsewhere (<c>ITimeZoneService.GetTeacherLocalDate</c>).
    ///
    /// Deliberately does NOT cover WasAbsentLastSession/LastAbsenceDate/LastAbsenceSessionName —
    /// those stay unconditional on <see cref="PagedAttendanceStudentRow"/> (REQ-ATT-028/029/060).
    /// </summary>
    Task<Dictionary<long, AttendanceHistoryCountsRow>> GetAttendanceHistoryCountsBatchAsync(
        long teacherId, IReadOnlyCollection<long> teacherStudentIds,
        DateTime monthStart, DateTime monthEndExclusive);
}
'@

Apply-Edit -Path $iAttendanceRepoPath -Old $edit7Old -New $edit7New `
    -Description '7/17 IAttendanceRepo: add GetAttendanceHistoryCountsBatchAsync signature'

# =====================================================================================
# Edit 8 of 17  -  IAttendanceRepo.cs: add the AttendanceHistoryCountsRow projection type.
# =====================================================================================

$edit8Old = @'
public class PagedAttendanceStudentRow
{
    public long TeacherStudentId { get; set; }
    public string StudentName { get; set; } = null!;
    public string StudentCode { get; set; } = null!;
    public long? SessionId { get; set; }
    public string? SessionName { get; set; }
    public bool IsFromLinkedSession { get; set; }
    public string? SourceSessionName { get; set; }
    public bool IsMarked { get; set; }
    public AttendanceStatus? CurrentStatus { get; set; }
    public int ConsecutiveAbsences { get; set; }
    public int TotalAbsences { get; set; }
    public DateTime? LastAbsenceDate { get; set; }
    public string? LastAbsenceSessionName { get; set; }
}
'@

$edit8New = @'
public class PagedAttendanceStudentRow
{
    public long TeacherStudentId { get; set; }
    public string StudentName { get; set; } = null!;
    public string StudentCode { get; set; } = null!;
    public long? SessionId { get; set; }
    public string? SessionName { get; set; }
    public bool IsFromLinkedSession { get; set; }
    public string? SourceSessionName { get; set; }
    public bool IsMarked { get; set; }
    public AttendanceStatus? CurrentStatus { get; set; }
    public int ConsecutiveAbsences { get; set; }
    public int TotalAbsences { get; set; }
    public DateTime? LastAbsenceDate { get; set; }
    public string? LastAbsenceSessionName { get; set; }
}

/// <summary>
/// Per-student course-scoped and current-month absence counts (query projection) for the
/// Attendance student-list screen's optional history enrichment
/// (<c>ShowAttendanceHistoryOnAttendanceScreen</c>). See
/// <see cref="IAttendanceRepo.GetAttendanceHistoryCountsBatchAsync"/> for scope details.
/// </summary>
public class AttendanceHistoryCountsRow
{
    public long TeacherStudentId { get; set; }
    public int CourseAbsences { get; set; }
    public int CurrentMonthAbsences { get; set; }
}
'@

Apply-Edit -Path $iAttendanceRepoPath -Old $edit8Old -New $edit8New `
    -Description '8/17 IAttendanceRepo: add AttendanceHistoryCountsRow projection'

# =====================================================================================
# Edit 9 of 17  -  AttendanceRepo.cs: implement GetAttendanceHistoryCountsBatchAsync.
# =====================================================================================

$edit9Old = @'
            .ToListAsync();
    }

    // ══════════════════════════════════════════════
    // V2 AUDIT FIX — NEW BATCH METHODS
    // ══════════════════════════════════════════════
'@

$edit9New = @'
            .ToListAsync();
    }

    // ══════════════════════════════════════════════
    // ATTENDANCE SCREEN HISTORY ENRICHMENT (ShowAttendanceHistoryOnAttendanceScreen)
    // ══════════════════════════════════════════════

    /// <inheritdoc />
    public async Task<Dictionary<long, AttendanceHistoryCountsRow>> GetAttendanceHistoryCountsBatchAsync(
        long teacherId, IReadOnlyCollection<long> teacherStudentIds,
        DateTime monthStart, DateTime monthEndExclusive)
    {
        var idList = teacherStudentIds.Distinct().ToList();
        if (idList.Count == 0)
            return new Dictionary<long, AttendanceHistoryCountsRow>();

        // Course-scoped absences: strictly the student's CURRENT active assignment (confirmed
        // scope — Option A), NOT StudentAbsenceCounter.TotalAbsences, which is lifetime across
        // every session ever assigned (BR-ATT-004). Every AttendanceRecord is written with the
        // originating StudentSessionAssignmentId, so this is a direct, precise correlated count.
        // A student can have at most one active assignment (GetActiveAssignmentAsync contract),
        // so TeacherStudentId is unique in the result — safe direct ToDictionary below.
        var rows = await _context.StudentSessionAssignments
            .Where(a => a.IsActive && a.TeacherId == teacherId
                && a.TeacherStudentId.HasValue && idList.Contains(a.TeacherStudentId.Value))
            .Select(a => new AttendanceHistoryCountsRow
            {
                TeacherStudentId = a.TeacherStudentId!.Value,
                CourseAbsences = _context.AttendanceRecords.Count(r =>
                    r.StudentSessionAssignmentId == a.Id && r.Status == AttendanceStatus.Absent),
                // Current-month count is NOT scoped to this assignment/session — it counts the
                // student's absences across whichever session(s) they were marked in that month,
                // matching the teacher-local-month convention used elsewhere in this codebase.
                CurrentMonthAbsences = _context.AttendanceRecords.Count(r =>
                    r.TeacherStudentId == a.TeacherStudentId
                    && r.Status == AttendanceStatus.Absent
                    && r.OccurrenceDate >= monthStart && r.OccurrenceDate < monthEndExclusive)
            })
            .AsNoTracking()
            .ToListAsync();

        return rows.ToDictionary(r => r.TeacherStudentId);
    }

    // ══════════════════════════════════════════════
    // V2 AUDIT FIX — NEW BATCH METHODS
    // ══════════════════════════════════════════════
'@

Apply-Edit -Path $attendanceRepoPath -Old $edit9Old -New $edit9New `
    -Description '9/17 AttendanceRepo: implement GetAttendanceHistoryCountsBatchAsync'

# =====================================================================================
# Edit 10 of 17  -  IPaymentRepo.cs: add GetPaymentInfoForAttendanceBatchAsync signature.
# =====================================================================================

$edit10Old = @'
    Task<CollectLookupRow?> ResolveCollectLookupAsync(
        long teacherId, string? qr, string? code, string? name, DateTime throughMonthEnd);

    // ══════════════════════════════════════════════
    // ASSISTANT WALLET QUERIES
    // ══════════════════════════════════════════════
'@

$edit10New = @'
    Task<CollectLookupRow?> ResolveCollectLookupAsync(
        long teacherId, string? qr, string? code, string? name, DateTime throughMonthEnd);

    // ══════════════════════════════════════════════
    // ATTENDANCE SCREEN PAYMENT ENRICHMENT (ShowPaymentInfoOnAttendanceScreen)
    // ══════════════════════════════════════════════

    /// <summary>
    /// Batched, per-student payment/debt snapshot for the Take/Edit Attendance list's optional
    /// payment enrichment — bounded to the caller's page of <paramref name="teacherStudentIds"/>,
    /// never the full roster. Reuses the exact same arrears rules as the Unpaid Students Overview
    /// (<see cref="UnpaidStudentRow"/>) per CLAUDE.md §7.4: every figure is judged only through
    /// <paramref name="throughMonthEnd"/> (<c>PeriodStart &lt;= throughMonthEnd</c>), never off the
    /// all-time <c>StudentPaymentCounter</c>, since periods are pre-generated to the session end
    /// and would otherwise report un-owed future months as debt.
    ///
    /// <see cref="AttendanceScreenPaymentInfoRow.HasUnpaidLastMonth"/> is a direct existence check
    /// against a Monthly period whose <c>PeriodStart</c> falls in
    /// [<paramref name="lastMonthStart"/>, <paramref name="lastMonthEnd"/>] and is not fully paid —
    /// not inferred from the unpaid-tail count, so it stays correct independent of the oldest-first
    /// collection assumption (BR-PAY-006).
    ///
    /// A student with no unpaid periods through the cutoff is simply absent from the returned
    /// dictionary — the caller defaults such rows to zeroed/false values (fully paid), never null.
    /// </summary>
    Task<Dictionary<long, AttendanceScreenPaymentInfoRow>> GetPaymentInfoForAttendanceBatchAsync(
        long teacherId, IReadOnlyCollection<long> teacherStudentIds,
        DateTime throughMonthEnd, DateTime lastMonthStart, DateTime lastMonthEnd);

    // ══════════════════════════════════════════════
    // ASSISTANT WALLET QUERIES
    // ══════════════════════════════════════════════
'@

Apply-Edit -Path $iPaymentRepoPath -Old $edit10Old -New $edit10New `
    -Description '10/17 IPaymentRepo: add GetPaymentInfoForAttendanceBatchAsync signature'

# =====================================================================================
# Edit 11 of 17 (new file)  -  AttendanceScreenPaymentInfoRow.cs projection type.
# =====================================================================================

$newPaymentInfoRowContent = @'
namespace Edvanz.Domain.Interfaces;

// ════════════════════════════════════════════════════════════════════════════
// ATTENDANCE MODULE (MODULE 3) × PAYMENT MODULE (MODULE 4) — ATTENDANCE-SCREEN
// PAYMENT ENRICHMENT PROJECTION
// ════════════════════════════════════════════════════════════════════════════
//
// Same convention as PaymentRepoProjections.cs / UnpaidStudentRow.cs: projections
// live in the Domain layer alongside the repo interface so the Application
// service maps them to client-facing DTOs without the repo knowing about
// Application DTO types.
//
// Kept in a dedicated file rather than appended to PaymentRepoProjections.cs so
// the change is additive and cannot conflict with edits to that file.
// ════════════════════════════════════════════════════════════════════════════

/// <summary>
/// Per-student payment/debt snapshot (query projection) for the Attendance student-list
/// screen's optional payment enrichment (<c>ShowPaymentInfoOnAttendanceScreen</c>). Reuses
/// <see cref="UnpaidPeriodRef"/> and the same CLAUDE.md §7.4 arrears rules as
/// <see cref="UnpaidStudentRow"/> — see
/// <see cref="IPaymentRepo.GetPaymentInfoForAttendanceBatchAsync"/> for scope details.
/// </summary>
public sealed class AttendanceScreenPaymentInfoRow
{
    public long TeacherStudentId { get; set; }

    /// <summary>
    /// True when a Monthly <c>PaymentPeriod</c> starting in the calendar month immediately
    /// before the teacher's current local month exists and is not fully paid.
    /// </summary>
    public bool HasUnpaidLastMonth { get; set; }

    /// <summary>
    /// Count of unpaid periods through the current month cutoff. Contiguous tail (BR-PAY-006),
    /// same semantics as <see cref="UnpaidStudentRow.UnpaidPeriodCount"/>.
    /// </summary>
    public int UnpaidMonthsCount { get; set; }

    /// <summary>Sum of <c>(AmountDue - AmountPaid)</c> over those periods.</summary>
    public decimal UnpaidAmount { get; set; }

    /// <summary>
    /// The individual unpaid periods behind <see cref="UnpaidMonthsCount"/>, earliest first.
    /// Dates only — display formatting/localization belongs to the Application layer
    /// (<c>PaymentLabelFormatter.FormatUnpaidPeriodLabel</c>).
    /// </summary>
    public IReadOnlyList<UnpaidPeriodRef> UnpaidPeriods { get; set; } = Array.Empty<UnpaidPeriodRef>();
}
'@

New-FileIfMissing -Path $newPaymentInfoRowPath -Content $newPaymentInfoRowContent `
    -Description '11/17 New file: AttendanceScreenPaymentInfoRow.cs'

# =====================================================================================
# Edit 12 of 17  -  PaymentRepo.cs: implement GetPaymentInfoForAttendanceBatchAsync.
# =====================================================================================

$edit12Old = @'
            return new CollectLookupRow
            {
                TeacherStudentId = student.Id,
                StudentName = student.StudentName,
                StudentCode = student.StudentCode,
                Group = student.Group,
                AmountDue = overdueTotal,
                IsUnpaid = overdueTotal > 0m
            };
        }

        // ----------------------------------------------
        // ASSISTANT WALLET QUERIES
        // ----------------------------------------------
'@

$edit12New = @'
            return new CollectLookupRow
            {
                TeacherStudentId = student.Id,
                StudentName = student.StudentName,
                StudentCode = student.StudentCode,
                Group = student.Group,
                AmountDue = overdueTotal,
                IsUnpaid = overdueTotal > 0m
            };
        }

        // ----------------------------------------------
        // ATTENDANCE SCREEN PAYMENT ENRICHMENT (ShowPaymentInfoOnAttendanceScreen)
        // ----------------------------------------------

        /// <inheritdoc />
        public async Task<Dictionary<long, AttendanceScreenPaymentInfoRow>> GetPaymentInfoForAttendanceBatchAsync(
            long teacherId, IReadOnlyCollection<long> teacherStudentIds,
            DateTime throughMonthEnd, DateTime lastMonthStart, DateTime lastMonthEnd)
        {
            var idList = teacherStudentIds.Distinct().ToList();
            if (idList.Count == 0)
                return new Dictionary<long, AttendanceScreenPaymentInfoRow>();

            // Same cutoff rule as GetUnpaidStudentsPagedAsync (CLAUDE.md §7.4): judge arrears only
            // through the cutoff month so pre-generated future periods are never counted as owed.
            var periodRefs = await _context.PaymentPeriods
                .Where(p => p.TeacherId == teacherId
                    && p.TeacherStudentId != null
                    && idList.Contains(p.TeacherStudentId!.Value)
                    && p.PaymentStatus != PaymentStatus.Paid
                    && p.PeriodStart <= throughMonthEnd)
                .OrderBy(p => p.PeriodSequence)
                .Select(p => new
                {
                    StudentId = p.TeacherStudentId!.Value,
                    Ref = new UnpaidPeriodRef
                    {
                        PeriodType = p.PeriodType,
                        PeriodStart = p.PeriodStart,
                        PeriodEnd = p.PeriodEnd,
                        AmountRemaining = p.AmountDue - p.AmountPaid
                    }
                })
                .AsNoTracking()
                .ToListAsync();

            // Direct existence check against exactly last calendar month's period window — not
            // derived from the unpaid-tail count — so it stays correct independent of the
            // oldest-first collection assumption (BR-PAY-006).
            var unpaidLastMonthStudentIds = (await _context.PaymentPeriods
                .Where(p => p.TeacherId == teacherId
                    && p.TeacherStudentId != null
                    && idList.Contains(p.TeacherStudentId!.Value)
                    && p.PaymentStatus != PaymentStatus.Paid
                    && p.PeriodStart >= lastMonthStart && p.PeriodStart <= lastMonthEnd)
                .Select(p => p.TeacherStudentId!.Value)
                .Distinct()
                .ToListAsync())
                .ToHashSet();

            var result = new Dictionary<long, AttendanceScreenPaymentInfoRow>();
            foreach (var group in periodRefs.GroupBy(x => x.StudentId))
            {
                var refs = group.Select(x => x.Ref).ToList();
                result[group.Key] = new AttendanceScreenPaymentInfoRow
                {
                    TeacherStudentId = group.Key,
                    HasUnpaidLastMonth = unpaidLastMonthStudentIds.Contains(group.Key),
                    UnpaidMonthsCount = refs.Count,
                    UnpaidAmount = refs.Sum(r => r.AmountRemaining),
                    UnpaidPeriods = refs
                };
            }

            return result;
        }

        // ----------------------------------------------
        // ASSISTANT WALLET QUERIES
        // ----------------------------------------------
'@

Apply-Edit -Path $paymentRepoPath -Old $edit12Old -New $edit12New `
    -Description '12/17 PaymentRepo: implement GetPaymentInfoForAttendanceBatchAsync'

# =====================================================================================
# Edit 13 of 17 (new file)  -  PaymentLabelFormatter.cs shared helper.
# =====================================================================================

$newLabelFormatterContent = @'
using Edvanz.Domain.Enums;
using Edvanz.Domain.Interfaces;

namespace Edvanz.Application.Extensions;

/// <summary>
/// Shared display-label formatting for unpaid billing periods. Extracted from
/// <c>PaymentService</c> (originally a private static method there) so the Attendance module's
/// payment-info enrichment (<c>ShowPaymentInfoOnAttendanceScreen</c>) can reuse the exact same
/// label rules as the Unpaid Students Overview instead of duplicating them.
/// </summary>
public static class PaymentLabelFormatter
{
    /// <summary>
    /// Display label for one unpaid period: the calendar month for a Monthly obligation, the
    /// occurrence date for a PerSession one.
    /// </summary>
    public static string FormatUnpaidPeriodLabel(UnpaidPeriodRef period) =>
        period.PeriodType == PeriodType.Monthly
            ? period.PeriodStart.ToString("MMMM yyyy")
            : period.PeriodStart.ToString("yyyy-MM-dd");
}
'@

New-FileIfMissing -Path $newLabelFormatterPath -Content $newLabelFormatterContent `
    -Description '13/17 New file: PaymentLabelFormatter.cs'

# =====================================================================================
# Edit 14 of 17  -  PaymentService.cs: add using for the extracted formatter.
# =====================================================================================

$edit14Old = @'
using Edvanz.Application.Dtos;
using Edvanz.Application.Dtos.Payment;
using Edvanz.Application.IservicesContract;
using Edvanz.Application.ServiceContract;
using Edvanz.Domain.Constants;
using Edvanz.Domain.Entities;
using Edvanz.Domain.Enums;
using Edvanz.Domain.Interfaces;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using System.Net;
'@

$edit14New = @'
using Edvanz.Application.Dtos;
using Edvanz.Application.Dtos.Payment;
using Edvanz.Application.Extensions;
using Edvanz.Application.IservicesContract;
using Edvanz.Application.ServiceContract;
using Edvanz.Domain.Constants;
using Edvanz.Domain.Entities;
using Edvanz.Domain.Enums;
using Edvanz.Domain.Interfaces;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using System.Net;
'@

Apply-Edit -Path $paymentServicePath -Old $edit14Old -New $edit14New `
    -Description '14/17 PaymentService: add using Edvanz.Application.Extensions'

# =====================================================================================
# Edit 15 of 17  -  PaymentService.cs: point the call site at the shared formatter.
# =====================================================================================

$edit15Old = @'
                UnpaidPeriodLabels = row.UnpaidPeriods.Select(FormatUnpaidPeriodLabel).ToList()
'@

$edit15New = @'
                UnpaidPeriodLabels = row.UnpaidPeriods.Select(PaymentLabelFormatter.FormatUnpaidPeriodLabel).ToList()
'@

Apply-Edit -Path $paymentServicePath -Old $edit15Old -New $edit15New `
    -Description '15/17 PaymentService: use PaymentLabelFormatter at the call site'

# =====================================================================================
# Edit 16 of 17  -  PaymentService.cs: remove the now-duplicate private formatter.
# =====================================================================================

$edit16Old = @'
        monthEnd = new DateTime(year, month, 1).AddMonths(1).AddDays(-1);
        return true;
    }

    /// <summary>
    /// Display label for one unpaid period: the calendar month for a Monthly obligation, the
    /// occurrence date for a PerSession one.
    /// </summary>
    private static string FormatUnpaidPeriodLabel(UnpaidPeriodRef period) =>
        period.PeriodType == PeriodType.Monthly
            ? period.PeriodStart.ToString("MMMM yyyy")
            : period.PeriodStart.ToString("yyyy-MM-dd");

'@

$edit16New = @'
        monthEnd = new DateTime(year, month, 1).AddMonths(1).AddDays(-1);
        return true;
    }

'@

Apply-Edit -Path $paymentServicePath -Old $edit16Old -New $edit16New `
    -Description '16/17 PaymentService: remove dead private FormatUnpaidPeriodLabel'

# =====================================================================================
# Edit 17 of 17  -  AttendanceDtos.cs: extend AttendanceStudentRowDto + add 2 new DTOs.
# =====================================================================================

$edit17Old = @'
    /// <summary>
    /// Session name where the student's most recent absence occurred, or null if none. REQ-ATT-060.
    /// From <c>StudentAbsenceCounter.LastAbsenceSessionName</c>.
    /// </summary>
    public string? LastAbsenceSessionName { get; set; }
}
'@

$edit17New = @'
    /// <summary>
    /// Session name where the student's most recent absence occurred, or null if none. REQ-ATT-060.
    /// From <c>StudentAbsenceCounter.LastAbsenceSessionName</c>.
    /// </summary>
    public string? LastAbsenceSessionName { get; set; }

    /// <summary>
    /// Payment/debt snapshot, present only when the teacher has
    /// <c>ShowPaymentInfoOnAttendanceScreen</c> enabled. Null means "not shown" per the teacher's
    /// configuration — NOT "no debt" (a fully-paid student still gets a populated, zeroed object).
    /// </summary>
    public StudentPaymentInfoDto? PaymentInfo { get; set; }

    /// <summary>
    /// Course-scoped and current-month absence counts, present only when the teacher has
    /// <c>ShowAttendanceHistoryOnAttendanceScreen</c> enabled. Deliberately separate from
    /// <see cref="WasAbsentLastSession"/>/<see cref="LastAbsenceDate"/>/<see cref="LastAbsenceSessionName"/>
    /// above, which stay unconditional (REQ-ATT-028/029/060).
    /// </summary>
    public StudentAttendanceHistoryInfoDto? HistoryInfo { get; set; }
}

/// <summary>
/// Payment/debt snapshot for one student on the Attendance student-list screen
/// (<c>ShowPaymentInfoOnAttendanceScreen</c>). Every figure is judged through the current
/// teacher-local month cutoff (CLAUDE.md §7.4) — see
/// <see cref="Edvanz.Domain.Interfaces.AttendanceScreenPaymentInfoRow"/>.
/// </summary>
public class StudentPaymentInfoDto
{
    /// <summary>True when the calendar month immediately before the teacher's current local
    /// month has an unpaid Monthly obligation.</summary>
    public bool HasUnpaidLastMonth { get; set; }

    /// <summary>Count of unpaid periods through the current month cutoff.</summary>
    public int UnpaidMonthsCount { get; set; }

    /// <summary>Sum of (AmountDue - AmountPaid) over those periods.</summary>
    public decimal UnpaidAmount { get; set; }

    /// <summary>Display labels for each unpaid month/period, earliest first — e.g.
    /// ["July 2026", "August 2026"]. Formatted via <c>PaymentLabelFormatter</c>.</summary>
    public List<string> UnpaidMonthLabels { get; set; } = new();
}

/// <summary>
/// Course-scoped and current-month absence counts for one student on the Attendance
/// student-list screen (<c>ShowAttendanceHistoryOnAttendanceScreen</c>).
/// </summary>
public class StudentAttendanceHistoryInfoDto
{
    /// <summary>Absences within the student's CURRENT active session assignment only —
    /// distinct from the lifetime <c>TotalAbsences</c> above (BR-ATT-004).</summary>
    public int CourseAbsences { get; set; }

    /// <summary>Absences within the teacher's current local calendar month.</summary>
    public int CurrentMonthAbsences { get; set; }
}
'@

Apply-Edit -Path $attendanceDtosPath -Old $edit17Old -New $edit17New `
    -Description '17/17a AttendanceDtos: extend row DTO + add StudentPaymentInfoDto/StudentAttendanceHistoryInfoDto'

# =====================================================================================
# Edit 18  -  AttendanceService.cs: add using for the extracted formatter.
# =====================================================================================

$edit18Old = @'
using Edvanz.Application.Dtos;
using Edvanz.Application.Dtos.Attendance;
using Edvanz.Application.IservicesContract;
using Edvanz.Application.ServiceContract;
using Edvanz.Domain.Constants;
using Edvanz.Domain.Entities;
using Edvanz.Domain.Enums;
using Edvanz.Domain.Interfaces;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using System.Net;
'@

$edit18New = @'
using Edvanz.Application.Dtos;
using Edvanz.Application.Dtos.Attendance;
using Edvanz.Application.Extensions;
using Edvanz.Application.IservicesContract;
using Edvanz.Application.ServiceContract;
using Edvanz.Domain.Constants;
using Edvanz.Domain.Entities;
using Edvanz.Domain.Enums;
using Edvanz.Domain.Interfaces;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using System.Net;
'@

Apply-Edit -Path $attendanceServicePath -Old $edit18Old -New $edit18New `
    -Description '18/19 AttendanceService: add using Edvanz.Application.Extensions'

# =====================================================================================
# Edit 19  -  AttendanceService.cs: wire the optional enrichment into
#             GetAttendanceStudentListAsync.
# =====================================================================================

$edit19Old = @'
        var session = await _unitOfWork.SessionsRepo.GetByIdAndTeacherAsync(sessionId, teacherId);
        if (session is null)
            return Result<AttendanceStudentListDto>.Failure(
                _localizer, AttendanceConstants.Messages.SessionNotFound, HttpStatusCode.NotFound);

        var date = occurrenceDate?.Date ?? _timeZoneService.GetTeacherLocalDate(teacherId);

        var linkedSessions = await _unitOfWork.SessionsRepo.GetLinkedSessionsAsync(sessionId);
        var linkedSessionIds = linkedSessions.Select(s => s.Id).ToList();

        var (items, totalCount, assignedCount, notAssignedCount, holdCount) =
            await _unitOfWork.AttendanceRepo.GetPagedAttendanceStudentListAsync(
                teacherId, sessionId, date, linkedSessionIds,
                request.Search, request.UnmarkedOnly,
                request.Page, request.PageSize);

        var dtos = items.Select(row => new AttendanceStudentRowDto
        {
            TeacherStudentId = row.TeacherStudentId,
            StudentName = row.StudentName,
            StudentCode = row.StudentCode,
            Barcode = row.StudentCode, // FIX L7: REQ-ATT-009 — barcode encodes the student's unique code
            CurrentStatus = row.CurrentStatus,
            IsMarked = row.IsMarked,
            IsHeld = row.CurrentStatus == AttendanceStatus.Held, // Step 3.1: Held indicator
            IsCrossSessionStudent = row.IsFromLinkedSession,
            SourceSessionName = row.SourceSessionName,
            ConsecutiveAbsences = row.ConsecutiveAbsences,
            TotalAbsences = row.TotalAbsences,
            // "Was absent last session" warning, straight from the counter — same source
            // MarkAttendanceResultDto uses (REQ-ATT-028/029/060), so no second lookup is needed.
            WasAbsentLastSession = row.ConsecutiveAbsences > 0,
            LastAbsenceDate = row.LastAbsenceDate,
            LastAbsenceSessionName = row.LastAbsenceSessionName
        }).ToList();
'@

$edit19New = @'
        var session = await _unitOfWork.SessionsRepo.GetByIdAndTeacherAsync(sessionId, teacherId);
        if (session is null)
            return Result<AttendanceStudentListDto>.Failure(
                _localizer, AttendanceConstants.Messages.SessionNotFound, HttpStatusCode.NotFound);

        var date = occurrenceDate?.Date ?? _timeZoneService.GetTeacherLocalDate(teacherId);

        var linkedSessions = await _unitOfWork.SessionsRepo.GetLinkedSessionsAsync(sessionId);
        var linkedSessionIds = linkedSessions.Select(s => s.Id).ToList();

        var (items, totalCount, assignedCount, notAssignedCount, holdCount) =
            await _unitOfWork.AttendanceRepo.GetPagedAttendanceStudentListAsync(
                teacherId, sessionId, date, linkedSessionIds,
                request.Search, request.UnmarkedOnly,
                request.Page, request.PageSize);

        // Optional per-teacher enrichment (ShowPaymentInfoOnAttendanceScreen /
        // ShowAttendanceHistoryOnAttendanceScreen). Read once; a missing config row (should not
        // happen post-initialization, but defensively) is treated as both disabled — the safest
        // default, matching every other config-gated feature in this codebase. When a flag is
        // off, the corresponding batch repo call below is skipped entirely — no extra query.
        var config = await _unitOfWork.Users.GetConfigurationByTeacherIdAsync(teacherId);
        bool showPaymentInfo = config?.ShowPaymentInfoOnAttendanceScreen ?? false;
        bool showAttendanceHistory = config?.ShowAttendanceHistoryOnAttendanceScreen ?? false;

        var teacherStudentIds = items.Select(r => r.TeacherStudentId).ToList();

        // "Current month" here is always the teacher's ACTUAL current local calendar day
        // (today) — not the occurrence date being viewed. These two cards summarize the
        // student's present-day debt/attendance standing regardless of which past/future
        // occurrence the roster happens to be scoped to.
        Dictionary<long, AttendanceScreenPaymentInfoRow>? paymentInfoMap = null;
        if (showPaymentInfo && teacherStudentIds.Count > 0)
        {
            var today = _timeZoneService.GetTeacherLocalDate(teacherId);
            var currentMonthStart = new DateTime(today.Year, today.Month, 1);
            var currentMonthEnd = currentMonthStart.AddMonths(1).AddDays(-1);
            var lastMonthStart = currentMonthStart.AddMonths(-1);
            var lastMonthEnd = currentMonthStart.AddDays(-1);

            paymentInfoMap = await _unitOfWork.PaymentsRepo.GetPaymentInfoForAttendanceBatchAsync(
                teacherId, teacherStudentIds, currentMonthEnd, lastMonthStart, lastMonthEnd);
        }

        Dictionary<long, AttendanceHistoryCountsRow>? historyInfoMap = null;
        if (showAttendanceHistory && teacherStudentIds.Count > 0)
        {
            var today = _timeZoneService.GetTeacherLocalDate(teacherId);
            var currentMonthStart = new DateTime(today.Year, today.Month, 1);
            var currentMonthEndExclusive = currentMonthStart.AddMonths(1);

            historyInfoMap = await _unitOfWork.AttendanceRepo.GetAttendanceHistoryCountsBatchAsync(
                teacherId, teacherStudentIds, currentMonthStart, currentMonthEndExclusive);
        }

        var dtos = items.Select(row =>
        {
            var dto = new AttendanceStudentRowDto
            {
                TeacherStudentId = row.TeacherStudentId,
                StudentName = row.StudentName,
                StudentCode = row.StudentCode,
                Barcode = row.StudentCode, // FIX L7: REQ-ATT-009 — barcode encodes the student's unique code
                CurrentStatus = row.CurrentStatus,
                IsMarked = row.IsMarked,
                IsHeld = row.CurrentStatus == AttendanceStatus.Held, // Step 3.1: Held indicator
                IsCrossSessionStudent = row.IsFromLinkedSession,
                SourceSessionName = row.SourceSessionName,
                ConsecutiveAbsences = row.ConsecutiveAbsences,
                TotalAbsences = row.TotalAbsences,
                // "Was absent last session" warning, straight from the counter — same source
                // MarkAttendanceResultDto uses (REQ-ATT-028/029/060), so no second lookup is needed.
                // Deliberately NOT gated by ShowAttendanceHistoryOnAttendanceScreen — this is the
                // pre-existing absence-alert warning, unrelated to the new display preference.
                WasAbsentLastSession = row.ConsecutiveAbsences > 0,
                LastAbsenceDate = row.LastAbsenceDate,
                LastAbsenceSessionName = row.LastAbsenceSessionName
            };

            if (showPaymentInfo)
            {
                var pay = paymentInfoMap != null && paymentInfoMap.TryGetValue(row.TeacherStudentId, out var p)
                    ? p : null;
                // Populated-but-zeroed when a student has no unpaid periods (fully paid) — NEVER
                // left null here, since null on the row means "config disabled", not "no debt".
                dto.PaymentInfo = new StudentPaymentInfoDto
                {
                    HasUnpaidLastMonth = pay?.HasUnpaidLastMonth ?? false,
                    UnpaidMonthsCount = pay?.UnpaidMonthsCount ?? 0,
                    UnpaidAmount = pay?.UnpaidAmount ?? 0m,
                    UnpaidMonthLabels = pay?.UnpaidPeriods
                        .Select(PaymentLabelFormatter.FormatUnpaidPeriodLabel).ToList()
                        ?? new List<string>()
                };
            }

            if (showAttendanceHistory)
            {
                var hist = historyInfoMap != null && historyInfoMap.TryGetValue(row.TeacherStudentId, out var h)
                    ? h : null;
                dto.HistoryInfo = new StudentAttendanceHistoryInfoDto
                {
                    CourseAbsences = hist?.CourseAbsences ?? 0,
                    CurrentMonthAbsences = hist?.CurrentMonthAbsences ?? 0
                };
            }

            return dto;
        }).ToList();
'@

Apply-Edit -Path $attendanceServicePath -Old $edit19Old -New $edit19New `
    -Description '19/19 AttendanceService: wire the optional enrichment into GetAttendanceStudentListAsync'

Write-Host ''
Write-Host 'Done. Next steps:'
Write-Host '  1. dotnet build  -  build it locally to confirm it compiles before trusting it.'
Write-Host '  2. Run the migration from the repo root:'
Write-Host '       dotnet ef migrations add AddAttendanceScreenVisibilityFlags --project Edvanz.Infrastructure --startup-project Edvanz.API'
Write-Host '     Review the generated migration (two non-nullable bit columns, default 0) before applying it.'
Write-Host '  3. Review AttendanceService.GetAttendanceStudentListAsync and PaymentRepo/AttendanceRepo new methods against the diff.'
Write-Host '  4. Update the Postman collection / OpenAPI docs for the two new response fields (paymentInfo/historyInfo) and the two new configuration fields if you keep those in sync manually.'
Write-Host '  5. Still open per the confirmed plan: (a) whether to also gate the pre-existing WasAbsentLastSession/LastAbsenceDate/LastAbsenceSessionName fields behind ShowAttendanceHistoryOnAttendanceScreen — currently left unconditional per REQ-ATT-028/029/060; (b) resx/localization keys if you want any new user-facing strings beyond the existing ones (none were needed for this change).'
