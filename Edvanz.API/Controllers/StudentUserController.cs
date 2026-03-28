using Edvanz.Application.Dtos;
using Edvanz.Application.Dtos.StudentUser;
using Edvanz.Application.ServiceContract;
using Microsoft.AspNetCore.Mvc;

namespace Edvanz.API.Controllers;

/// <summary>
/// API endpoints for the Student User module.
/// 
/// This controller handles all operations specific to the Student User account type:
/// profile management, teacher linking, dashboard retrieval, and account code lookup.
/// 
/// IMPORTANT: Registration (creating the User record), login, password update, and
/// account deletion endpoints are NOT here — they live in the User module controller.
/// The User module creates the User record first, then calls InitializeStudentUser to
/// set up the Student-specific data (account code, first-login flag).
/// 
/// All responses follow a unified JSON shape:
///   Success: { "success": true,  "message": "...", "data": { ... } }
///   Failure: { "success": false, "message": "..." }
/// 
/// Messages are returned in Arabic or English based on the Accept-Language header.
/// Set "Accept-Language: ar" for Arabic, "Accept-Language: en" for English.
/// </summary>
public class StudentUserController : ApiBaseController
{
    private readonly IStudentUserService _studentUserService;

    public StudentUserController(IStudentUserService studentUserService)
    {
        _studentUserService = studentUserService;
    }

    // ══════════════════════════════════════════════════════════════════════════
    // ENDPOINT 1: INITIALIZE STUDENT USER
    // ══════════════════════════════════════════════════════════════════════════
    //
    // WHAT IT DOES:
    //   Called AFTER a User record is created with UserType = Student.
    //   This endpoint creates the StudentUser-specific data that doesn't belong
    //   on the User table: generates the unique StudentAccountCode and sets
    //   IsFirstLogin = true so the dashboard shows the "Add Teacher" prompt.
    //
    // WHAT IT CREATES IN THE DATABASE:
    //   1. One row in StudentUsers table (with generated StudentAccountCode)
    //
    // WHO CALLS IT:
    //   The User module, after successfully creating a User with UserType.Student.
    //
    // VALIDATIONS:
    //   - UserId must reference an existing User with UserType = Student (404 if not)
    //   - Must not already have a StudentUser record for this UserId (409 if duplicate)
    //
    // SAMPLE REQUEST:
    //   POST /api/studentuser
    //   {
    //     "userId": 42,
    //     "languagePreference": "ar"
    //   }
    //
    // SAMPLE RESPONSE (201 Created):
    //   {
    //     "success": true,
    //     "message": "Your student account has been created successfully",
    //     "data": {
    //       "id": 1,
    //       "userId": 42,
    //       "studentAccountCode": "STU4X7B2KM",
    //       "fullName": "محمد أحمد",
    //       "phoneNumber": "01012345678",
    //       "languagePreference": "ar",
    //       "accountStatus": "Active",
    //       "isFirstLogin": true,
    //       "linkedTeacherCount": 0
    //     }
    //   }
    //
    // ══════════════════════════════════════════════════════════════════════════
    [HttpPost]
    [ProducesResponseType(typeof(object), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(object), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> InitializeStudentUser([FromBody] CreateStudentUserDto dto)
    {
        var result = await _studentUserService.InitializeStudentUserAsync(dto);
        return ToResponse(result);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // ENDPOINT 2: GET STUDENT USER PROFILE
    // ══════════════════════════════════════════════════════════════════════════
    //
    // WHAT IT DOES:
    //   Returns the full student user profile including account code, status,
    //   first-login flag, and the count of linked teachers.
    //
    // VALIDATIONS:
    //   - studentUserId must exist and not be soft-deleted (404 if not found)
    //
    // SAMPLE REQUEST:
    //   GET /api/studentuser/1
    //
    // SAMPLE RESPONSE (200 OK):
    //   {
    //     "success": true,
    //     "message": "Done successfully",
    //     "data": {
    //       "id": 1,
    //       "userId": 42,
    //       "studentAccountCode": "STU4X7B2KM",
    //       "fullName": "محمد أحمد",
    //       "email": "mohamed@gmail.com",
    //       "phoneNumber": "01012345678",
    //       "languagePreference": "ar",
    //       "accountStatus": "Active",
    //       "isFirstLogin": false,
    //       "linkedTeacherCount": 2
    //     }
    //   }
    //
    // ══════════════════════════════════════════════════════════════════════════
    [HttpGet("{studentUserId:long}")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetStudentUserProfile([FromRoute] long studentUserId)
    {
        var result = await _studentUserService.GetStudentUserProfileAsync(studentUserId);
        return ToResponse(result);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // ENDPOINT 3: UPDATE STUDENT USER PROFILE
    // ══════════════════════════════════════════════════════════════════════════
    //
    // WHAT IT DOES:
    //   Updates the student's own profile settings. Currently only supports
    //   language preference. FullName, PhoneNumber, Email, Password updates
    //   are handled by the User module (they live on the shared User record).
    //
    // WHAT IT UPDATES IN THE DATABASE:
    //   1. StudentUsers table: LanguagePreference
    //
    // VALIDATIONS:
    //   - studentUserId must exist (404 if not)
    //   - LanguagePreference must be "en" or "ar" if provided (400 if invalid)
    //
    // SAMPLE REQUEST:
    //   PUT /api/studentuser/1/profile
    //   {
    //     "languagePreference": "en"
    //   }
    //
    // SAMPLE RESPONSE (200 OK):
    //   {
    //     "success": true,
    //     "message": "Profile updated successfully",
    //     "data": { ... full profile DTO ... }
    //   }
    //
    // ══════════════════════════════════════════════════════════════════════════
    [HttpPut("{studentUserId:long}/profile")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateStudentUserProfile(
        [FromRoute] long studentUserId,
        [FromBody] UpdateStudentUserProfileDto dto)
    {
        var result = await _studentUserService.UpdateStudentUserProfileAsync(studentUserId, dto);
        return ToResponse(result);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // ENDPOINT 4: GET DASHBOARD
    // ══════════════════════════════════════════════════════════════════════════
    //
    // WHAT IT DOES:
    //   Returns the student's dashboard state: first-login flag, account code,
    //   and the list of linked teachers with their names, subjects, and
    //   visibility settings.
    //
    //   AAM-FR-05.4: If IsFirstLogin is true, the UI should show an empty
    //   dashboard with a prompt to add a Teacher.
    //
    // VALIDATIONS:
    //   - studentUserId must exist (404 if not)
    //
    // SAMPLE REQUEST:
    //   GET /api/studentuser/1/dashboard
    //
    // SAMPLE RESPONSE (200 OK):
    //   {
    //     "success": true,
    //     "message": "Done successfully",
    //     "data": {
    //       "isFirstLogin": false,
    //       "studentAccountCode": "STU4X7B2KM",
    //       "linkedTeachers": [
    //         {
    //           "linkId": 1,
    //           "teacherCode": "48291057",
    //           "teacherFullName": "Ahmed Mohamed",
    //           "subjectName": "Mathematics",
    //           "linkedAt": "2026-03-15T10:30:00Z",
    //           "isEnrollmentActive": true,
    //           "visibilityAttendance": true,
    //           "visibilityPayment": true,
    //           "visibilityHomework": true,
    //           "visibilityExamDefault": false
    //         }
    //       ]
    //     }
    //   }
    //
    // ══════════════════════════════════════════════════════════════════════════
    [HttpGet("{studentUserId:long}/dashboard")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetDashboard([FromRoute] long studentUserId)
    {
        var result = await _studentUserService.GetDashboardAsync(studentUserId);
        return ToResponse(result);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // ENDPOINT 5: LINK TEACHER
    // ══════════════════════════════════════════════════════════════════════════
    //
    // WHAT IT DOES:
    //   Links a Teacher to the student's dashboard by validating all three
    //   credentials required by AAM-FR-05.5:
    //     1. Teacher's unique 8-digit TeacherCode
    //     2. The student's unique code as assigned by the Teacher
    //     3. The hash/token generated for that student under that Teacher
    //
    //   Upon success, the teacher entry appears on the student's dashboard
    //   with the teacher's full name and subject name (AAM-FR-05.6).
    //
    // WHAT IT CREATES IN THE DATABASE:
    //   1. One row in StudentTeacherLinks table
    //   2. Updates StudentUsers.IsFirstLogin to false (if first link)
    //
    // VALIDATIONS:
    //   - studentUserId must exist (404 if not)
    //   - TeacherCode must be exactly 8 digits (400 if invalid)
    //   - TeacherCode must match an active teacher (404 if not found)
    //   - Must not already be linked to this teacher (409 if duplicate)
    //   - StudentCode + HashedToken must match a record under that teacher (400 if invalid)
    //
    // SAMPLE REQUEST:
    //   POST /api/studentuser/1/teachers
    //   {
    //     "teacherCode": "48291057",
    //     "studentCode": "A1",
    //     "hashedToken": "abc123xyz"
    //   }
    //
    // SAMPLE RESPONSE (201 Created):
    //   {
    //     "success": true,
    //     "message": "Teacher added successfully",
    //     "data": {
    //       "linkId": 1,
    //       "teacherCode": "48291057",
    //       "teacherFullName": "Ahmed Mohamed",
    //       "subjectName": "Mathematics",
    //       "linkedAt": "2026-03-28T14:00:00Z",
    //       "isEnrollmentActive": true,
    //       "visibilityAttendance": true,
    //       "visibilityPayment": true,
    //       "visibilityHomework": true,
    //       "visibilityExamDefault": false
    //     }
    //   }
    //
    // ══════════════════════════════════════════════════════════════════════════
    [HttpPost("{studentUserId:long}/teachers")]
    [ProducesResponseType(typeof(object), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(object), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> LinkTeacher(
        [FromRoute] long studentUserId,
        [FromBody] LinkTeacherDto dto)
    {
        var result = await _studentUserService.LinkTeacherAsync(studentUserId, dto);
        return ToResponse(result);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // ENDPOINT 6: UNLINK TEACHER
    // ══════════════════════════════════════════════════════════════════════════
    //
    // WHAT IT DOES:
    //   Removes a Teacher from the student's dashboard. This is a soft-unlink:
    //   the StudentTeacherLink record is preserved with LinkStatus = Unlinked
    //   and an UnlinkedAt timestamp for audit purposes.
    //
    // WHAT IT UPDATES IN THE DATABASE:
    //   1. StudentTeacherLinks table: LinkStatus → Unlinked, UnlinkedAt → now
    //
    // VALIDATIONS:
    //   - An active link must exist between this student and teacher (404 if not)
    //
    // SAMPLE REQUEST:
    //   DELETE /api/studentuser/1/teachers/5
    //
    // SAMPLE RESPONSE (200 OK):
    //   {
    //     "success": true,
    //     "message": "Teacher removed from your dashboard",
    //     "data": true
    //   }
    //
    // ══════════════════════════════════════════════════════════════════════════
    [HttpDelete("{studentUserId:long}/teachers/{teacherId:long}")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UnlinkTeacher(
        [FromRoute] long studentUserId,
        [FromRoute] long teacherId)
    {
        var result = await _studentUserService.UnlinkTeacherAsync(studentUserId, teacherId);
        return ToResponse(result);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // ENDPOINT 7: GET LINKED TEACHERS
    // ══════════════════════════════════════════════════════════════════════════
    //
    // WHAT IT DOES:
    //   Returns all teachers currently linked to this student's dashboard.
    //   Only active links are returned (LinkStatus = Active).
    //   Each entry includes the teacher's name, subject, and visibility settings
    //   as configured by the teacher (AAM-FR-04.8 / AAM-FR-05.8).
    //
    // VALIDATIONS:
    //   - studentUserId must exist (404 if not)
    //
    // SAMPLE REQUEST:
    //   GET /api/studentuser/1/teachers
    //
    // SAMPLE RESPONSE (200 OK):
    //   {
    //     "success": true,
    //     "message": "Done successfully",
    //     "data": [ ... array of StudentDashboardTeacherDto ... ]
    //   }
    //
    // ══════════════════════════════════════════════════════════════════════════
    [HttpGet("{studentUserId:long}/teachers")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetLinkedTeachers([FromRoute] long studentUserId)
    {
        var result = await _studentUserService.GetLinkedTeachersAsync(studentUserId);
        return ToResponse(result);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // ENDPOINT 8: GET STUDENT USER BY ACCOUNT CODE
    // ══════════════════════════════════════════════════════════════════════════
    //
    // WHAT IT DOES:
    //   Looks up a student user by their unique StudentAccountCode.
    //   Used by the Parent module when linking to a child's account
    //   (AAM-FR-06.3 Method A: Parent scans or enters the Student Code).
    //
    //   Returns the student's basic profile info (name, account code).
    //   Does NOT expose internal IDs, email, or sensitive data.
    //
    // VALIDATIONS:
    //   - accountCode must not be empty (400 if blank)
    //   - Must match an active student user (404 if not found)
    //
    // SAMPLE REQUEST:
    //   GET /api/studentuser/by-code/STU4X7B2KM
    //
    // SAMPLE RESPONSE (200 OK):
    //   {
    //     "success": true,
    //     "message": "Done successfully",
    //     "data": {
    //       "id": 1,
    //       "userId": 42,
    //       "studentAccountCode": "STU4X7B2KM",
    //       "fullName": "محمد أحمد",
    //       "accountStatus": "Active",
    //       "isFirstLogin": false,
    //       "linkedTeacherCount": 2
    //     }
    //   }
    //
    // ══════════════════════════════════════════════════════════════════════════
    [HttpGet("by-code/{accountCode}")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetStudentUserByAccountCode([FromRoute] string accountCode)
    {
        var result = await _studentUserService.GetStudentUserByAccountCodeAsync(accountCode);
        return ToResponse(result);
    }
}