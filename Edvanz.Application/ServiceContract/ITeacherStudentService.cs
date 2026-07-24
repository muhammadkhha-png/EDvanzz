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
    /// Soft-deletes a single student, moving it to the recycle bin.
    /// REQ-STU-021: Single delete from profile or list.
    /// REQ-STU-025: Moved to recycle bin, not permanently deleted.
    /// </summary>
    Task<Result<bool>> SoftDeleteStudentAsync(long teacherId, long studentId);

    /// <summary>
    /// Soft-deletes multiple students in a single operation.
    /// REQ-STU-022: Bulk delete via checkbox selection.
    /// </summary>
    Task<Result<int>> BulkSoftDeleteStudentsAsync(long teacherId, BulkStudentIdsDto dto);

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
    /// </summary>
    Task<Result<BulkImportResultDto>> BulkImportStudentsAsync(long teacherId, BulkImportTeacherStudentsDto dto);
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