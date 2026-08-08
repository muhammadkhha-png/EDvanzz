using System.Net;
using Edvanz.API.Attributes;
using Edvanz.Application.Dtos.Attendance;
using Edvanz.Application.Dtos.ParentUser;
using Edvanz.Application.IservicesContract;
using Edvanz.Application.ServiceContract;
using Edvanz.Domain.Enums;
using Edvanz.Domain.Interfaces;
using Edvanz.Domain.Resources;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;

namespace Edvanz.API.Controllers;

/// <summary>
/// Consolidated API surface for the Parent module — profile, child management, teacher
/// linking, and every per-relationship read (attendance, payments, and the cross-module
/// dashboard) live under this single controller.
///
/// HARD CUTOVER (Parent Module requirements, decision B4): this REPLACES
/// <c>ParentUserController</c>, <c>ParentAttendanceController</c>, and
/// <c>ParentPaymentController</c> — those three are removed, not deprecated aliases. All
/// business logic, authorization, and response shapes are preserved from those controllers
/// unchanged; only the route surface and the (previously triplicated)
/// caller-resolution helpers are consolidated.
///
/// SECURITY — tenant isolation (unchanged from the controllers this replaces): the acting
/// parent is ALWAYS resolved from the JWT (User.Id → active <c>ParentUser</c>) via
/// <see cref="ResolveParentUserIdAsync"/>. Any <c>childId</c> in a route is scoped to the
/// JWT-resolved parent inside the service layer — a caller can never read or mutate another
/// parent's data by supplying a different id. Role gated to Parent throughout.
///
/// CODE-BASED DASHBOARD (Parent Module requirements §3/§9): the one genuinely new endpoint,
/// <see cref="GetTeacherDashboard"/>, is keyed by (TeacherCode, StudentCode) rather than
/// internal ids — per decision, codes are pure address resolution and the same ownership gate
/// still applies underneath (see <see cref="IParentDashboardService"/>). Every other endpoint
/// keeps its existing (childId, teacherId) shape unchanged, per "preserve HTTP methods and
/// request/response semantics where possible."
/// </summary>
[Route("api/parent")]
[Authorize]
public sealed class ParentController : ApiBaseController
{
    private readonly IParentUserService _parentUserService;
    private readonly IParentDashboardService _parentDashboardService;
    private readonly IAttendanceService _attendanceService;
    private readonly IPaymentService _paymentService;
    private readonly ICurrentUserService _currentUser;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IStringLocalizer<Messages> _localizer;

    public ParentController(
        IParentUserService parentUserService,
        IParentDashboardService parentDashboardService,
        IAttendanceService attendanceService,
        IPaymentService paymentService,
        ICurrentUserService currentUser,
        IUnitOfWork unitOfWork,
        IStringLocalizer<Messages> localizer)
    {
        _parentUserService = parentUserService;
        _parentDashboardService = parentDashboardService;
        _attendanceService = attendanceService;
        _paymentService = paymentService;
        _currentUser = currentUser;
        _unitOfWork = unitOfWork;
        _localizer = localizer;
    }

    // ══════════════════════════════════════════════════════════════════════════
    // PROFILE  (formerly ParentUserController endpoints 1–3)
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Initializes the ParentUser record. Called AFTER the User module creates a User with
    /// UserType = Parent.
    /// POST /api/parent
    /// </summary>
    [HttpPost]
    [ModulePermission(roles: new[] { "Parent" }, roleOnly: true)]
    [ProducesResponseType(typeof(Edvanz.Application.Dtos.Result<ParentUserProfileDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(object), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> InitializeParentUser([FromBody] CreateParentUserDto dto)
    {
        // Tenant isolation: the ParentUser is always created for the AUTHENTICATED user.
        long? userId = _currentUser.UserId;
        if (userId is null) return UserNotResolved();
        dto.UserId = userId.Value;

        var result = await _parentUserService.InitializeParentUserAsync(dto);
        return ToResponse(result);
    }

    /// <summary>
    /// GET /api/parent/profile
    /// </summary>
    [HttpGet("profile")]
    [ModulePermission(roles: new[] { "Parent" }, roleOnly: true)]
    [ProducesResponseType(typeof(Edvanz.Application.Dtos.Result<ParentUserProfileDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetProfile()
    {
        long? resolvedParentId = await ResolveParentUserIdAsync();
        if (resolvedParentId is null) return ParentNotResolved();

        var result = await _parentUserService.GetParentUserProfileAsync(resolvedParentId.Value);
        return ToResponse(result);
    }

    /// <summary>
    /// Updates language preference. name/phone/password go through the User module.
    /// PUT /api/parent/profile
    /// </summary>
    [HttpPut("profile")]
    [ModulePermission(roles: new[] { "Parent" }, roleOnly: true)]
    [ProducesResponseType(typeof(Edvanz.Application.Dtos.Result<ParentUserProfileDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateProfile([FromBody] UpdateParentUserProfileDto dto)
    {
        long? resolvedParentId = await ResolveParentUserIdAsync();
        if (resolvedParentId is null) return ParentNotResolved();

        var result = await _parentUserService.UpdateParentUserProfileAsync(resolvedParentId.Value, dto);
        return ToResponse(result);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // CHILDREN  (formerly ParentUserController endpoints 4–10)
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Returns all children with their linked teachers and visibility settings.
    /// GET /api/parent/dashboard
    /// </summary>
    [HttpGet("dashboard")]
    [ModulePermission(roles: new[] { "Parent" }, roleOnly: true)]
    [ProducesResponseType(typeof(Edvanz.Application.Dtos.Result<ParentDashboardDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetDashboard()
    {
        long? resolvedParentId = await ResolveParentUserIdAsync();
        if (resolvedParentId is null) return ParentNotResolved();

        var result = await _parentUserService.GetDashboardAsync(resolvedParentId.Value);
        return ToResponse(result);
    }

    /// <summary>
    /// Method A: child has a Student User account. Parent scans or enters the
    /// StudentAccountCode. Inherits all teachers already linked to that student.
    /// POST /api/parent/children/by-code
    /// </summary>
    [HttpPost("children/by-code")]
    [ModulePermission(roles: new[] { "Parent" }, roleOnly: true)]
    [ProducesResponseType(typeof(Edvanz.Application.Dtos.Result<ParentChildDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(object), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> AddChildByAccountCode([FromBody] AddChildByAccountCodeDto dto)
    {
        long? resolvedParentId = await ResolveParentUserIdAsync();
        if (resolvedParentId is null) return ParentNotResolved();

        var result = await _parentUserService.AddChildByAccountCodeAsync(resolvedParentId.Value, dto);
        return ToResponse(result);
    }

    /// <summary>
    /// Method B: child has no account. Parent enters the child's name manually. Teachers are
    /// added separately via <see cref="LinkTeacherToChild"/>.
    /// POST /api/parent/children/manual
    /// </summary>
    [HttpPost("children/manual")]
    [ModulePermission(roles: new[] { "Parent" }, roleOnly: true)]
    [ProducesResponseType(typeof(Edvanz.Application.Dtos.Result<ParentChildDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AddChildManual([FromBody] AddChildManualDto dto)
    {
        long? resolvedParentId = await ResolveParentUserIdAsync();
        if (resolvedParentId is null) return ParentNotResolved();

        var result = await _parentUserService.AddChildManualAsync(resolvedParentId.Value, dto);
        return ToResponse(result);
    }

    /// <summary>
    /// Returns a single child with their linked teachers.
    /// GET /api/parent/children/{childId}
    /// </summary>
    [HttpGet("children/{childId:long}")]
    [ModulePermission(roles: new[] { "Parent" }, roleOnly: true)]
    [ProducesResponseType(typeof(Edvanz.Application.Dtos.Result<ParentChildDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetChild([FromRoute] long childId)
    {
        long? resolvedParentId = await ResolveParentUserIdAsync();
        if (resolvedParentId is null) return ParentNotResolved();

        var result = await _parentUserService.GetChildAsync(resolvedParentId.Value, childId);
        return ToResponse(result);
    }

    /// <summary>
    /// Soft-deactivates the child link. Preserves the record for audit.
    /// DELETE /api/parent/children/{childId}
    /// </summary>
    [HttpDelete("children/{childId:long}")]
    [ModulePermission(roles: new[] { "Parent" }, roleOnly: true)]
    [ProducesResponseType(typeof(Edvanz.Application.Dtos.Result<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RemoveChild([FromRoute] long childId)
    {
        long? resolvedParentId = await ResolveParentUserIdAsync();
        if (resolvedParentId is null) return ParentNotResolved();

        var result = await _parentUserService.RemoveChildAsync(resolvedParentId.Value, childId);
        return ToResponse(result);
    }

    /// <summary>
    /// Method B only: links a Teacher to a manual child profile using TeacherCode + StudentCode
    /// + HashedToken. NOT allowed for Method A children (their teachers come from
    /// StudentTeacherLink).
    /// POST /api/parent/children/{childId}/teachers
    /// </summary>
    [HttpPost("children/{childId:long}/teachers")]
    [ModulePermission(roles: new[] { "Parent" }, roleOnly: true)]
    [ProducesResponseType(typeof(Edvanz.Application.Dtos.Result<ParentChildTeacherDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(object), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> LinkTeacherToChild(
        [FromRoute] long childId,
        [FromBody] LinkTeacherToChildDto dto)
    {
        long? resolvedParentId = await ResolveParentUserIdAsync();
        if (resolvedParentId is null) return ParentNotResolved();

        var result = await _parentUserService.LinkTeacherToChildAsync(resolvedParentId.Value, childId, dto);
        return ToResponse(result);
    }

    /// <summary>
    /// Method B only: removes a Teacher from a manual child profile (soft-unlink). Does NOT
    /// touch the underlying student-teacher relationship — only the parent's own visibility of it.
    /// DELETE /api/parent/children/{childId}/teachers/{teacherId}
    /// </summary>
    [HttpDelete("children/{childId:long}/teachers/{teacherId:long}")]
    [ModulePermission(roles: new[] { "Parent" }, roleOnly: true)]
    [ProducesResponseType(typeof(Edvanz.Application.Dtos.Result<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UnlinkTeacherFromChild(
        [FromRoute] long childId,
        [FromRoute] long teacherId)
    {
        long? resolvedParentId = await ResolveParentUserIdAsync();
        if (resolvedParentId is null) return ParentNotResolved();

        var result = await _parentUserService.UnlinkTeacherFromChildAsync(resolvedParentId.Value, childId, teacherId);
        return ToResponse(result);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // ATTENDANCE  (formerly ParentAttendanceController)
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// GET /api/parent/children/{childId}/teachers/{teacherId}/attendance/summary
    /// </summary>
    [HttpGet("children/{childId:long}/teachers/{teacherId:long}/attendance/summary")]
    [ModulePermission(roles: new[] { "Parent" }, roleOnly: true)]
    [ProducesResponseType(typeof(Edvanz.Application.Dtos.Result<Edvanz.Application.Dtos.Attendance.StudentAttendanceSummaryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(object), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetChildAttendanceSummary(
        [FromRoute] long childId,
        [FromRoute] long teacherId)
    {
        var resolution = await ResolveChildForParentAsync(childId, teacherId);
        if (resolution.ErrorResponse is not null) return resolution.ErrorResponse;

        var result = await _attendanceService.GetStudentViewAttendanceSummaryAsync(
            teacherId, resolution.TeacherStudentId!.Value, AttendanceViewerType.Parent);
        return ToResponse(result);
    }

    /// <summary>
    /// GET /api/parent/children/{childId}/teachers/{teacherId}/attendance/month?year=&amp;month=
    /// </summary>
    [HttpGet("children/{childId:long}/teachers/{teacherId:long}/attendance/month")]
    [ModulePermission(roles: new[] { "Parent" }, roleOnly: true)]
    [ProducesResponseType(typeof(Edvanz.Application.Dtos.Result<MonthlyAttendanceSummaryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(object), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetChildAttendanceMonth(
        [FromRoute] long childId,
        [FromRoute] long teacherId,
        [FromQuery] StudentTimelineMonthRequest request)
    {
        var resolution = await ResolveChildForParentAsync(childId, teacherId);
        if (resolution.ErrorResponse is not null) return resolution.ErrorResponse;

        var result = await _attendanceService.GetStudentViewAttendanceAsync(
            teacherId, resolution.TeacherStudentId!.Value, request, AttendanceViewerType.Parent);
        return ToResponse(result);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // PAYMENTS  (formerly ParentPaymentController)
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Whole Payment screen for the chosen child under the chosen teacher.
    /// GET /api/parent/children/{childId}/teachers/{teacherId}/payments/tracking
    /// </summary>
    [HttpGet("children/{childId:long}/teachers/{teacherId:long}/payments/tracking")]
    [ModulePermission(roles: new[] { "Parent" }, roleOnly: true)]
    [ProducesResponseType(typeof(Edvanz.Application.Dtos.Result<Edvanz.Application.Dtos.Payment.StudentPaymentTrackingDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(object), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetChildPaymentTracking(
        [FromRoute] long childId,
        [FromRoute] long teacherId)
    {
        var resolution = await ResolveChildForParentAsync(childId, teacherId);
        if (resolution.ErrorResponse is not null) return resolution.ErrorResponse;

        var result = await _paymentService.GetStudentPaymentTrackingAsync(
            teacherId, resolution.TeacherStudentId!.Value, PaymentViewerType.Parent);
        return ToResponse(result);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // CONSOLIDATED TEACHER DASHBOARD  (NEW — Parent Module requirements §3/§9)
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Videos / Online Exams / Offline Exams / Homework / Attendance / Payments for one child
    /// under one teacher, in a single call. Resolved by (TeacherCode, StudentCode) — see
    /// <see cref="IParentDashboardService"/> for the ownership guarantee behind the codes.
    /// GET /api/parent/teachers/{teacherCode}/students/{studentCode}/dashboard
    /// </summary>
    [HttpGet("teachers/{teacherCode}/students/{studentCode}/dashboard")]
    [ModulePermission(roles: new[] { "Parent" }, roleOnly: true)]
    [ProducesResponseType(typeof(Edvanz.Application.Dtos.Result<ParentChildTeacherDashboardDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(object), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetTeacherDashboard(
        [FromRoute] string teacherCode,
        [FromRoute] string studentCode)
    {
        long? resolvedParentId = await ResolveParentUserIdAsync();
        if (resolvedParentId is null) return ParentNotResolved();

        var result = await _parentDashboardService.GetTeacherDashboardAsync(
            resolvedParentId.Value, teacherCode, studentCode);
        return ToResponse(result);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // PRIVATE HELPERS
    // Consolidates the resolution logic previously triplicated across ParentUserController /
    // ParentAttendanceController / ParentPaymentController into one place.
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Resolves the acting parent's <c>ParentUser.Id</c> from the JWT
    /// (<c>User.Id</c> → active <c>ParentUser</c>).
    /// </summary>
    private async Task<long?> ResolveParentUserIdAsync()
    {
        long? userId = _currentUser.UserId;
        if (userId is null) return null;

        var parentUser = await _unitOfWork.Users.GetActiveParentUserByUserIdAsync(userId.Value);
        return parentUser?.Id;
    }

    private IActionResult ParentNotResolved() =>
        new ObjectResult(new { success = false, message = "Parent could not be resolved from token." })
        { StatusCode = StatusCodes.Status404NotFound };

    /// <summary>
    /// Resolves the named child's TeacherStudent.Id under the named teacher, for the calling
    /// parent. Verifies parent ownership of the child, then branches on link method
    /// (AAM-FR-06.3). Returns either the resolved id or a 401/403/404 result. Used by every
    /// (childId, teacherId)-keyed endpoint above — identical logic to what
    /// ParentAttendanceController and ParentPaymentController each carried separately before
    /// consolidation.
    /// </summary>
    private async Task<ChildResolution> ResolveChildForParentAsync(long childId, long teacherId)
    {
        long? userId = _currentUser.UserId;
        if (userId is null)
            return ChildResolution.Error(Unauthorized());

        var parentUser = await _unitOfWork.Users.GetActiveParentUserByUserIdAsync(userId.Value);
        if (parentUser is null)
            return ChildResolution.Error(NotFoundError("ParentUserNotFound"));

        var child = await _unitOfWork.Users.GetActiveChildAsync(parentUser.Id, childId);
        if (child is null)
            return ChildResolution.Error(NotFoundError("ChildNotFound"));

        // Method A — child has a StudentUser account: reuse the student-teacher link.
        if (child.LinkMethod == ChildLinkMethod.StudentAccount)
        {
            if (child.StudentUserId is null)
                return ChildResolution.Error(ForbiddenError("ChildEnrollmentRemoved"));

            var link = await _unitOfWork.Users
                .GetActiveStudentTeacherLinkAsync(child.StudentUserId.Value, teacherId);
            if (link is null || link.LinkStatus != LinkStatus.Active)
                return ChildResolution.Error(ForbiddenError("TeacherLinkNotFound"));
            if (link.TeacherStudentId is null)
                return ChildResolution.Error(ForbiddenError("StudentEnrollmentRemoved"));

            return ChildResolution.Ok(link.TeacherStudentId.Value);
        }

        // Method B — manual profile: teacher link lives on ParentChildTeacherLink.
        var parentLink = await _unitOfWork.Users
            .GetActiveParentChildTeacherLinkAsync(child.Id, teacherId);
        if (parentLink is null || parentLink.LinkStatus != LinkStatus.Active)
            return ChildResolution.Error(ForbiddenError("TeacherLinkNotFound"));
        if (parentLink.TeacherStudentId is null)
            return ChildResolution.Error(ForbiddenError("StudentEnrollmentRemoved"));

        return ChildResolution.Ok(parentLink.TeacherStudentId.Value);
    }

    private IActionResult NotFoundError(string message) =>
        new ObjectResult(new { success = false, code = message, message = _localizer[message].Value })
        {
            StatusCode = (int)HttpStatusCode.NotFound,
        };

    private IActionResult ForbiddenError(string message) =>
        new ObjectResult(new { success = false, code = message, message = _localizer[message].Value })
        {
            StatusCode = (int)HttpStatusCode.Forbidden,
        };

    private readonly struct ChildResolution
    {
        public long? TeacherStudentId { get; }
        public IActionResult? ErrorResponse { get; }

        private ChildResolution(long? id, IActionResult? error)
        {
            TeacherStudentId = id;
            ErrorResponse = error;
        }

        public static ChildResolution Ok(long teacherStudentId) => new(teacherStudentId, null);
        public static ChildResolution Error(IActionResult response) => new(null, response);
    }
}
