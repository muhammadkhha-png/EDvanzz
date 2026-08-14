#Requires -Version 5.1
<#
.SYNOPSIS
    Student Accounts admin list  -  GET /api/studentuser/list (SuperAdmin only).
    Anchor-based find/replace + new-file creation, not a git patch  -  safe against
    CRLF checkout mismatches.

.DESCRIPTION
    Adds a paginated, SuperAdmin-only endpoint that lists ALL StudentUser accounts on
    the platform, each enriched with the list of teachers it currently holds an ACTIVE
    StudentTeacherLink to (teacher identity + the per-teacher student code from the
    bound TeacherStudent roster record). Supports search (account full name OR
    per-teacher student code) and an optional teacherId filter. Uses the project's
    existing PaginatedResponse<T> / Result<T> envelope  -  no new pagination pattern.

    4 new files, 5 anchor edits to existing files:

      1. Edvanz.Domain/Models/StudentAccountRepoProjections.cs   - NEW. Repo projection
         rows (StudentAccountRow, StudentAccountLinkedTeacherRow), same convention as
         StudentLinkRepoProjections.cs (namespace Edvanz.Domain.Interfaces so IUserRepo
         needs no extra using).
      2. Edvanz.Application/Dtos/StudentUser/StudentAccountListRequest.cs - NEW. Its own
         Page/PageSize/Search/TeacherId request type  -  NOT reusing the shared
         PaginatedRequest, whose SortBy is typed to the Teacher-specific sort enum (same
         reason StudentListRequest in TeacherStudentDtos.cs doesn't reuse it either).
      3. Edvanz.Application/Dtos/StudentUser/StudentAccountTeacherDto.cs - NEW. One
         ACTIVE teacher-link entry (teacherId, teacherCode, studentCode, teacherName).
      4. Edvanz.Application/Dtos/StudentUser/StudentAccountListItemDto.cs - NEW. One
         account row (studentAccountId, fullName, userName, phoneNumber, teachers[]).
      5. Edvanz.Domain/Interfaces/IUserRepo.cs           - +2 method contracts.
      6. Edvanz.Infrastructure/Repositories/UserRepo.cs  - +2 implementations. Page 1:
         CountAsync + Skip/Take over StudentUser (soft-delete filters apply
         automatically). Page 2: ONE batch query for every ACTIVE StudentTeacherLink of
         the page's account ids, grouped in-memory by StudentUserId by the caller  -
         same N+1-avoidance convention as GetTeacherDashboardDataAsync /
         GetActiveLinkedStudentsForTeacherPagedAsync already in this file.
      7. Edvanz.Application/ServiceContract/IStudentUserService.cs - +1 contract.
      8. Edvanz.Application/Services/StudentUserService.cs         - +1 implementation
         (batch-loads linked teachers for the page, maps to DTOs, wraps in the standard
         PaginatedResponse<T>).
      9. Edvanz.API/Controllers/StudentUserController.cs - +1 endpoint: GET "list",
         [ModulePermission(roles: new[] { "SuperAdmin" }, roleOnly: true)]  -  same
         SuperAdmin-only gate as TeacherController's GET /api/teacher/list.

    Business-rule assumptions baked into this implementation (flag if any is wrong):
      - "Linked teachers" = ACTIVE StudentTeacherLink rows only (not Pending/terminal).
      - studentCode is nullable: an Active link's bound TeacherStudent can be null
        (SetNull FK if the teacher later deletes the roster record)  -  same degraded
        state GetActiveLinkedStudentsForTeacherPagedAsync already handles.
      - No AccountStatus/IsActive filter applied  -  SuperAdmin sees every non-deleted
        account (Active/Inactive/Suspended); this isn't a "my students" screen.
      - Search matches ONLY account FullName + per-teacher StudentCode, exactly as
        specified  -  Username and the global StudentAccountCode are NOT searched.
      - Default sort: CreateAt descending (newest account first), matching
        TeacherController.GetTeachers' default; no sort param requested, so none exposed.

.NOTES
    Run from the repo root (muhammadkhha-png/EDvanzz, master_integration branch).
    Idempotent: if an anchor is already gone (i.e. already applied) that edit is
    skipped with a warning, not a failure; a new file that already exists is skipped
    the same way. Verified end-to-end against a fresh pristine clone of
    master_integration before delivery: all 9 steps apply cleanly on a first run
    (simulated against a CRLF-converted checkout), all 9 correctly SKIP on a second run
    (no double-insertion  -  every anchor was deliberately chosen so it is NOT a
    substring of its own replacement), and the resulting files are byte-identical
    (modulo line endings) to the reviewed implementation.

    Saved with a UTF-8 BOM on purpose: this script's own comments contain em-dashes and
    the box-drawing header characters used throughout the codebase's XML docs/region
    comments. Windows PowerShell 5.1 does not reliably assume UTF-8 for a BOM-less
    script or a BOM-less Get-Content read  -  without the BOM here, and -Encoding UTF8
    below, those bytes get silently reinterpreted under the console's codepage and the
    script (or the files it writes) end up corrupted. If you ever re-save this file,
    keep it UTF-8 with BOM.
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

    # CRLF-safety: this repo's checkout under Git for Windows typically normalizes to
    # CRLF on disk. The Old/New here-strings below are LF-only. Matching (and building
    # the replacement) is done against an LF-normalized view of both, and the file's OWN
    # original line-ending convention is restored before writing - never mixing CRLF and
    # LF within one file, and matching correctly whether the checkout is CRLF (typical
    # Windows) or LF (e.g. under WSL).
    $usesCrlf = $content.Contains("`r`n")
    $normalizedContent = $content -replace "`r`n", "`n"
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
    $updated = if ($usesCrlf) { $updatedNormalized -replace "`n", "`r`n" } else { $updatedNormalized }

    # OneDrive (and sometimes an AV scanner) transiently locks files under a synced
    # Desktop path right after a write. Retry with backoff instead of failing the whole
    # run on a lock that clears itself within a second or two.
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
        Write-Warning "SKIP  [$Description]  -  $Path already exists (already applied, or file was created independently  -  check manually)."
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
# New files (4)
# =====================================================================================

$newFile1Path = Join-Path $PSScriptRoot 'Edvanz.Domain\Models\StudentAccountRepoProjections.cs'

$newFile1Content = @'
namespace Edvanz.Domain.Interfaces;

// ════════════════════════════════════════════════════════════════════════════
// STUDENT ACCOUNT (SUPER-ADMIN LIST) — REPOSITORY PROJECTION TYPES
// ════════════════════════════════════════════════════════════════════════════
//
// Query projections returned by the IUserRepo student-account list methods for
// the SuperAdmin "Student Accounts" screen. Same convention as
// StudentLinkRepoProjections: these are NOT DTOs — the repo returns these, the
// service maps them to client-facing DTOs (Catalog: repo projections vs.
// Application DTOs stay separate so a repo shape change never leaks to the API).
// ════════════════════════════════════════════════════════════════════════════

/// <summary>
/// One page row on the SuperAdmin student-accounts list: the account identity
/// only (StudentUser + User). Linked-teacher rows are loaded separately in one
/// batch query (<see cref="IUserRepo.GetActiveLinkedTeachersForStudentUsersAsync"/>)
/// keyed by <see cref="StudentAccountId"/> to avoid an N+1 per row.
/// </summary>
public sealed class StudentAccountRow
{
    /// <summary>StudentUser.Id — the value returned to the client as studentAccountId.</summary>
    public long StudentAccountId { get; set; }

    public string FullName { get; set; } = null!;
    public string UserName { get; set; } = null!;
    public string? PhoneNumber { get; set; }
}

/// <summary>
/// One ACTIVE teacher link for a student account, batch-loaded for a page of
/// <see cref="StudentAccountRow"/> results. <see cref="StudentCode"/> is the
/// per-teacher code from the bound TeacherStudent roster record — null when
/// the Active link is not (or no longer) bound to one (see
/// GetActiveLinkedStudentsForTeacherPagedAsync for the same degraded-link case).
/// </summary>
public sealed class StudentAccountLinkedTeacherRow
{
    /// <summary>FK back to the owning <see cref="StudentAccountRow.StudentAccountId"/> for grouping.</summary>
    public long StudentUserId { get; set; }

    public long TeacherId { get; set; }
    public string TeacherCode { get; set; } = null!;
    public string TeacherName { get; set; } = null!;
    public string? StudentCode { get; set; }
}

'@

New-FileIfMissing -Path $newFile1Path -Content $newFile1Content `
    -Description '1/9 New file: StudentAccountRow / StudentAccountLinkedTeacherRow repo projections'

$newFile2Path = Join-Path $PSScriptRoot 'Edvanz.Application\Dtos\StudentUser\StudentAccountListRequest.cs'

$newFile2Content = @'
using System.ComponentModel;

namespace Edvanz.Application.Dtos.StudentUser;

/// <summary>
/// Paginated request for the SuperAdmin "Student Accounts" list
/// (<c>GET /api/studentuser/list</c>). Deliberately its own request type rather
/// than the shared <see cref="PaginatedRequest"/> — that type's <c>SortBy</c> is
/// typed to the Teacher-specific sort enum and doesn't apply here, same reason
/// the Student Module's own <c>StudentListRequest</c> (TeacherStudentDtos.cs)
/// doesn't reuse it either.
/// </summary>
public class StudentAccountListRequest
{
    private int _page = 1;
    private int _pageSize = 20;

    /// <summary>Page number (1-based). Defaults to 1.</summary>
    public int Page
    {
        get => _page;
        set => _page = value < 1 ? 1 : value;
    }

    /// <summary>Records per page. Defaults to 20. Max 100.</summary>
    public int PageSize
    {
        get => _pageSize;
        set => _pageSize = value < 1 ? 20 : value > 100 ? 100 : value;
    }

    /// <summary>
    /// Search term matched against the account's full name and, for each of its
    /// ACTIVE teacher links, the per-teacher student code assigned by that
    /// teacher. Partial match, case-insensitive.
    /// </summary>
    [Description("Search by: student name, or per-teacher student code (partial match)")]
    public string? Search { get; set; }

    /// <summary>
    /// Optional filter: only return accounts holding an ACTIVE link to this
    /// Teacher.Id.
    /// </summary>
    public long? TeacherId { get; set; }
}

'@

New-FileIfMissing -Path $newFile2Path -Content $newFile2Content `
    -Description '2/9 New file: StudentAccountListRequest (pagination + search + teacherId filter)'

$newFile3Path = Join-Path $PSScriptRoot 'Edvanz.Application\Dtos\StudentUser\StudentAccountTeacherDto.cs'

$newFile3Content = @'
namespace Edvanz.Application.Dtos.StudentUser;

/// <summary>
/// One ACTIVE teacher link on a <see cref="StudentAccountListItemDto"/> row —
/// the teacher's identity plus the per-teacher code this student was assigned
/// under that teacher's roster.
/// </summary>
public class StudentAccountTeacherDto
{
    public long TeacherId { get; set; }

    /// <summary>Teacher's unique, immutable 8-digit code.</summary>
    public string TeacherCode { get; set; } = null!;

    /// <summary>
    /// The per-teacher code (<c>TeacherStudent.StudentCode</c>) this account was
    /// assigned under this teacher's roster. Null when the Active link is not
    /// (or no longer) bound to a roster record — see
    /// <see cref="Edvanz.Domain.Entities.StudentTeacherLink.TeacherStudentId"/>.
    /// </summary>
    public string? StudentCode { get; set; }

    /// <summary>Teacher's full name from the User table.</summary>
    public string TeacherName { get; set; } = null!;
}

'@

New-FileIfMissing -Path $newFile3Path -Content $newFile3Content `
    -Description '3/9 New file: StudentAccountTeacherDto'

$newFile4Path = Join-Path $PSScriptRoot 'Edvanz.Application\Dtos\StudentUser\StudentAccountListItemDto.cs'

$newFile4Content = @'
namespace Edvanz.Application.Dtos.StudentUser;

/// <summary>
/// One row on the SuperAdmin "Student Accounts" list
/// (<c>GET /api/studentuser/list</c>): the account's identity plus every
/// teacher it currently holds an ACTIVE link to.
/// </summary>
public class StudentAccountListItemDto
{
    /// <summary>StudentUser.Id.</summary>
    public long StudentAccountId { get; set; }

    /// <summary>Account holder's full name, from the User table.</summary>
    public string FullName { get; set; } = null!;

    /// <summary>Account username, from the User table.</summary>
    public string UserName { get; set; } = null!;

    public string? PhoneNumber { get; set; }

    /// <summary>
    /// Every teacher this account currently holds an ACTIVE
    /// <see cref="Edvanz.Domain.Entities.StudentTeacherLink"/> to. Empty when the
    /// account has never linked to a teacher, or all its links are
    /// Pending/terminal (Rejected/Unlinked/RemovedByTeacher/CancelledByStudent).
    /// </summary>
    public List<StudentAccountTeacherDto> Teachers { get; set; } = new();
}

'@

New-FileIfMissing -Path $newFile4Path -Content $newFile4Content `
    -Description '4/9 New file: StudentAccountListItemDto'

# =====================================================================================
# Edits to existing files (5)
# =====================================================================================

$iUserRepoPath = Join-Path $PSScriptRoot 'Edvanz.Domain\Interfaces\IUserRepo.cs'

$edit5Old = @'
        Task<string?> GetUserLanguagePreferenceByUserIdAsync(long userId);


    }
'@

$edit5New = @'
        Task<string?> GetUserLanguagePreferenceByUserIdAsync(long userId);

        // ══════════════════════════════════════════════
        // STUDENT ACCOUNTS — SUPER-ADMIN PAGINATED LIST
        // ══════════════════════════════════════════════

        /// <summary>
        /// Pages ALL StudentUser accounts on the platform (not scoped to any teacher) for
        /// the SuperAdmin "Student Accounts" screen. Excludes soft-deleted accounts via the
        /// StudentUser/User query filters (no explicit predicate needed here).
        ///
        /// <paramref name="search"/> matches (case-insensitive, partial): the account's
        /// <c>User.FullName</c>, OR the per-teacher <c>TeacherStudent.StudentCode</c> of any of
        /// its ACTIVE teacher links. <paramref name="teacherId"/>, when supplied, restricts the
        /// result to accounts holding an ACTIVE <see cref="Entities.StudentTeacherLink"/> to that
        /// teacher. Ordered by <c>CreateAt</c> descending (newest account first), matching the
        /// Teacher admin list's default ordering.
        ///
        /// Returns identity rows ONLY — call
        /// <see cref="GetActiveLinkedTeachersForStudentUsersAsync"/> with the returned ids to
        /// batch-load each account's linked teachers without an N+1.
        /// </summary>
        Task<(IReadOnlyList<StudentAccountRow> Items, int TotalCount)> GetStudentAccountsPagedAsync(
            string? search, long? teacherId, int page, int pageSize);

        /// <summary>
        /// Batch-loads every ACTIVE <see cref="Entities.StudentTeacherLink"/> — teacher identity
        /// plus the bound per-teacher student code — for the given set of StudentUser ids, in one
        /// round trip. Used by <see cref="GetStudentAccountsPagedAsync"/>'s caller to enrich a page
        /// of student accounts with their linked-teachers list (group the result by
        /// <see cref="StudentAccountLinkedTeacherRow.StudentUserId"/>).
        /// </summary>
        Task<IReadOnlyList<StudentAccountLinkedTeacherRow>> GetActiveLinkedTeachersForStudentUsersAsync(
            IReadOnlyList<long> studentUserIds);

    }
'@

Apply-Edit -Path $iUserRepoPath -Old $edit5Old -New $edit5New `
    -Description '5/9 IUserRepo: add GetStudentAccountsPagedAsync + GetActiveLinkedTeachersForStudentUsersAsync contracts'

$userRepoPath = Join-Path $PSScriptRoot 'Edvanz.Infrastructure\Repositories\UserRepo.cs'

$edit6Old = @'
                _ => null
            };
        }
    }
}
'@

$edit6New = @'
                _ => null
            };
        }

        // ══════════════════════════════════════════════
        // STUDENT ACCOUNTS — SUPER-ADMIN PAGINATED LIST
        // ══════════════════════════════════════════════

        /// <inheritdoc />
        public async Task<(IReadOnlyList<StudentAccountRow> Items, int TotalCount)> GetStudentAccountsPagedAsync(
            string? search, long? teacherId, int page, int pageSize)
        {
            // StudentUser and User both carry their own soft-delete HasQueryFilter
            // (DeletedAt == null) — no explicit predicate needed for either here.
            var query = _context.Set<StudentUser>().AsNoTracking();

            if (teacherId.HasValue)
            {
                query = query.Where(su => su.StudentTeacherLinks.Any(l =>
                    l.TeacherId == teacherId.Value && l.LinkStatus == LinkStatus.Active));
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim();
                query = query.Where(su =>
                    su.User.FullName.Contains(term) ||
                    su.StudentTeacherLinks.Any(l =>
                        l.LinkStatus == LinkStatus.Active &&
                        l.TeacherStudent != null &&
                        l.TeacherStudent.StudentCode.Contains(term)));
            }

            int total = await query.CountAsync();

            var items = await query
                .OrderByDescending(su => su.CreateAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(su => new StudentAccountRow
                {
                    StudentAccountId = su.Id,
                    FullName = su.User.FullName,
                    UserName = su.User.Username,
                    PhoneNumber = su.User.PhoneNumber
                })
                .ToListAsync();

            return (items, total);
        }

        /// <inheritdoc />
        public async Task<IReadOnlyList<StudentAccountLinkedTeacherRow>> GetActiveLinkedTeachersForStudentUsersAsync(
            IReadOnlyList<long> studentUserIds)
        {
            var idList = studentUserIds.ToList();
            if (idList.Count == 0)
                return Array.Empty<StudentAccountLinkedTeacherRow>();

            // Required-nav projection (l.Teacher.*) implicitly applies Teacher's own
            // soft-delete query filter — a link pointing at a deleted teacher is dropped,
            // same behavior as every other Teacher-joining query in this repo.
            return await _context.Set<StudentTeacherLink>()
                .AsNoTracking()
                .Where(l => l.LinkStatus == LinkStatus.Active && idList.Contains(l.StudentUserId))
                .Select(l => new StudentAccountLinkedTeacherRow
                {
                    StudentUserId = l.StudentUserId,
                    TeacherId = l.TeacherId,
                    TeacherCode = l.Teacher.TeacherCode,
                    TeacherName = l.Teacher.User.FullName,
                    StudentCode = l.TeacherStudent != null ? l.TeacherStudent.StudentCode : null
                })
                .ToListAsync();
        }
    }
}
'@

Apply-Edit -Path $userRepoPath -Old $edit6Old -New $edit6New `
    -Description '6/9 UserRepo: implement GetStudentAccountsPagedAsync + GetActiveLinkedTeachersForStudentUsersAsync'

$iStudentUserServicePath = Join-Path $PSScriptRoot 'Edvanz.Application\ServiceContract\IStudentUserService.cs'

$edit7Old = @'
    Task<Result<bool>> RegisterDeviceForTeacherAsync(long studentUserId, long teacherId, string? deviceId);
}
'@

$edit7New = @'
    Task<Result<bool>> RegisterDeviceForTeacherAsync(long studentUserId, long teacherId, string? deviceId);

    /// <summary>
    /// SuperAdmin-only: pages ALL student accounts on the platform (not scoped to
    /// any teacher), each enriched with the list of teachers it currently holds
    /// an ACTIVE link to. Supports searching by account full name or by the
    /// per-teacher student code, and filtering to a single teacher's accounts.
    /// </summary>
    /// <param name="request">Page/pageSize plus the optional search term and teacherId filter.</param>
    /// <returns>Result containing the paginated, enriched student-accounts page.</returns>
    Task<Result<PaginatedResponse<List<StudentAccountListItemDto>>>> GetStudentAccountsAsync(
        StudentAccountListRequest request);
}
'@

Apply-Edit -Path $iStudentUserServicePath -Old $edit7Old -New $edit7New `
    -Description '7/9 IStudentUserService: add GetStudentAccountsAsync contract'

$studentUserServicePath = Join-Path $PSScriptRoot 'Edvanz.Application\Services\StudentUserService.cs'

$edit8Old = @'
        return link.LinkStatus.ToString();
    }

    /// <summary>
    /// Architecture-safe unique-index violation detection (same rationale as
'@

$edit8New = @'
        return link.LinkStatus.ToString();
    }

    /// <inheritdoc />
    public async Task<Result<PaginatedResponse<List<StudentAccountListItemDto>>>> GetStudentAccountsAsync(
        StudentAccountListRequest request)
    {
        var (items, totalCount) = await _unitOfWork.Users.GetStudentAccountsPagedAsync(
            request.Search, request.TeacherId, request.Page, request.PageSize);

        // Batch-load linked teachers for exactly this page's accounts — one extra
        // query total, regardless of page size (same convention as
        // GetMyTeachersAsync's GetTeacherDashboardDataAsync batch load above).
        var studentAccountIds = items.Select(i => i.StudentAccountId).ToList();
        var teacherRows = await _unitOfWork.Users.GetActiveLinkedTeachersForStudentUsersAsync(studentAccountIds);

        var teachersByAccount = teacherRows
            .GroupBy(r => r.StudentUserId)
            .ToDictionary(
                g => g.Key,
                g => g.Select(r => new StudentAccountTeacherDto
                {
                    TeacherId = r.TeacherId,
                    TeacherCode = r.TeacherCode,
                    StudentCode = r.StudentCode,
                    TeacherName = r.TeacherName
                }).ToList());

        var dtos = items.Select(i => new StudentAccountListItemDto
        {
            StudentAccountId = i.StudentAccountId,
            FullName = i.FullName,
            UserName = i.UserName,
            PhoneNumber = i.PhoneNumber,
            Teachers = teachersByAccount.TryGetValue(i.StudentAccountId, out var teachers)
                ? teachers
                : new List<StudentAccountTeacherDto>()
        }).ToList();

        var response = new PaginatedResponse<List<StudentAccountListItemDto>>
        {
            totalCount = totalCount,
            page = request.Page,
            pageSize = request.PageSize,
            totalPages = totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)request.PageSize),
            data = dtos
        };

        return Result<PaginatedResponse<List<StudentAccountListItemDto>>>.Success(response, _localizer);
    }

    /// <summary>
    /// Architecture-safe unique-index violation detection (same rationale as
'@

Apply-Edit -Path $studentUserServicePath -Old $edit8Old -New $edit8New `
    -Description '8/9 StudentUserService: implement GetStudentAccountsAsync'

$studentUserControllerPath = Join-Path $PSScriptRoot 'Edvanz.API\Controllers\StudentUserController.cs'

$edit9Old = @'
    public async Task<IActionResult> RegisterDeviceForTeacher([FromRoute] long teacherId)
    {
        var studentUserId = await ResolveStudentUserIdAsync();
        if (studentUserId is null) return StudentNotResolved();

        var result = await _studentUserService.RegisterDeviceForTeacherAsync(
            studentUserId.Value, teacherId, this.ReadDeviceId());
        return ToResponse(result);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // LOOKUP BY ACCOUNT CODE (used by the Parent module, AAM-FR-06.3 Method A)
    // ══════════════════════════════════════════════════════════════════════════
'@

$edit9New = @'
    public async Task<IActionResult> RegisterDeviceForTeacher([FromRoute] long teacherId)
    {
        var studentUserId = await ResolveStudentUserIdAsync();
        if (studentUserId is null) return StudentNotResolved();

        var result = await _studentUserService.RegisterDeviceForTeacherAsync(
            studentUserId.Value, teacherId, this.ReadDeviceId());
        return ToResponse(result);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // SUPER ADMIN: STUDENT ACCOUNTS LIST
    // ══════════════════════════════════════════════════════════════════════════
    //
    // WHAT IT DOES:
    //   Pages ALL student accounts on the platform (not scoped to any teacher).
    //   Each row is enriched with the list of teachers that account currently
    //   holds an ACTIVE link to — teacher identity plus the per-teacher student
    //   code that teacher assigned this account — in one call, no N+1.
    //
    // QUERY PARAMETERS:
    //   - page (int, default 1): Page number (1-based)
    //   - pageSize (int, default 20, max 100): Records per page
    //   - search (string, optional): Matches account full name, OR the
    //     per-teacher student code of any of the account's ACTIVE teacher links
    //   - teacherId (long, optional): Only accounts ACTIVE-linked to this teacher
    //
    // TABLES READ:
    //   StudentUsers, Users, StudentTeacherLinks, Teachers, TeacherStudents
    //
    // SOFT-DELETE:
    //   Deleted student accounts (StudentUser.DeletedAt != null) and deleted
    //   users (User.DeletedAt != null) are excluded by the EF Core global query
    //   filters — they never appear in this list.
    //
    // SAMPLE REQUEST:
    //   GET /api/studentuser/list?page=1&pageSize=20&search=ahmed&teacherId=12
    //
    // SAMPLE RESPONSE (200 OK):
    //   {
    //     "success": true,
    //     "message": "Done successfully",
    //     "data": {
    //       "totalCount": 3,
    //       "page": 1,
    //       "pageSize": 20,
    //       "totalPages": 1,
    //       "data": [
    //         {
    //           "studentAccountId": 501,
    //           "fullName": "Ahmed Mostafa",
    //           "userName": "ahmed.m",
    //           "phoneNumber": "01000000000",
    //           "teachers": [
    //             {
    //               "teacherId": 12,
    //               "teacherCode": "48291057",
    //               "studentCode": "S001",
    //               "teacherName": "Mariam Hassan"
    //             }
    //           ]
    //         }
    //       ]
    //     }
    //   }
    //
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// SuperAdmin-only: paginated list of every student account on the platform,
    /// searchable by name / per-teacher student code and filterable by teacher.
    /// Each row carries the account's ACTIVE teacher links (teacher identity +
    /// the per-teacher student code), loaded via one batched query — no N+1.
    /// </summary>
    /// <response code="200">Paginated student-accounts page.</response>
    /// <response code="401">JWT missing or expired.</response>
    /// <response code="403">Caller is not a SuperAdmin.</response>
    [HttpGet("list")]
    [ModulePermission(roles: new[] { "SuperAdmin" }, roleOnly: true)]
    [ProducesResponseType(typeof(Edvanz.Application.Dtos.Result<Edvanz.Application.Dtos.PaginatedResponse<System.Collections.Generic.List<Edvanz.Application.Dtos.StudentUser.StudentAccountListItemDto>>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(object), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetStudentAccounts([FromQuery] StudentAccountListRequest request)
    {
        var result = await _studentUserService.GetStudentAccountsAsync(request);
        return ToResponse(result);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // LOOKUP BY ACCOUNT CODE (used by the Parent module, AAM-FR-06.3 Method A)
    // ══════════════════════════════════════════════════════════════════════════
'@

Apply-Edit -Path $studentUserControllerPath -Old $edit9Old -New $edit9New `
    -Description '9/9 StudentUserController: add GET /api/studentuser/list endpoint'

Write-Host ''
Write-Host 'Done. Next steps:'
Write-Host '  1. dotnet build  -  build it locally to confirm it compiles before trusting it.'
Write-Host '  2. Swagger: GET /api/studentuser/list as a SuperAdmin. Try search= (name match and'
Write-Host '     per-teacher-code match separately) and teacherId= against known seeded data.'
Write-Host '  3. Confirm 403 when called with a Teacher/Assistant/Student token (roleOnly gate).'
Write-Host '  4. Confirm a soft-deleted StudentUser and a soft-deleted User never appear in the list.'
Write-Host '  5. Confirm an Active link whose TeacherStudent was deleted still returns the teacher'
Write-Host '     entry with studentCode: null, rather than being dropped from teachers[].'
