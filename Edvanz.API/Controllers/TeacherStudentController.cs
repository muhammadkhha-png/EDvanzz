
using Edvanz.API.Attributes;
using Edvanz.Application.Dtos.TeacherStudent;
using Edvanz.Application.IservicesContract;
using Edvanz.Application.ServiceContract;
using Edvanz.Domain.Constants;
using Edvanz.Domain.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Edvanz.API.Controllers;

/// <summary>
/// API endpoints for the Student Module (Module 1) — teacher-scoped student records:
/// CRUD, search/filter/sort, recycle bin, restore, bulk import, and counts.
///
/// AUTH &amp; TENANCY: class-level [Authorize] + per-action
/// [ModulePermission(StudentConstants.ModuleName, ...)]. The acting teacherId is
/// ALWAYS derived from the JWT via ModuleSixApiBaseController.ResolveTeacherIdAsync —
/// never read from the route or body (Catalog §1.3). Assistants resolve to their
/// owning tutor automatically; SuperAdmin bypasses the permission filter.
///
/// These manage TeacherStudent records (student DATA owned by a teacher), NOT
/// StudentUser accounts — those live in StudentUserController.
/// </summary>
[Authorize]
public class TeacherStudentController : ModuleSixApiBaseController
{
    private readonly ITeacherStudentService _studentService;

    public TeacherStudentController(
        ITeacherStudentService studentService,
        ICurrentUserService currentUser,
        IUnitOfWork unitOfWork)
        : base(currentUser, unitOfWork)
    {
        _studentService = studentService;
    }

    /// <summary>Creates a new student record under the authenticated teacher's account.</summary>
    /// <remarks>
    /// REQ-STU-012: Manual student entry form. Only <c>StudentName</c> is required; all other fields are optional.
    /// REQ-STU-005: Only StudentName is mandatory on creation.
    /// AUTH: requires <c>Student / Add</c> permission.
    /// TENANCY: teacherId resolved from JWT — never from route or body (IDOR-safe, §1.3).
    /// Assistants with <c>Add</c> permission resolve to their owning tutor.
    /// </remarks>
    /// <param name="dto">New student data. Only <c>StudentName</c> is mandatory (REQ-STU-014).</param>
    /// <response code="201">Student created successfully. Returns the new <c>TeacherStudentDto</c>.</response>
    /// <response code="400">Validation failure (e.g. empty name, duplicate student code, capacity exceeded).</response>
    /// <response code="401">JWT missing or expired.</response>
    /// <response code="403">Caller lacks the <c>Student / Add</c> permission.</response>
    /// <response code="404">Teacher record not found for the authenticated user.</response>
    [HttpPost]
    [ModulePermission(StudentConstants.ModuleName, StudentConstants.PermissionAdd)]
    [ProducesResponseType(typeof(object), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(object), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(object), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CreateStudent([FromBody] CreateTeacherStudentDto dto)
    {
        long? teacherId = await ResolveTeacherIdAsync();
        if (teacherId is null) return TeacherNotResolved();

        var result = await _studentService.CreateStudentAsync(teacherId.Value, dto);
        return ToResponse(result);
    }

    /// <summary>Returns a single student record by its primary key, scoped to the authenticated teacher.</summary>
    /// <remarks>
    /// AUTH: requires <c>Student / ViewProfile</c> permission.
    /// TENANCY: teacherId from JWT — the service rejects studentIds that do not belong to the resolved teacher.
    /// </remarks>
    /// <param name="studentId">Primary key of the TeacherStudent record.</param>
    /// <response code="200">Returns the <c>TeacherStudentDto</c> for the requested student.</response>
    /// <response code="401">JWT missing or expired.</response>
    /// <response code="403">Caller lacks the <c>Student / ViewProfile</c> permission.</response>
    /// <response code="404">Student not found, soft-deleted, or does not belong to this teacher; or teacher record not found.</response>
    [HttpGet("students/{studentId:long}")]
    [ModulePermission(StudentConstants.ModuleName, StudentConstants.PermissionViewProfile)]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(object), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetStudentById([FromRoute] long studentId)
    {
        long? teacherId = await ResolveTeacherIdAsync();
        if (teacherId is null) return TeacherNotResolved();

        var result = await _studentService.GetStudentByIdAsync(teacherId.Value, studentId);
        return ToResponse(result);
    }

    /// <summary>Updates an existing student record. Supply null for optional fields to clear them.</summary>
    /// <remarks>
    /// REQ-STU-006: All student fields remain fully editable after creation.
    /// REQ-STU-048: Optimistic concurrency via RowVersion — returns 409 if the record was modified concurrently.
    /// AUTH: requires <c>Student / Edit</c> permission.
    /// TENANCY: teacherId from JWT — the service rejects studentIds not owned by this teacher.
    /// </remarks>
    /// <param name="studentId">Primary key of the student record to update.</param>
    /// <param name="dto">Updated student fields. <c>StudentName</c> is mandatory.</param>
    /// <response code="200">Student updated successfully. Returns the updated <c>TeacherStudentDto</c>.</response>
    /// <response code="400">Validation failure (e.g. empty name, invalid student code format).</response>
    /// <response code="401">JWT missing or expired.</response>
    /// <response code="403">Caller lacks the <c>Student / Edit</c> permission.</response>
    /// <response code="404">Student not found or does not belong to this teacher; or teacher record not found.</response>
    /// <response code="409">Optimistic concurrency conflict — record was modified by another request. Refresh and retry.</response>
    [HttpPut("students/{studentId:long}")]
    [ModulePermission(StudentConstants.ModuleName, StudentConstants.PermissionEdit)]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(object), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(object), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(object), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UpdateStudent(
        [FromRoute] long studentId,
        [FromBody] UpdateTeacherStudentDto dto)
    {
        long? teacherId = await ResolveTeacherIdAsync();
        if (teacherId is null) return TeacherNotResolved();

        var result = await _studentService.UpdateStudentAsync(teacherId.Value, studentId, dto);
        return ToResponse(result);
    }

    /// <summary>Returns a paginated, filtered, and sorted list of active students for the authenticated teacher.</summary>
    /// <remarks>
    /// REQ-STU-032 through REQ-STU-046: Student list with filter panel and sort options.
    /// REQ-STU-SRT-001 through REQ-STU-SRT-007: Sort columns (DateAdded desc by default).
    /// AUTH: requires <c>Student / ViewList</c> permission.
    /// TENANCY: teacherId from JWT — only this teacher's students are returned.
    /// Response <c>data</c> is a <c>PaginatedResponse&lt;TeacherStudentDto&gt;</c> with <c>data[]</c>,
    /// <c>page</c>, <c>pageSize</c>, and <c>totalCount</c> fields.
    /// </remarks>
    /// <param name="request">Pagination, search, filter, and sort parameters.</param>
    /// <response code="200">Paginated list of active students.</response>
    /// <response code="401">JWT missing or expired.</response>
    /// <response code="403">Caller lacks the <c>Student / ViewList</c> permission.</response>
    /// <response code="404">Teacher record not found for the authenticated user.</response>
    [HttpGet("students")]
    [ModulePermission(StudentConstants.ModuleName, StudentConstants.PermissionViewList)]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(object), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetStudentList([FromQuery] StudentListRequest request)
    {
        long? teacherId = await ResolveTeacherIdAsync();
        if (teacherId is null) return TeacherNotResolved();

        var result = await _studentService.GetStudentListAsync(teacherId.Value, request);
        return ToResponse(result);
    }

    /// <summary>Returns aggregate student counts (total active, filtered, recycle bin) for the authenticated teacher.</summary>
    /// <remarks>
    /// REQ-STU-UX-001: Total active student count badge.
    /// REQ-STU-UX-002: Filtered count when the filter panel is active.
    /// REQ-STU-UX-009: Recycle bin count for the bin badge.
    /// AUTH: requires <c>Student / ViewList</c> permission.
    /// TENANCY: teacherId from JWT.
    /// Accepts the same <see cref="StudentListRequest"/> filter params as the list endpoint
    /// so the UI can display the filtered count without fetching a full page.
    /// </remarks>
    /// <param name="request">Filter parameters (pagination fields are ignored for count purposes).</param>
    /// <response code="200">Returns <c>StudentCountsDto</c> with <c>totalActiveStudents</c>, <c>filteredCount</c>, and <c>recycleBinCount</c>.</response>
    /// <response code="401">JWT missing or expired.</response>
    /// <response code="403">Caller lacks the <c>Student / ViewList</c> permission.</response>
    /// <response code="404">Teacher record not found for the authenticated user.</response>
    [HttpGet("counts")]
    [ModulePermission(StudentConstants.ModuleName, StudentConstants.PermissionViewList)]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(object), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetStudentCounts([FromQuery] StudentListRequest request)
    {
        long? teacherId = await ResolveTeacherIdAsync();
        if (teacherId is null) return TeacherNotResolved();

        var result = await _studentService.GetStudentCountsAsync(teacherId.Value, request);
        return ToResponse(result);
    }

    /// <summary>Soft-deletes a single student record (moves to recycle bin). Reversible via the restore endpoint.</summary>
    /// <remarks>
    /// REQ-STU-021: Student can be moved to the recycle bin.
    /// REQ-STU-025: Recycle bin auto-purges records after the retention window expires.
    /// AUTH: requires <c>Student / Delete</c> permission.
    /// TENANCY: teacherId from JWT — the service rejects studentIds not owned by this teacher.
    /// </remarks>
    /// <param name="studentId">Primary key of the student record to soft-delete.</param>
    /// <response code="200">Student moved to recycle bin successfully.</response>
    /// <response code="401">JWT missing or expired.</response>
    /// <response code="403">Caller lacks the <c>Student / Delete</c> permission.</response>
    /// <response code="404">Student not found, already deleted, or not owned by this teacher; or teacher record not found.</response>
    [HttpDelete("students/{studentId:long}")]
    [ModulePermission(StudentConstants.ModuleName, StudentConstants.PermissionDelete)]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(object), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SoftDeleteStudent([FromRoute] long studentId)
    {
        long? teacherId = await ResolveTeacherIdAsync();
        if (teacherId is null) return TeacherNotResolved();

        var result = await _studentService.SoftDeleteStudentAsync(teacherId.Value, studentId);
        return ToResponse(result);
    }

    /// <summary>Soft-deletes multiple student records in a single request (bulk move to recycle bin).</summary>
    /// <remarks>
    /// REQ-STU-022: Bulk delete by selecting multiple student IDs from the list view.
    /// AUTH: requires <c>Student / Delete</c> permission.
    /// TENANCY: teacherId from JWT — IDs not belonging to this teacher are rejected entirely.
    /// </remarks>
    /// <param name="dto">List of student record IDs to soft-delete.</param>
    /// <response code="200">All selected students moved to recycle bin.</response>
    /// <response code="400">Request body is invalid or <c>studentIds</c> list is empty.</response>
    /// <response code="401">JWT missing or expired.</response>
    /// <response code="403">Caller lacks the <c>Student / Delete</c> permission.</response>
    /// <response code="404">One or more IDs not found or not owned by this teacher; or teacher record not found.</response>
    [HttpPost("bulk-delete")]
    [ModulePermission(StudentConstants.ModuleName, StudentConstants.PermissionDelete)]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(object), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(object), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> BulkSoftDeleteStudents([FromBody] BulkStudentIdsDto dto)
    {
        long? teacherId = await ResolveTeacherIdAsync();
        if (teacherId is null) return TeacherNotResolved();

        var result = await _studentService.BulkSoftDeleteStudentsAsync(teacherId.Value, dto);
        return ToResponse(result);
    }

    /// <summary>Returns a paginated list of soft-deleted students in the recycle bin for the authenticated teacher.</summary>
    /// <remarks>
    /// REQ-STU-029: Recycle bin view.
    /// REQ-STU-UX-010: Each row includes <c>daysRemaining</c> before permanent purge, used for color-coded urgency.
    /// AUTH: requires <c>Student / Delete</c> permission.
    /// TENANCY: teacherId from JWT — only this teacher's deleted students are returned.
    /// </remarks>
    /// <param name="page">Page number (1-based). Defaults to 1.</param>
    /// <param name="pageSize">Records per page. Defaults to 20.</param>
    /// <response code="200">Paginated list of deleted students. Each item includes <c>daysRemaining</c> before permanent purge.</response>
    /// <response code="401">JWT missing or expired.</response>
    /// <response code="403">Caller lacks the <c>Student / Delete</c> permission.</response>
    /// <response code="404">Teacher record not found for the authenticated user.</response>
    [HttpGet("recycle-bin")]
    [ModulePermission(StudentConstants.ModuleName, StudentConstants.PermissionDelete)]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(object), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetRecycleBin(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        long? teacherId = await ResolveTeacherIdAsync();
        if (teacherId is null) return TeacherNotResolved();

        var result = await _studentService.GetRecycleBinAsync(teacherId.Value, page, pageSize);
        return ToResponse(result);
    }

    /// <summary>Restores a single soft-deleted student from the recycle bin back to the active list.</summary>
    /// <remarks>
    /// REQ-STU-026: Restore a deleted student.
    /// REQ-STU-031: Restore clears <c>IsDeleted</c> and <c>DeletedAt</c>.
    /// AUTH: requires <c>Student / Delete</c> permission.
    /// TENANCY: teacherId from JWT — only students in this teacher's recycle bin can be restored.
    /// Returns 400 if the student is not currently in the recycle bin (already active or permanently deleted).
    /// </remarks>
    /// <param name="studentId">Primary key of the soft-deleted student to restore.</param>
    /// <response code="200">Student restored to the active list.</response>
    /// <response code="400">Student is not in the recycle bin (not currently soft-deleted).</response>
    /// <response code="401">JWT missing or expired.</response>
    /// <response code="403">Caller lacks the <c>Student / Delete</c> permission.</response>
    /// <response code="404">Student not found or not owned by this teacher; or teacher record not found.</response>
    [HttpPost("recycle-bin/{studentId:long}/restore")]
    [ModulePermission(StudentConstants.ModuleName, StudentConstants.PermissionDelete)]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(object), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(object), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RestoreStudent([FromRoute] long studentId)
    {
        long? teacherId = await ResolveTeacherIdAsync();
        if (teacherId is null) return TeacherNotResolved();

        var result = await _studentService.RestoreStudentAsync(teacherId.Value, studentId);
        return ToResponse(result);
    }

    /// <summary>Restores multiple soft-deleted students from the recycle bin in a single request.</summary>
    /// <remarks>
    /// REQ-STU-031.1: Bulk restore from recycle bin.
    /// AUTH: requires <c>Student / Delete</c> permission.
    /// TENANCY: teacherId from JWT — IDs not belonging to this teacher's recycle bin are rejected.
    /// </remarks>
    /// <param name="dto">List of soft-deleted student record IDs to restore.</param>
    /// <response code="200">All selected students restored to the active list.</response>
    /// <response code="400">Request body is invalid, <c>studentIds</c> list is empty, or one or more students are not in the recycle bin.</response>
    /// <response code="401">JWT missing or expired.</response>
    /// <response code="403">Caller lacks the <c>Student / Delete</c> permission.</response>
    /// <response code="404">One or more IDs not found or not owned by this teacher; or teacher record not found.</response>
    [HttpPost("bulk-restore")]
    [ModulePermission(StudentConstants.ModuleName, StudentConstants.PermissionDelete)]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(object), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(object), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> BulkRestoreStudents([FromBody] BulkStudentIdsDto dto)
    {
        long? teacherId = await ResolveTeacherIdAsync();
        if (teacherId is null) return TeacherNotResolved();

        var result = await _studentService.BulkRestoreStudentsAsync(teacherId.Value, dto);
        return ToResponse(result);
    }

    /// <summary>Permanently deletes a single student from the recycle bin. IRREVERSIBLE — no restore path exists after this call.</summary>
    /// <remarks>
    /// REQ-STU-029: Hard-delete from recycle bin.
    /// AUTH: requires <c>Student / Delete</c> permission.
    /// TENANCY: teacherId from JWT — the service rejects studentIds not in this teacher's recycle bin.
    /// The student must be in the recycle bin (soft-deleted) before this endpoint can be called.
    /// </remarks>
    /// <param name="studentId">Primary key of the soft-deleted student to permanently remove.</param>
    /// <response code="200">Student permanently deleted.</response>
    /// <response code="401">JWT missing or expired.</response>
    /// <response code="403">Caller lacks the <c>Student / Delete</c> permission.</response>
    /// <response code="404">Student not found in this teacher's recycle bin; or teacher record not found.</response>
    [HttpDelete("recycle-bin/{studentId:long}/permanent")]
    [ModulePermission(StudentConstants.ModuleName, StudentConstants.PermissionDelete)]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(object), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> PermanentDeleteStudent([FromRoute] long studentId)
    {
        long? teacherId = await ResolveTeacherIdAsync();
        if (teacherId is null) return TeacherNotResolved();

        var result = await _studentService.PermanentDeleteStudentAsync(teacherId.Value, studentId);
        return ToResponse(result);
    }

    /// <summary>Imports multiple student records in bulk from a structured list (e.g. Google Sheets export).</summary>
    /// <remarks>
    /// REQ-STU-015 through REQ-STU-020: Bulk import flow.
    /// REQ-STU-018: Rows with empty <c>studentName</c> are skipped. Student codes are auto-generated when blank.
    /// REQ-STU-019: Returns a summary report with per-row success/failure detail.
    /// AUTH: requires <c>Student / Import</c> permission.
    /// TENANCY: teacherId from JWT — all imported students are created under this teacher.
    /// The operation is best-effort: valid rows succeed even if some rows fail validation.
    /// </remarks>
    /// <param name="dto">Import envelope containing the list of student rows to process.</param>
    /// <response code="200">Import completed. Returns <c>BulkImportResultDto</c> with <c>totalProcessed</c>, <c>successCount</c>, <c>failedCount</c>, and per-row failure details.</response>
    /// <response code="400">Request body is invalid or <c>students</c> list is null/empty.</response>
    /// <response code="401">JWT missing or expired.</response>
    /// <response code="403">Caller lacks the <c>Student / Import</c> permission.</response>
    /// <response code="404">Teacher record not found for the authenticated user.</response>
    [HttpPost("bulk-import")]
    [ModulePermission(StudentConstants.ModuleName, StudentConstants.PermissionImport)]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(object), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(object), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> BulkImportStudents([FromBody] BulkImportTeacherStudentsDto dto)
    {
        long? teacherId = await ResolveTeacherIdAsync();
        if (teacherId is null) return TeacherNotResolved();

        var result = await _studentService.BulkImportStudentsAsync(teacherId.Value, dto);
        return ToResponse(result);
    }
    /// <summary>Returns the chip-strip data for the "Assign Students" screen: All / Unassigned counts plus one chip per session with its assigned-student count.</summary>
    /// <remarks>
    /// REQ-SES-016/017: student picker for session assignment.
    /// Counts reflect the optional <c>search</c> term; the session set is the full catalog (stable while searching).
    /// AUTH: requires <c>Student / ViewList</c> permission.
    /// TENANCY: teacherId from JWT.
    /// </remarks>
    /// <param name="search">Optional name/code partial match applied to the counts.</param>
    /// <response code="200">Returns <c>SessionAssignmentChipsDto</c>.</response>
    /// <response code="401">JWT missing or expired.</response>
    /// <response code="403">Caller lacks the <c>Student / ViewList</c> permission.</response>
    /// <response code="404">Teacher record not found for the authenticated user.</response>
    [HttpGet("assignment-chips")]
    [ModulePermission(StudentConstants.ModuleName, StudentConstants.PermissionViewList)]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(object), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetSessionAssignmentChips([FromQuery] string? search)
    {
        long? teacherId = await ResolveTeacherIdAsync();
        if (teacherId is null) return TeacherNotResolved();

        var result = await _studentService.GetSessionAssignmentChipsAsync(teacherId.Value, search);
        return ToResponse(result);
    }
}
