using Edvanz.Domain.Entities;

namespace Edvanz.Domain.Interfaces;

/// <summary>
/// Extended repository interface for the Student Module (Module 1: Students).
/// Centralizes all domain-specific query methods for TeacherStudent records.
/// 
/// WHY THIS EXISTS (same rationale as IUserRepo):
/// All query logic lives here in named methods. The Application layer never
/// builds raw expression predicates. If a query changes, you edit ONE method
/// in the repo — not every service that uses it.
/// 
/// Inherits from IGenericRepo&lt;TeacherStudent, long&gt; so basic CRUD is still available.
/// 
/// IMPORTANT: This is a SEPARATE repo from IUserRepo. The Student Module (teacher's
/// student management) is a different bounded context from the User module ecosystem.
/// IUserRepo handles User, Teacher, StudentUser, ParentUser, and linking.
/// ITeacherStudentRepo handles the teacher's student records CRUD, search, filter, recycle bin.
/// </summary>
public interface ITeacherStudentRepo : IGenericRepo<TeacherStudent, long>
{
    // ══════════════════════════════════════════════
    // SINGLE RECORD QUERIES
    // ══════════════════════════════════════════════

    /// <summary>
    /// Finds an active (non-deleted) student by Id, scoped to the teacher.
    /// Returns null if the student doesn't exist, belongs to another teacher, or is soft-deleted.
    /// </summary>
    Task<TeacherStudent?> GetActiveByIdAndTeacherAsync(long studentId, long teacherId);

    /// <summary>
    /// Finds a student by Id and teacher, including soft-deleted records.
    /// Used for recycle bin restore and permanent delete operations.
    /// REQ-STU-029: View, restore, or permanently delete from recycle bin.
    /// </summary>
    Task<TeacherStudent?> GetByIdAndTeacherIgnoreFiltersAsync(long studentId, long teacherId);

    // ══════════════════════════════════════════════
    // UNIQUENESS & EXISTENCE CHECKS
    // ══════════════════════════════════════════════

    /// <summary>
    /// Checks if a student code already exists under this teacher's account.
    /// REQ-STU-010: Student code must be unique per teacher account.
    /// Checks ONLY active (non-deleted) records — a code freed by permanent purge can be reused.
    /// </summary>
    Task<bool> StudentCodeExistsAsync(long teacherId, string studentCode);

    /// <summary>
    /// Checks if a student code exists, excluding a specific student Id.
    /// Used during update to allow the student to keep their own code.
    /// </summary>
    Task<bool> StudentCodeExistsExcludingAsync(long teacherId, string studentCode, long excludeStudentId);

    // ══════════════════════════════════════════════
    // CODE GENERATION SUPPORT
    // ══════════════════════════════════════════════

    /// <summary>
    /// Retrieves the highest (latest) auto-generated student code for a teacher.
    /// Used by the student code generator to determine the next sequential code.
    /// REQ-STU-007: Sequential codes A1 → Z999 — always picks next after highest existing.
    /// Considers ALL codes (including soft-deleted) to avoid collisions with recycle bin records.
    /// </summary>
    /// <summary>
    /// Returns every student code for the teacher (INCLUDING soft-deleted rows) that is short
    /// enough to be part of an auto-generated sequence (length 2-5). The generator parses these
    /// in memory and picks the highest that matches the [Letter][Digits] pattern for the target
    /// language, so a non-parseable manual/imported code (e.g. "Z9X9") can never poison the
    /// sequence and force a reset to the first code — which previously collided on every add and
    /// surfaced (mislabeled) as "phone number already registered".
    /// </summary>
    Task<List<string>> GetSequentialCandidateCodesAsync(long teacherId);

    // ══════════════════════════════════════════════
    // COUNT QUERIES
    // ══════════════════════════════════════════════

    /// <summary>
    /// Counts all active (non-deleted) students for a teacher.
    /// REQ-STU-UX-001: Live student counter in the module header.
    /// Also used for capacity validation (Teacher.StudentCapacity).
    /// </summary>
    Task<int> CountActiveStudentsAsync(long teacherId);

    /// <summary>
    /// Counts students currently in the recycle bin for a teacher.
    /// REQ-STU-UX-009: Recycle bin badge count.
    /// Requires IgnoreQueryFilters to see soft-deleted records.
    /// </summary>
    Task<int> CountRecycleBinStudentsAsync(long teacherId);

    // ══════════════════════════════════════════════
    // LIST / SEARCH / FILTER QUERIES
    // ══════════════════════════════════════════════



    /// <summary>
    /// Builds a queryable for the teacher's recycle bin (soft-deleted students only).
    /// REQ-STU-029: View recycle bin contents.
    /// Requires IgnoreQueryFilters to see soft-deleted records.
    /// Ordered by DeletedAt descending (most recently deleted first).
    /// </summary>
    IQueryable<TeacherStudent> BuildRecycleBinQuery(long teacherId);

    // ══════════════════════════════════════════════
    // BULK OPERATIONS
    // ══════════════════════════════════════════════

    /// <summary>
    /// Retrieves multiple active students by their Ids, scoped to the teacher.
    /// Used for bulk delete (REQ-STU-022) and bulk barcode export (REQ-STU-052).
    /// </summary>
    Task<IReadOnlyList<TeacherStudent>> GetActiveByIdsAndTeacherAsync(long teacherId, IEnumerable<long> studentIds);

    /// <summary>
    /// Retrieves multiple soft-deleted students by their Ids, scoped to the teacher.
    /// Used for bulk restore (REQ-STU-031.1) and bulk permanent delete from recycle bin.
    /// </summary>
    Task<IReadOnlyList<TeacherStudent>> GetDeletedByIdsAndTeacherAsync(long teacherId, IEnumerable<long> studentIds);

    // ══════════════════════════════════════════════
    // PURGE (PERMANENT DELETE)
    // ══════════════════════════════════════════════

    /// <summary>
    /// Retrieves all soft-deleted students whose 10-day retention window has expired.
    /// REQ-STU-027/028: Daily automated purge of records past 10-day retention.
    /// Uses IgnoreQueryFilters to access soft-deleted records.
    /// </summary>
    Task<IReadOnlyList<TeacherStudent>> GetExpiredRecycleBinRecordsAsync();

    /// <summary>
    /// Get students by multiple IDs in clean reusable way
    /// </summary>
    public  Task<IReadOnlyList<TeacherStudent>> GetByIdsAsync(List<long> studentIds, long teacherId);



    /// <summary>
    /// Finds an active (non-deleted) student by their student code, scoped to the teacher.
    /// Used by the *ByCode status-entry endpoints (REQ-EXH-024). Code matching is exact
    /// (case-sensitive) — student codes are stored in normalized form by REQ-STU-007.
    /// </summary>
    Task<TeacherStudent?> GetActiveByCodeAndTeacherAsync(string studentCode, long teacherId);
    /// <summary>
    /// Returns All / Unassigned scalar counts plus per-session assigned counts for the
    /// teacher's active students, filtered by an optional name/code search term.
    /// Powers the "Assign Students" screen chip strip (REQ-SES-016/017).
    /// Per-session rows include only sessions with ≥1 matching student; the service
    /// left-joins them onto the full session catalog.
    /// </summary>
    Task<StudentAssignmentCounts> GetAssignmentCountsAsync(long teacherId, string? search = null);
    /// <summary>
    /// Builds a filtered, searchable, sortable IQueryable for the teacher's active students.
    /// The caller (service layer) uses this with GetPagedAsync for pagination.
    /// 
    /// REQ-STU-032: Search by name, code, or session name (partial match).
    /// REQ-STU-036: Filter by session, missing phone, missing parent phone, missing session.
    /// REQ-STU-SRT-001: Sort by name, code, or date added.
    /// REQ-STU-SRT-005: Filter first, then sort.
    /// 
    /// Returns IQueryable so pagination is applied at the database level (not in-memory).
    /// </summary>
    /// <param name="teacherId">The owning teacher's Id (multi-tenant scope).</param>
    /// <param name="search">Optional search term (partial match on name, code).</param>
    /// <param name="sessionId">Optional filter: only students in this session.</param>
    /// <param name="missingStudentPhone">If true, only students with no phone number.</param>
    /// <param name="missingParentPhone">If true, only students with no parent phone.</param>
    /// <param name="missingSession">If true, only students with no assigned session.</param>
    /// <param name="sortBy">Column to sort by. Defaults to DateAdded.</param>
    /// <param name="sortDirection">Sort direction. Defaults to Desc.</param>

    IQueryable<TeacherStudent> BuildStudentListQuery(
    long? teacherId,
    string? search = null,
    long? sessionId = null,
    bool missingStudentPhone = false,
    bool missingParentPhone = false,
    bool missingSession = false,
    Enums.StudentSortBy sortBy = Enums.StudentSortBy.DateAdded,
    Enums.SortDirection sortDirection = Enums.SortDirection.Desc);
}