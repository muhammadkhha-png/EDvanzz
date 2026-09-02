using Edvanz.Application.Dtos;
using Edvanz.Application.Dtos.TeacherStudent;

namespace Edvanz.Application.ServiceContract;

/// <summary>
/// Defines the contract for the Student Module (Module 1) operations.
/// Manages teacher-scoped student records: CRUD, search, filter, bulk import,
/// recycle bin, and student code generation.
/// 
/// IMPORTANT: This service manages TeacherStudent records — the student DATA
/// owned by a Teacher. It does NOT manage StudentUser accounts (those are
/// handled by IStudentUserService).
/// 
/// All methods are async. All return Result&lt;T&gt; for consistent error handling.
/// All database access goes through IUnitOfWork.
/// </summary>
public interface ITeacherStudentService
{
    // ══════════════════════════════════════════════
    // SINGLE STUDENT CRUD
    // ══════════════════════════════════════════════

    /// <summary>
    /// Creates a single student record under the teacher's account.
    /// REQ-STU-012: Single student entry form.
    /// REQ-STU-008/009: Auto-generates code if config is Auto.
    /// REQ-STU-047: Auto-generates barcode at creation.
    /// Validates capacity limit, name not empty, code uniqueness, and code format.
    /// </summary>
    Task<Result<TeacherStudentDto>>  CreateStudentAsync(long teacherId, CreateTeacherStudentDto dto);
    /// <summary>
    /// Retrieves a single active student record by Id, scoped to the teacher,
    /// including the assigned-session summary for the profile screen.
    /// </summary>
    Task<Result<TeacherStudentProfileDto>> GetStudentByIdAsync(long teacherId, long studentId);

    /// <summary>
    /// SUPER-ADMIN variant of <see cref="GetStudentByIdAsync"/> — no tenant
    /// scope. Resolves TeacherId from the student record itself (via an
    /// unscoped lookup), then delegates to the identical profile-assembly
    /// logic. Must only be reachable behind a roleOnly SuperAdmin gate.
    /// </summary>
    Task<Result<TeacherStudentProfileDto>> GetStudentByIdForAdminAsync(long studentId);

    /// <summary>
    /// Resolves a scanned/typed per-teacher <c>StudentCode</c> to its student — the canonical
    /// scan-resolution contract shared by every scanning surface (session/exam attendance,
    /// future link scans). EXACT match on <c>StudentCode</c> (not the partial roster search),
    /// tenant-scoped from the JWT, so the same code resolves only within this teacher's roster.
    /// Returns 400 <c>BarcodeRequired</c> for a blank code and 404 <c>StudentCodeNotFound</c>
    /// when no active student carries it. Case-insensitive and whitespace-trimmed; works for
    /// non-ASCII (Arabic) codes too.
    /// </summary>
    Task<Result<StudentCodeResolveDto>> ResolveByCodeAsync(long teacherId, string code);

    /// <summary>
    /// Updates an existing student record.
    /// REQ-STU-006: All fields remain editable after creation.
    /// REQ-STU-048: Barcode never changes even if other fields are modified.
    /// </summary>
    Task<Result<TeacherStudentDto>> UpdateStudentAsync(long teacherId, long studentId, UpdateTeacherStudentDto dto);

    /// <summary>
    /// SUPER-ADMIN variant of <see cref="UpdateStudentAsync"/> — no tenant
    /// scope. Resolves TeacherId from the student record itself, then
    /// delegates to the identical update/validation logic. Must only be
    /// reachable behind a roleOnly SuperAdmin gate.
    /// </summary>
    Task<Result<TeacherStudentDto>> UpdateStudentForAdminAsync(long studentId, UpdateTeacherStudentDto dto);

    // ══════════════════════════════════════════════
    // STUDENT LIST (SEARCH + FILTER + PAGINATION)
    // ══════════════════════════════════════════════

    /// <summary>
    /// Retrieves a paginated, searchable, filterable, sortable list of the teacher's active students.
    /// REQ-STU-032 through REQ-STU-046: Search, filter, sort, pagination.
    /// REQ-STU-SRT-005: Filters first, then sorts the filtered results.
    /// </summary>
    Task<Result<PaginatedResponse<List<TeacherStudentDto>>>> GetStudentListAsync(
        long teacherId, StudentListRequest request);

    /// <summary>
    /// Retrieves the student count summary for the module header.
    /// REQ-STU-UX-001: Total active count.
    /// REQ-STU-UX-002: Filtered count.
    /// REQ-STU-UX-009: Recycle bin badge count.
    /// </summary>
    Task<Result<StudentCountsDto>> GetStudentCountsAsync(long teacherId, StudentListRequest request);

    // ══════════════════════════════════════════════
    // SOFT DELETE (RECYCLE BIN)
    // ══════════════════════════════════════════════

    /// <summary>
    /// Soft-deletes a single student, moving it to the recycle bin, AND tears the record's
    /// live enrolment down: session cleared, session assignments deactivated, and the student
    /// ACCOUNT link (plus any Method-B parent link) ended as <c>RemovedByTeacher</c>.
    /// Without the teardown the student app keeps listing the teacher forever and the
    /// live-row filtered unique index blocks a new link request (see
    /// <see cref="IStudentTeardownService"/>).
    /// REQ-STU-021: Single delete from profile or list.
    /// REQ-STU-025: Moved to recycle bin, not permanently deleted.
    /// </summary>
    /// <param name="actingUserId">
    /// User.Id recorded as the account that ENDED the link (audit column
    /// <c>StudentTeacherLink.RemovedByUserId</c>). Controllers pass the JWT user id;
    /// background callers pass null.
    /// </param>
    Task<Result<bool>> SoftDeleteStudentAsync(long teacherId, long studentId, long? actingUserId = null);

    /// <summary>
    /// Soft-deletes multiple students in a single operation, applying the same
    /// unassign + unlink teardown as <see cref="SoftDeleteStudentAsync"/> to each.
    /// REQ-STU-022: Bulk delete via checkbox selection.
    /// </summary>
    Task<Result<int>> BulkSoftDeleteStudentsAsync(
        long teacherId, BulkStudentIdsDto dto, long? actingUserId = null);

    /// <summary>
    /// Retrieves the paginated recycle bin contents for a teacher.
    /// REQ-STU-029: View recycle bin contents.
    /// REQ-STU-UX-010: Includes days remaining before permanent purge.
    /// </summary>
    Task<Result<PaginatedResponse<List<RecycleBinStudentDto>>>> GetRecycleBinAsync(
        long teacherId, int page = 1, int pageSize = 20);

    /// <summary>
    /// Restores a single student from the recycle bin.
    /// REQ-STU-026: Restore at any time during 10-day window.
    /// REQ-STU-031: Restored with all original data intact.
    /// NOTE: restore deliberately does NOT resurrect the enrolment — the record comes back
    /// UNASSIGNED (no session) and UNLINKED (the account link stays terminal). Re-assign the
    /// session and re-accept/bind the link explicitly; silently re-granting a student account
    /// access to a teacher's content on a restore would be a privacy regression.
    /// </summary>
    Task<Result<TeacherStudentDto>> RestoreStudentAsync(long teacherId, long studentId);

    /// <summary>
    /// Restores multiple students from the recycle bin in a single action.
    /// REQ-STU-031.1: Bulk restore.
    /// </summary>
    Task<Result<int>> BulkRestoreStudentsAsync(long teacherId, BulkStudentIdsDto dto);

    /// <summary>
    /// Permanently deletes a single student from the recycle bin.
    /// REQ-STU-029: Manual permanent delete before 10-day expiry.
    /// WARNING: This is irreversible.
    /// </summary>
    Task<Result<bool>> PermanentDeleteStudentAsync(long teacherId, long studentId);

    /// <summary>
    /// Permanently deletes expired recycle bin records (older than 10 days).
    /// REQ-STU-027/028: Automated daily purge.
    /// Called by a background job, not by a controller endpoint.
    /// </summary>
    Task<int> PurgeExpiredRecycleBinRecordsAsync();

    // ══════════════════════════════════════════════
    // BULK IMPORT
    // ══════════════════════════════════════════════

    /// <summary>
    /// Imports multiple students from a parsed spreadsheet.
    /// REQ-STU-015 through REQ-STU-020: Bulk import with validation and summary report.
    /// REQ-STU-018: Skips empty names, auto-generates codes, rejects duplicates.
    /// REQ-STU-054: Auto-generates barcode for each imported student.
    ///
    /// <para>Everything is committed in ONE transaction, so the whole import is all-or-nothing:
    /// if <paramref name="cancellationToken"/> fires before the final commit (the streaming caller
    /// passes <c>HttpContext.RequestAborted</c>, so a disconnected/cancelled client trips it), the
    /// transaction is rolled back and NOTHING is saved. <paramref name="onProgress"/>, when supplied,
    /// is awaited during the slow per-student assignment phase as <c>(processed, total)</c> so a
    /// streaming endpoint can report a live counter; it is null for the plain (non-streaming)
    /// endpoint, whose behavior is unchanged.</para>
    /// </summary>
    Task<Result<BulkImportResultDto>> BulkImportStudentsAsync(
        long teacherId,
        BulkImportTeacherStudentsDto dto,
        Func<int, int, Task>? onProgress = null,
        CancellationToken cancellationToken = default);
    /// <summary>
    /// Builds the chip-strip data for the "Assign Students" screen: All / Unassigned
    /// counts plus one chip per session with its assigned-student count. Counts respect
    /// the optional search term (REQ-SES-016/017).
    /// </summary>
    Task<Result<SessionAssignmentChipsDto>> GetSessionAssignmentChipsAsync(long teacherId, string? search);
    /// <summary>
    /// Retrieves the student-list screen payload: the tenant-wide active-student
    /// total plus a paginated/filtered student page in one response.
    /// REQ-STU-UX-001 (total badge) + REQ-STU-032..046 (list search/filter/sort).
    /// </summary>
    Task<Result<TenantStudentListDto>> GetTenantStudentListAsync(
        long teacherId, StudentListRequest request);
    /// <summary>
    /// SUPER-ADMIN variant of <see cref="GetStudentListAsync(long, StudentListRequest)"/>.
    /// When <paramref name="teacherId"/> is null, returns the paginated list across ALL
    /// teachers (no tenant scope) — must only be reachable behind a roleOnly SuperAdmin
    /// gate. When supplied, behaves identically to the teacher-scoped overload, including
    /// the TeacherNotFound guard.
    /// </summary>
    Task<Result<PaginatedResponse<List<TeacherStudentDto>>>> GetStudentListForAdminAsync(
        long? teacherId, StudentListRequest request);

}