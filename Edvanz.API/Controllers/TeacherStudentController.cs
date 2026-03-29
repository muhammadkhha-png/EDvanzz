using Edvanz.Application.Dtos.TeacherStudent;
using Edvanz.Application.ServiceContract;
using Microsoft.AspNetCore.Mvc;

namespace Edvanz.API.Controllers;

/// <summary>
/// API endpoints for the Student Module (Module 1).
/// 
/// Manages teacher-scoped student records: CRUD, search, filter, pagination,
/// soft delete (recycle bin), restore, bulk import, and student counts.
/// 
/// IMPORTANT: These endpoints manage TeacherStudent records — the student DATA
/// owned by a Teacher. They do NOT manage StudentUser accounts (those are
/// handled by StudentUserController).
/// 
/// All responses follow a unified JSON shape:
///   Success: { "success": true,  "message": "...", "data": { ... } }
///   Failure: { "success": false, "message": "..." }
/// 
/// Messages are returned in Arabic or English based on the Accept-Language header.
/// Set "Accept-Language: ar" for Arabic, "Accept-Language: en" for English.
/// </summary>
public class TeacherStudentController : ApiBaseController
{
    private readonly ITeacherStudentService _studentService;

    public TeacherStudentController(ITeacherStudentService studentService)
    {
        _studentService = studentService;
    }

    // ══════════════════════════════════════════════════════════════════════════
    // ENDPOINT 1: CREATE STUDENT
    // ══════════════════════════════════════════════════════════════════════════
    //
    // WHAT IT DOES:
    //   Creates a single student record under the teacher's account.
    //   Auto-generates student code (if config is Auto), hashed token, and barcode.
    //   Validates: teacher exists, capacity not exceeded, name not empty,
    //   code uniqueness (if manual), code format (REQ-STU-CODE-001).
    //
    // TABLES WRITTEN:
    //   TeacherStudents
    //
    // TABLES READ:
    //   Teachers, TeacherConfigurations, TeacherStudents (for uniqueness/capacity)
    //
    // SAMPLE REQUEST:
    //   POST /api/teacherstudent
    //   {
    //     "teacherId": 1,
    //     "studentName": "Ahmed Mohamed",
    //     "studentPhoneNumber": "01012345678",
    //     "parentPhoneNumber": "01098765432",
    //     "studentCode": null,
    //     "sessionId": null
    //   }
    //
    // SAMPLE RESPONSE (201 Created):
    //   {
    //     "success": true,
    //     "message": "Student added successfully",
    //     "data": {
    //       "id": 1,
    //       "teacherId": 1,
    //       "studentName": "Ahmed Mohamed",
    //       "studentCode": "A1",
    //       "hashedToken": "X7B2KM4Q9R1TPWZN",
    //       "studentPhoneNumber": "01012345678",
    //       "parentPhoneNumber": "01098765432",
    //       "barcode": "A1",
    //       "sessionId": null,
    //       "isComplete": false,
    //       "createdAt": "2026-03-29T10:00:00Z"
    //     }
    //   }
    //
    // ══════════════════════════════════════════════════════════════════════════
    [HttpPost]
    [ProducesResponseType(typeof(object), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(object), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateStudent([FromBody] CreateTeacherStudentDto dto)
    {
        var result = await _studentService.CreateStudentAsync(dto);
        return ToResponse(result);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // ENDPOINT 2: GET STUDENT BY ID
    // ══════════════════════════════════════════════════════════════════════════
    //
    // WHAT IT DOES:
    //   Retrieves a single active student record by Id, scoped to the teacher.
    //   Returns 404 if the student doesn't exist, belongs to another teacher,
    //   or is in the recycle bin.
    //
    // SAMPLE REQUEST:
    //   GET /api/teacherstudent/1/students/5
    //
    // ══════════════════════════════════════════════════════════════════════════
    [HttpGet("{teacherId:long}/students/{studentId:long}")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetStudentById(
        [FromRoute] long teacherId,
        [FromRoute] long studentId)
    {
        var result = await _studentService.GetStudentByIdAsync(teacherId, studentId);
        return ToResponse(result);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // ENDPOINT 3: UPDATE STUDENT
    // ══════════════════════════════════════════════════════════════════════════
    //
    // WHAT IT DOES:
    //   Updates an existing student record. All fields are editable (REQ-STU-006).
    //   Barcode NEVER changes (REQ-STU-048).
    //   Student code is only editable when GenerationMode == Manual.
    //
    // SAMPLE REQUEST:
    //   PUT /api/teacherstudent/1/students/5
    //   {
    //     "studentName": "Ahmed Mohamed Ali",
    //     "studentPhoneNumber": "01099999999",
    //     "parentPhoneNumber": null,
    //     "studentCode": null,
    //     "sessionId": 3
    //   }
    //
    // ══════════════════════════════════════════════════════════════════════════
    [HttpPut("{teacherId:long}/students/{studentId:long}")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(object), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UpdateStudent(
        [FromRoute] long teacherId,
        [FromRoute] long studentId,
        [FromBody] UpdateTeacherStudentDto dto)
    {
        var result = await _studentService.UpdateStudentAsync(teacherId, studentId, dto);
        return ToResponse(result);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // ENDPOINT 4: GET STUDENT LIST (PAGINATED + SEARCH + FILTER + SORT)
    // ══════════════════════════════════════════════════════════════════════════
    //
    // WHAT IT DOES:
    //   Returns a paginated list of the teacher's active students.
    //   Supports search (name, code), filters (session, missing phone,
    //   missing parent phone, missing session), and sort (name, code, date added).
    //
    // QUERY PARAMETERS:
    //   - page (int, default 1)
    //   - pageSize (int, default 20, max 100)
    //   - search (string, optional): partial match on name or code
    //   - sessionId (long, optional): filter by assigned session
    //   - missingStudentPhone (bool, default false)
    //   - missingParentPhone (bool, default false)
    //   - missingSession (bool, default false)
    //   - sortBy (StudentSortBy: DateAdded, StudentName, StudentCode)
    //   - sortDirection (SortDirection: Asc, Desc)
    //
    // SAMPLE REQUEST:
    //   GET /api/teacherstudent/1/students?page=1&pageSize=20&search=ahmed&sortBy=StudentName&sortDirection=Asc
    //
    // ══════════════════════════════════════════════════════════════════════════
    [HttpGet("{teacherId:long}/students")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetStudentList(
        [FromRoute] long teacherId,
        [FromQuery] StudentListRequest request)
    {
        var result = await _studentService.GetStudentListAsync(teacherId, request);
        return ToResponse(result);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // ENDPOINT 5: GET STUDENT COUNTS
    // ══════════════════════════════════════════════════════════════════════════
    //
    // WHAT IT DOES:
    //   Returns the student count summary: total active, filtered count,
    //   and recycle bin count. Used for header counters and badges.
    //
    // SAMPLE REQUEST:
    //   GET /api/teacherstudent/1/counts?missingSession=true
    //
    // SAMPLE RESPONSE:
    //   {
    //     "success": true,
    //     "message": "Done successfully",
    //     "data": { "totalActiveStudents": 247, "filteredCount": 18, "recycleBinCount": 3 }
    //   }
    //
    // ══════════════════════════════════════════════════════════════════════════
    [HttpGet("{teacherId:long}/counts")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetStudentCounts(
        [FromRoute] long teacherId,
        [FromQuery] StudentListRequest request)
    {
        var result = await _studentService.GetStudentCountsAsync(teacherId, request);
        return ToResponse(result);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // ENDPOINT 6: SOFT DELETE STUDENT (SINGLE)
    // ══════════════════════════════════════════════════════════════════════════
    //
    // WHAT IT DOES:
    //   Moves a single student to the recycle bin (soft-delete).
    //   The student can be restored within 10 days.
    //
    // SAMPLE REQUEST:
    //   DELETE /api/teacherstudent/1/students/5
    //
    // ══════════════════════════════════════════════════════════════════════════
    [HttpDelete("{teacherId:long}/students/{studentId:long}")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SoftDeleteStudent(
        [FromRoute] long teacherId,
        [FromRoute] long studentId)
    {
        var result = await _studentService.SoftDeleteStudentAsync(teacherId, studentId);
        return ToResponse(result);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // ENDPOINT 7: BULK SOFT DELETE STUDENTS
    // ══════════════════════════════════════════════════════════════════════════
    //
    // WHAT IT DOES:
    //   Moves multiple students to the recycle bin in a single operation.
    //   REQ-STU-022: Bulk delete via checkbox selection.
    //
    // SAMPLE REQUEST:
    //   POST /api/teacherstudent/bulk-delete
    //   { "teacherId": 1, "studentIds": [5, 8, 12] }
    //
    // ══════════════════════════════════════════════════════════════════════════
    [HttpPost("bulk-delete")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> BulkSoftDeleteStudents([FromBody] BulkStudentIdsDto dto)
    {
        var result = await _studentService.BulkSoftDeleteStudentsAsync(dto);
        return ToResponse(result);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // ENDPOINT 8: GET RECYCLE BIN
    // ══════════════════════════════════════════════════════════════════════════
    //
    // WHAT IT DOES:
    //   Returns the paginated recycle bin contents. Each item includes
    //   days remaining before permanent purge (REQ-STU-UX-010).
    //
    // SAMPLE REQUEST:
    //   GET /api/teacherstudent/1/recycle-bin?page=1&pageSize=20
    //
    // ══════════════════════════════════════════════════════════════════════════
    [HttpGet("{teacherId:long}/recycle-bin")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetRecycleBin(
        [FromRoute] long teacherId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var result = await _studentService.GetRecycleBinAsync(teacherId, page, pageSize);
        return ToResponse(result);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // ENDPOINT 9: RESTORE STUDENT (SINGLE)
    // ══════════════════════════════════════════════════════════════════════════
    //
    // WHAT IT DOES:
    //   Restores a single student from the recycle bin with all data intact.
    //   Validates that restoring won't exceed capacity.
    //
    // SAMPLE REQUEST:
    //   POST /api/teacherstudent/1/recycle-bin/5/restore
    //
    // ══════════════════════════════════════════════════════════════════════════
    [HttpPost("{teacherId:long}/recycle-bin/{studentId:long}/restore")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RestoreStudent(
        [FromRoute] long teacherId,
        [FromRoute] long studentId)
    {
        var result = await _studentService.RestoreStudentAsync(teacherId, studentId);
        return ToResponse(result);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // ENDPOINT 10: BULK RESTORE STUDENTS
    // ══════════════════════════════════════════════════════════════════════════
    //
    // WHAT IT DOES:
    //   Restores multiple students from the recycle bin in a single action.
    //   REQ-STU-031.1: Bulk restore.
    //
    // SAMPLE REQUEST:
    //   POST /api/teacherstudent/bulk-restore
    //   { "teacherId": 1, "studentIds": [5, 8, 12] }
    //
    // ══════════════════════════════════════════════════════════════════════════
    [HttpPost("bulk-restore")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> BulkRestoreStudents([FromBody] BulkStudentIdsDto dto)
    {
        var result = await _studentService.BulkRestoreStudentsAsync(dto);
        return ToResponse(result);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // ENDPOINT 11: PERMANENT DELETE FROM RECYCLE BIN
    // ══════════════════════════════════════════════════════════════════════════
    //
    // WHAT IT DOES:
    //   Permanently deletes a student from the recycle bin. IRREVERSIBLE.
    //   REQ-STU-029: Manual permanent delete before 10-day expiry.
    //
    // SAMPLE REQUEST:
    //   DELETE /api/teacherstudent/1/recycle-bin/5/permanent
    //
    // ══════════════════════════════════════════════════════════════════════════
    [HttpDelete("{teacherId:long}/recycle-bin/{studentId:long}/permanent")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> PermanentDeleteStudent(
        [FromRoute] long teacherId,
        [FromRoute] long studentId)
    {
        var result = await _studentService.PermanentDeleteStudentAsync(teacherId, studentId);
        return ToResponse(result);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // ENDPOINT 12: BULK IMPORT STUDENTS
    // ══════════════════════════════════════════════════════════════════════════
    //
    // WHAT IT DOES:
    //   Imports multiple students from a parsed spreadsheet.
    //   Validates each row, auto-generates codes where blank,
    //   rejects duplicates, and returns a summary report.
    //
    // SAMPLE REQUEST:
    //   POST /api/teacherstudent/bulk-import
    //   {
    //     "teacherId": 1,
    //     "students": [
    //       { "studentName": "Ahmed", "studentPhoneNumber": "0101234", "parentPhoneNumber": null, "studentCode": null },
    //       { "studentName": "Sara", "studentPhoneNumber": null, "parentPhoneNumber": "0109876", "studentCode": "S1" },
    //       { "studentName": "", "studentPhoneNumber": null, "parentPhoneNumber": null, "studentCode": null }
    //     ]
    //   }
    //
    // SAMPLE RESPONSE (200 OK):
    //   {
    //     "success": true,
    //     "message": "Bulk import completed",
    //     "data": {
    //       "totalProcessed": 3,
    //       "successCount": 2,
    //       "failedCount": 1,
    //       "failures": [
    //         { "rowNumber": 3, "studentName": "", "studentCode": null, "reason": "Student name is required" }
    //       ]
    //     }
    //   }
    //
    // ══════════════════════════════════════════════════════════════════════════
    [HttpPost("bulk-import")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> BulkImportStudents([FromBody] BulkImportTeacherStudentsDto dto)
    {
        var result = await _studentService.BulkImportStudentsAsync(dto);
        return ToResponse(result);
    }
}