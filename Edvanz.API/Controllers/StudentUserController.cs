using System.Net;
using Edvanz.API.Attributes;
using Edvanz.Application.Dtos.StudentUser;
using Edvanz.Application.IservicesContract;
using Edvanz.Application.ServiceContract;
using Edvanz.Domain.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Edvanz.API.Controllers;

/// <summary>
/// API endpoints for the Student User module (request/approval linking design).
///
/// IDENTITY: every "me" endpoint resolves the calling StudentUser from the JWT
/// (User.Id → StudentUsers.UserId) — the studentUserId is NEVER read from the
/// route or body (same IDOR-safe pattern as StudentAttendanceController /
/// StudentVideosController; the old route-id endpoints of this controller were
/// the last violators and have been removed).
///
/// LINKING: the student sends a link REQUEST (teacher code + own name + optional
/// student code); the teacher accepts or rejects it from the teacher app
/// (TeacherStudentLinksController). GET me/teachers surfaces the request state
/// (Pending/Active/Rejected/...) so the student always knows the outcome, and a
/// push notification is sent on accept/reject.
///
/// NOTE: StudentUser initialization is performed internally by the User module
/// during registration (UserService → IStudentUserService.InitializeStudentUserAsync);
/// it is intentionally no longer exposed as an HTTP endpoint.
///
/// All responses follow the unified JSON shape; messages localize via the
/// Accept-Language header ("ar" / "en").
/// </summary>
[Authorize]
public class StudentUserController : ApiBaseController
{
    private readonly IStudentUserService _studentUserService;
    private readonly ICurrentUserService _currentUser;
    private readonly IUnitOfWork _unitOfWork;

    public StudentUserController(
        IStudentUserService studentUserService,
        ICurrentUserService currentUser,
        IUnitOfWork unitOfWork)
    {
        _studentUserService = studentUserService;
        _currentUser = currentUser;
        _unitOfWork = unitOfWork;
    }

    // ══════════════════════════════════════════════════════════════════════════
    // ME: PROFILE
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>Returns the authenticated student's own profile.</summary>
    /// <response code="200">Profile including account code and linked-teacher count.</response>
    /// <response code="401">JWT missing or expired.</response>
    /// <response code="404">The authenticated user has no student account.</response>
    [HttpGet("me")]
    [ModulePermission(roles: new[] { "Student" }, roleOnly: true)]
    [ProducesResponseType(typeof(Edvanz.Application.Dtos.Result<Edvanz.Application.Dtos.StudentUser.StudentUserProfileDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetMyProfile()
    {
        var studentUserId = await ResolveStudentUserIdAsync();
        if (studentUserId is null) return StudentNotResolved();

        var result = await _studentUserService.GetStudentUserProfileAsync(studentUserId.Value);
        return ToResponse(result);
    }

    /// <summary>Updates the authenticated student's own profile settings (language preference).</summary>
    /// <response code="200">Updated profile.</response>
    /// <response code="400">Invalid language preference.</response>
    [HttpPut("me/profile")]
    [ModulePermission(roles: new[] { "Student" }, roleOnly: true)]
    [ProducesResponseType(typeof(Edvanz.Application.Dtos.Result<Edvanz.Application.Dtos.StudentUser.StudentUserProfileDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(object), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateMyProfile([FromBody] UpdateStudentUserProfileDto dto)
    {
        var studentUserId = await ResolveStudentUserIdAsync();
        if (studentUserId is null) return StudentNotResolved();

        var result = await _studentUserService.UpdateStudentUserProfileAsync(studentUserId.Value, dto);
        return ToResponse(result);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // ME: DASHBOARD + TEACHERS
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Returns the authenticated student's dashboard: first-login flag, account
    /// code, and one entry per teacher with the CURRENT link state
    /// (Pending / Active / Rejected / Unlinked / RemovedByTeacher / CancelledByStudent)
    /// plus the teacher's visibility flags (AAM-FR-05.8).
    /// </summary>
    /// <response code="200">Dashboard payload.</response>
    [HttpGet("me/dashboard")]
    [ModulePermission(roles: new[] { "Student" }, roleOnly: true)]
    [ProducesResponseType(typeof(Edvanz.Application.Dtos.Result<Edvanz.Application.Dtos.StudentUser.StudentDashboardDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetMyDashboard()
    {
        var studentUserId = await ResolveStudentUserIdAsync();
        if (studentUserId is null) return StudentNotResolved();

        var result = await _studentUserService.GetDashboardAsync(studentUserId.Value);
        return ToResponse(result);
    }

    /// <summary>
    /// Returns the authenticated student's teachers including request states —
    /// this is how the student sees whether a request is still Pending, was
    /// accepted (Active), or was rejected (Rejected).
    /// </summary>
    /// <response code="200">One entry per teacher (latest state), live entries first.</response>
    [HttpGet("me/teachers")]
    [ModulePermission(roles: new[] { "Student" }, roleOnly: true)]
    [ProducesResponseType(typeof(Edvanz.Application.Dtos.Result<System.Collections.Generic.List<Edvanz.Application.Dtos.StudentUser.StudentDashboardTeacherDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetMyTeachers()
    {
        var studentUserId = await ResolveStudentUserIdAsync();
        if (studentUserId is null) return StudentNotResolved();

        var result = await _studentUserService.GetMyTeachersAsync(studentUserId.Value);
        return ToResponse(result);
    }

    /// <summary>
    /// Sends a link REQUEST to a teacher. The student supplies the teacher's
    /// 8-digit code and their own name; the student code the teacher assigned
    /// them is optional and only helps the teacher match the request to a roster
    /// record. The link becomes usable ONLY after the teacher accepts.
    /// </summary>
    /// <response code="201">Request created (entry returned with status Pending).</response>
    /// <response code="400">Invalid teacher code or missing student name.</response>
    /// <response code="404">No active teacher with this code.</response>
    /// <response code="409">Already linked to this teacher, or a request is already pending.</response>
    [HttpPost("me/link-requests")]
    [ModulePermission(roles: new[] { "Student" }, roleOnly: true)]
    [ProducesResponseType(typeof(Edvanz.Application.Dtos.Result<Edvanz.Application.Dtos.StudentUser.StudentDashboardTeacherDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(object), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(object), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateLinkRequest([FromBody] CreateLinkRequestDto dto)
    {
        var studentUserId = await ResolveStudentUserIdAsync();
        if (studentUserId is null) return StudentNotResolved();

        var result = await _studentUserService.CreateLinkRequestAsync(studentUserId.Value, dto);
        return ToResponse(result);
    }

    /// <summary>
    /// Removes a teacher from the student's side: cancels a Pending request or
    /// unlinks an Active link (soft transition, preserved for audit). The student
    /// can send a new request to the same teacher afterwards.
    /// </summary>
    /// <response code="200">Cancelled or unlinked.</response>
    /// <response code="404">No pending request or active link with this teacher.</response>
    [HttpDelete("me/teachers/{teacherId:long}")]
    [ModulePermission(roles: new[] { "Student" }, roleOnly: true)]
    [ProducesResponseType(typeof(Edvanz.Application.Dtos.Result<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RemoveMyTeacher([FromRoute] long teacherId)
    {
        var studentUserId = await ResolveStudentUserIdAsync();
        if (studentUserId is null) return StudentNotResolved();

        // Non-null: ResolveStudentUserIdAsync already required a JWT user id
        long actingUserId = _currentUser.UserId!.Value;

        var result = await _studentUserService.UnlinkTeacherAsync(studentUserId.Value, teacherId, actingUserId);
        return ToResponse(result);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // LOOKUP BY ACCOUNT CODE (used by the Parent module, AAM-FR-06.3 Method A)
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Looks up a student user by their unique StudentAccountCode. Used by the
    /// Parent module when linking to a child's account — intentionally NOT
    /// restricted to the Student role.
    /// </summary>
    /// <response code="200">Basic student profile info.</response>
    /// <response code="400">Blank account code.</response>
    /// <response code="404">No active student with this code.</response>
    [HttpGet("by-code/{accountCode}")]
    [ProducesResponseType(typeof(Edvanz.Application.Dtos.Result<Edvanz.Application.Dtos.StudentUser.StudentUserProfileDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetStudentUserByAccountCode([FromRoute] string accountCode)
    {
        var result = await _studentUserService.GetStudentUserByAccountCodeAsync(accountCode);
        return ToResponse(result);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // PRIVATE HELPERS
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Resolves the calling user's StudentUser.Id from the JWT:
    /// User.Id → StudentUsers.UserId (active, non-deleted). Returns null when the
    /// JWT is missing a user id or the user has no student account.
    /// </summary>
    private async Task<long?> ResolveStudentUserIdAsync()
    {
        long? userId = _currentUser.UserId;
        if (userId is null) return null;

        var studentUser = await _unitOfWork.Users.GetActiveStudentUserByUserIdAsync(userId.Value);
        return studentUser?.Id;
    }

    /// <summary>
    /// Standardized response when the JWT cannot be resolved to a student account.
    /// </summary>
    private IActionResult StudentNotResolved() =>
        new ObjectResult(new { success = false, message = _resolveMessage })
        {
            StatusCode = (int)HttpStatusCode.NotFound
        };

    private const string _resolveMessage = "Student account not found for the authenticated user";
}