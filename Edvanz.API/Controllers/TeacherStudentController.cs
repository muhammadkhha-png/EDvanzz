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

    // ENDPOINT 1 — CREATE STUDENT (REQ-STU-012) — perm: Add
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

    // ENDPOINT 2 — GET STUDENT BY ID — perm: ViewProfile
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

    // ENDPOINT 3 — UPDATE STUDENT (REQ-STU-006, REQ-STU-048) — perm: Edit
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

    // ENDPOINT 4 — GET STUDENT LIST (REQ-STU-032..046) — perm: ViewList
    [HttpGet("students")]
    [ModulePermission(StudentConstants.ModuleName, StudentConstants.PermissionViewList)]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(object), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetStudentList([FromQuery] StudentListRequest request)
    {
        long? teacherId = await ResolveTeacherIdAsync();
        if (teacherId is null) return TeacherNotResolved();

        var result = await _studentService.GetStudentListAsync(teacherId.Value, request);
        return ToResponse(result);
    }

    // ENDPOINT 5 — GET STUDENT COUNTS (REQ-STU-UX-001/002/009) — perm: ViewList
    [HttpGet("counts")]
    [ModulePermission(StudentConstants.ModuleName, StudentConstants.PermissionViewList)]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(object), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetStudentCounts([FromQuery] StudentListRequest request)
    {
        long? teacherId = await ResolveTeacherIdAsync();
        if (teacherId is null) return TeacherNotResolved();

        var result = await _studentService.GetStudentCountsAsync(teacherId.Value, request);
        return ToResponse(result);
    }

    // ENDPOINT 6 — SOFT DELETE SINGLE (REQ-STU-021/025) — perm: Delete
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

    // ENDPOINT 7 — BULK SOFT DELETE (REQ-STU-022) — perm: Delete
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

    // ENDPOINT 8 — GET RECYCLE BIN (REQ-STU-029, REQ-STU-UX-010) — perm: Delete
    [HttpGet("recycle-bin")]
    [ModulePermission(StudentConstants.ModuleName, StudentConstants.PermissionDelete)]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(object), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetRecycleBin(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        long? teacherId = await ResolveTeacherIdAsync();
        if (teacherId is null) return TeacherNotResolved();

        var result = await _studentService.GetRecycleBinAsync(teacherId.Value, page, pageSize);
        return ToResponse(result);
    }

    // ENDPOINT 9 — RESTORE SINGLE (REQ-STU-026/031) — perm: Delete
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

    // ENDPOINT 10 — BULK RESTORE (REQ-STU-031.1) — perm: Delete
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

    // ENDPOINT 11 — PERMANENT DELETE (REQ-STU-029, IRREVERSIBLE) — perm: Delete
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

    // ENDPOINT 12 — BULK IMPORT (REQ-STU-015..020) — perm: Import
    [HttpPost("bulk-import")]
    [ModulePermission(StudentConstants.ModuleName, StudentConstants.PermissionImport)]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(object), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(object), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> BulkImportStudents([FromBody] BulkImportTeacherStudentsDto dto)
    {
        long? teacherId = await ResolveTeacherIdAsync();
        if (teacherId is null) return TeacherNotResolved();

        var result = await _studentService.BulkImportStudentsAsync(teacherId.Value, dto);
        return ToResponse(result);
    }
}