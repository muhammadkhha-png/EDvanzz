<#
.SYNOPSIS
    Attendance enrichment - fix-up batch 2: missing interface declaration + both
    method wirings + Endpoint 6B (occurrences/{date}/students) extension.

.DESCRIPTION
    The first script (apply-attendance-screen-enrichment.ps1) landed the DTO fields,
    TeacherConfiguration flags, the projection types, and the CONCRETE repo method
    bodies - but two things never made it in, which is exactly why the API kept
    returning paymentInfo/historyInfo as null even with both config flags on:

      1. IPaymentRepo.cs never got GetPaymentInfoForAttendanceBatchAsync DECLARED on
         the interface (only PaymentRepo.cs, the concrete class, had it). Any call
         through _unitOfWork.PaymentsRepo (interface-typed) would not compile - which
         is almost certainly why the wiring in AttendanceService.cs was attempted and
         then reverted rather than fixed at the source.
      2. GetAttendanceStudentListAsync (Endpoint 3, .../sessions/{id}/students) never
         got the actual enrichment wiring - it was still building AttendanceStudentRowDto
         with no reference to config, PaymentInfo, or HistoryInfo at all.

    This batch also extends a second endpoint per your request:

      3. AttendanceRecordDto gets the same two nullable fields (PaymentInfo/HistoryInfo)
         added, additively. It's shared across ~49 call sites (mark/bulk-mark/edit/sync/
         reports) - those all keep getting null for these two fields, which is correct;
         only GetOccurrenceStudentsAsync sets them.
      4. GetOccurrenceStudentsAsync (Endpoint 6B, .../sessions/{id}/occurrences/{date}/students)
         gets the identical config-gated batch-enrichment pattern as Endpoint 3, so the
         Edit Attendance past-date view carries the same payment/history cards.

.NOTES
    Run from the repo root. Idempotent - if an anchor is already gone (already applied),
    that step is skipped with a warning, not a failure. Uses the CRLF-safe Apply-Edit
    (normalizes line endings before comparing, restores the file's original style on
    write) - the previous script's version of this function claimed to do this but did
    not; this one actually does, and was validated against a simulated CRLF checkout
    before being handed back.
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

    $rawContent = Get-Content -Path $Path -Raw -Encoding UTF8

    # Detect the file's ORIGINAL line-ending style so it can be restored after editing -
    # a CRLF file stays CRLF, an LF file stays LF, and the diff never picks up unrelated
    # line-ending churn as a side effect of this script.
    $usesCrlf = $rawContent -match "`r`n"

    # Normalize file content AND the anchor text to LF-only before comparing.
    $normalizedContent = $rawContent -replace "`r`n", "`n"
    $normalizedOld = $Old -replace "`r`n", "`n"
    $normalizedNew = $New -replace "`r`n", "`n"

    $count = ([regex]::Matches($normalizedContent, [regex]::Escape($normalizedOld))).Count

    if ($count -eq 0) {
        Write-Warning "SKIP  [$Description]  -  anchor not found in $Path (already applied, or file has diverged  -  check manually)."
        return
    }
    if ($count -gt 1) {
        throw "ABORT [$Description]  -  anchor found $count times in $Path, expected exactly 1. Refusing to guess which one."
    }

    $updatedNormalized = $normalizedContent.Replace($normalizedOld, $normalizedNew)

    # Restore the file's original line-ending style before writing back.
    $updated = if ($usesCrlf) { $updatedNormalized -replace "`n", "`r`n" } else { $updatedNormalized }

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

$iPaymentRepoPath      = Join-Path $PSScriptRoot 'Edvanz.Domain\Interfaces\IPaymentRepo.cs'
$attendanceServicePath = Join-Path $PSScriptRoot 'Edvanz.Application\Services\AttendanceService.cs'
$attendanceDtosPath    = Join-Path $PSScriptRoot 'Edvanz.Application\Dtos\Attendance\AttendanceDtos.cs'

# =====================================================================================
# Edit 1 of 4  -  IPaymentRepo.cs: declare the missing interface method.
# =====================================================================================

$edit1Old = @'
    Task<CollectLookupRow?> ResolveCollectLookupAsync(
        long teacherId, string? qr, string? code, string? name, DateTime throughMonthEnd);

    // ══════════════════════════════════════════════
    // ASSISTANT WALLET QUERIES
    // ══════════════════════════════════════════════
'@

$edit1New = @'
    Task<CollectLookupRow?> ResolveCollectLookupAsync(
        long teacherId, string? qr, string? code, string? name, DateTime throughMonthEnd);

    // ══════════════════════════════════════════════
    // ATTENDANCE SCREEN PAYMENT ENRICHMENT (ShowPaymentInfoOnAttendanceScreen)
    // ══════════════════════════════════════════════

    /// <summary>
    /// Batched, per-student payment/debt snapshot for the Take/Edit Attendance list's optional
    /// payment enrichment — bounded to the caller's page of <paramref name="teacherStudentIds"/>,
    /// never the full roster. Reuses the exact same arrears rules as the Unpaid Students Overview
    /// (<see cref="UnpaidStudentRow"/>) per CLAUDE.md §7.4.
    /// </summary>
    Task<Dictionary<long, AttendanceScreenPaymentInfoRow>> GetPaymentInfoForAttendanceBatchAsync(
        long teacherId, IReadOnlyCollection<long> teacherStudentIds,
        DateTime throughMonthEnd, DateTime lastMonthStart, DateTime lastMonthEnd);

    // ══════════════════════════════════════════════
    // ASSISTANT WALLET QUERIES
    // ══════════════════════════════════════════════
'@

Apply-Edit -Path $iPaymentRepoPath -Old $edit1Old -New $edit1New `
    -Description '1/4 IPaymentRepo: declare GetPaymentInfoForAttendanceBatchAsync (this was the compile-blocker)'

# =====================================================================================
# Edit 2 of 4  -  AttendanceService.cs: wire enrichment into GetAttendanceStudentListAsync
#                 (Endpoint 3, .../sessions/{id}/students).
# =====================================================================================

$edit2Old = @'
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

$edit2New = @'
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

Apply-Edit -Path $attendanceServicePath -Old $edit2Old -New $edit2New `
    -Description '2/4 AttendanceService: wire enrichment into GetAttendanceStudentListAsync (Endpoint 3)'

# =====================================================================================
# Edit 3 of 4  -  AttendanceDtos.cs: add PaymentInfo/HistoryInfo to AttendanceRecordDto.
# =====================================================================================

$edit3Old = @'
public class AttendanceRecordDto
{
    public long Id { get; set; }
    public long TeacherStudentId { get; set; }
    public string StudentName { get; set; } = null!;
    public string StudentCode { get; set; } = null!;
    public long? SessionOccurrenceId { get; set; }
    public long? SessionId { get; set; }
    public string SessionName { get; set; } = null!;
    public DateTime OccurrenceDate { get; set; }
    public AttendanceStatus Status { get; set; }
    public AttendanceMethod AttendanceMethod { get; set; }
    public bool IsCrossSession { get; set; }
    public long? CrossSessionId { get; set; }
    public string? CrossSessionName { get; set; }
    public DateTime? CrossSessionOccurrenceDate { get; set; }
    public DateTime RecordedAt { get; set; }
    public bool IsEdited { get; set; }
    public DateTime? LastEditedAt { get; set; }
}
'@

$edit3New = @'
public class AttendanceRecordDto
{
    public long Id { get; set; }
    public long TeacherStudentId { get; set; }
    public string StudentName { get; set; } = null!;
    public string StudentCode { get; set; } = null!;
    public long? SessionOccurrenceId { get; set; }
    public long? SessionId { get; set; }
    public string SessionName { get; set; } = null!;
    public DateTime OccurrenceDate { get; set; }
    public AttendanceStatus Status { get; set; }
    public AttendanceMethod AttendanceMethod { get; set; }
    public bool IsCrossSession { get; set; }
    public long? CrossSessionId { get; set; }
    public string? CrossSessionName { get; set; }
    public DateTime? CrossSessionOccurrenceDate { get; set; }
    public DateTime RecordedAt { get; set; }
    public bool IsEdited { get; set; }
    public DateTime? LastEditedAt { get; set; }

    /// <summary>
    /// Payment/debt snapshot, present only when the teacher has
    /// <c>ShowPaymentInfoOnAttendanceScreen</c> enabled AND the caller populated it. Null on
    /// every endpoint that maps records via <c>MapToRecordDto</c> without this feature
    /// (mark/bulk-mark/edit/sync/reports) — only <c>GetOccurrenceStudentsAsync</c> (Edit
    /// Attendance past-date view) currently sets this.
    /// </summary>
    public StudentPaymentInfoDto? PaymentInfo { get; set; }

    /// <summary>
    /// Course-scoped and current-month absence counts, present only when the teacher has
    /// <c>ShowAttendanceHistoryOnAttendanceScreen</c> enabled AND the caller populated it. Same
    /// scoping as <see cref="PaymentInfo"/> — only <c>GetOccurrenceStudentsAsync</c> sets this.
    /// </summary>
    public StudentAttendanceHistoryInfoDto? HistoryInfo { get; set; }
}
'@

Apply-Edit -Path $attendanceDtosPath -Old $edit3Old -New $edit3New `
    -Description '3/4 AttendanceDtos: add PaymentInfo/HistoryInfo to AttendanceRecordDto'

# =====================================================================================
# Edit 4 of 4  -  AttendanceService.cs: wire the same enrichment into
#                 GetOccurrenceStudentsAsync (Endpoint 6B, .../occurrences/{date}/students).
# =====================================================================================

$edit4Old = @'
        // Equivalence-aware: include cross-session visitors who physically attended THIS session on
        // this date (their record lives on their home-session occurrence, tagged CrossSessionId=this).
        var records = await _unitOfWork.AttendanceRepo
            .GetRecordsForOccurrenceEditViewAsync(sessionId, occurrence.Id, occurrenceDate);
        var dtos = records.Select(r => MapToRecordDto(r,
            r.StudentName ?? r.TeacherStudent?.StudentName ?? "Unknown",
            r.StudentCode ?? r.TeacherStudent?.StudentCode ?? "")).ToList();

        return Result<List<AttendanceRecordDto>>.Success(
            dtos, _localizer, AttendanceConstants.Messages.Success);
    }
'@

$edit4New = @'
        // Equivalence-aware: include cross-session visitors who physically attended THIS session on
        // this date (their record lives on their home-session occurrence, tagged CrossSessionId=this).
        var records = await _unitOfWork.AttendanceRepo
            .GetRecordsForOccurrenceEditViewAsync(sessionId, occurrence.Id, occurrenceDate);
        var dtos = records.Select(r => MapToRecordDto(r,
            r.StudentName ?? r.TeacherStudent?.StudentName ?? "Unknown",
            r.StudentCode ?? r.TeacherStudent?.StudentCode ?? "")).ToList();

        // Optional per-teacher enrichment (ShowPaymentInfoOnAttendanceScreen /
        // ShowAttendanceHistoryOnAttendanceScreen) — same config-gated batch pattern as
        // GetAttendanceStudentListAsync (Endpoint 3), so the Edit Attendance past-date view
        // carries the same payment/history cards. "Current month" is always the teacher's
        // actual current local calendar day, not the occurrenceDate being viewed.
        var config = await _unitOfWork.Users.GetConfigurationByTeacherIdAsync(teacherId);
        bool showPaymentInfo = config?.ShowPaymentInfoOnAttendanceScreen ?? false;
        bool showAttendanceHistory = config?.ShowAttendanceHistoryOnAttendanceScreen ?? false;

        if ((showPaymentInfo || showAttendanceHistory) && dtos.Count > 0)
        {
            var teacherStudentIds = dtos.Select(d => d.TeacherStudentId).Distinct().ToList();
            var today = _timeZoneService.GetTeacherLocalDate(teacherId);
            var currentMonthStart = new DateTime(today.Year, today.Month, 1);

            Dictionary<long, AttendanceScreenPaymentInfoRow>? paymentInfoMap = null;
            if (showPaymentInfo)
            {
                var currentMonthEnd = currentMonthStart.AddMonths(1).AddDays(-1);
                var lastMonthStart = currentMonthStart.AddMonths(-1);
                var lastMonthEnd = currentMonthStart.AddDays(-1);

                paymentInfoMap = await _unitOfWork.PaymentsRepo.GetPaymentInfoForAttendanceBatchAsync(
                    teacherId, teacherStudentIds, currentMonthEnd, lastMonthStart, lastMonthEnd);
            }

            Dictionary<long, AttendanceHistoryCountsRow>? historyInfoMap = null;
            if (showAttendanceHistory)
            {
                var currentMonthEndExclusive = currentMonthStart.AddMonths(1);

                historyInfoMap = await _unitOfWork.AttendanceRepo.GetAttendanceHistoryCountsBatchAsync(
                    teacherId, teacherStudentIds, currentMonthStart, currentMonthEndExclusive);
            }

            foreach (var dto in dtos)
            {
                if (showPaymentInfo)
                {
                    var pay = paymentInfoMap != null && paymentInfoMap.TryGetValue(dto.TeacherStudentId, out var p)
                        ? p : null;
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
                    var hist = historyInfoMap != null && historyInfoMap.TryGetValue(dto.TeacherStudentId, out var h)
                        ? h : null;
                    dto.HistoryInfo = new StudentAttendanceHistoryInfoDto
                    {
                        CourseAbsences = hist?.CourseAbsences ?? 0,
                        CurrentMonthAbsences = hist?.CurrentMonthAbsences ?? 0
                    };
                }
            }
        }

        return Result<List<AttendanceRecordDto>>.Success(
            dtos, _localizer, AttendanceConstants.Messages.Success);
    }
'@

Apply-Edit -Path $attendanceServicePath -Old $edit4Old -New $edit4New `
    -Description '4/4 AttendanceService: wire enrichment into GetOccurrenceStudentsAsync (Endpoint 6B)'

Write-Host ''
Write-Host 'Done. Next steps:'
Write-Host '  1. dotnet build  -  confirm it compiles. The IPaymentRepo fix (edit 1) was the actual'
Write-Host '     compile-blocker that caused edit 2 to be missing in the first place - if this build'
Write-Host '     fails now, something else diverged and is worth pasting back to me.'
Write-Host '  2. No new migration needed - this batch is pure C# (interface + two service methods +'
Write-Host '     one DTO), no new columns or tables.'
Write-Host '  3. Rebuild AND restart whatever process actually serves {{BaseUrl}} - the recurring'
Write-Host '     theme in this thread has been a stale running process, not stale source.'
Write-Host '  4. Re-test both endpoints:'
Write-Host '       GET .../Attendance/sessions/84/students?Page=1&PageSize=100'
Write-Host '       GET .../Attendance/sessions/84/occurrences/{date}/students'
Write-Host '     Both should now show populated (or zeroed, if fully paid/no absences) paymentInfo'
Write-Host '     and historyInfo objects for teacher 20, given both config flags are already true.'
