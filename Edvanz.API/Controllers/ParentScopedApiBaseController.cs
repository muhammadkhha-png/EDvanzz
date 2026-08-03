using System.Net;
using Edvanz.Application.IservicesContract;
using Edvanz.Domain.Enums;
using Edvanz.Domain.Interfaces;
using Edvanz.Domain.Resources;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;

namespace Edvanz.API.Controllers;

/// <summary>
/// Shared base for the parent-facing controllers (<see cref="ParentUserController"/>,
/// <see cref="ParentAttendanceController"/>, <see cref="ParentPaymentController"/>).
/// Centralizes JWT-to-parent resolution and the child/teacher-link resolution branch
/// (AAM-FR-06.3, Method A / Method B) that was previously copy-pasted across all three
/// controllers.
///
/// SCOPE (D2 — locked decision, parent-parity phase plan): this consolidation is
/// parent-side ONLY. The equivalent student-side duplication (StudentAttendanceController,
/// StudentVideosController, StudentOnlineExamsController, StudentAssignmentObligationsController)
/// is deliberately left untouched — separate ticket.
///
/// Inherits <see cref="ApiBaseController"/> so <c>ToResponse&lt;T&gt;</c> and the inherited
/// <c>[ApiController] / [Route("api/[controller]")]</c> attributes still apply.
/// </summary>
public abstract class ParentScopedApiBaseController : ApiBaseController
{
    private readonly ICurrentUserService _currentUser;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IStringLocalizer<Messages> _localizer;

    protected ParentScopedApiBaseController(
        ICurrentUserService currentUser,
        IUnitOfWork unitOfWork,
        IStringLocalizer<Messages> localizer)
    {
        _currentUser = currentUser;
        _unitOfWork = unitOfWork;
        _localizer = localizer;
    }

    /// <summary>
    /// Resolves the acting parent's <c>ParentUser.Id</c> from the JWT
    /// (<c>User.Id</c> → active <c>ParentUser</c>). Returns null when the caller is not an
    /// active parent — any route <c>{parentUserId}</c> segment is deliberately never consulted.
    /// </summary>
    protected async Task<long?> ResolveParentUserIdAsync()
    {
        long? userId = _currentUser.UserId;
        if (userId is null) return null;

        var parentUser = await _unitOfWork.Users.GetActiveParentUserByUserIdAsync(userId.Value);
        return parentUser?.Id;
    }

    /// <summary>
    /// Resolves the named child's TeacherStudent.Id under the named teacher, for the calling
    /// parent. Verifies parent ownership of the child, then branches on link method
    /// (AAM-FR-06.3). Returns either the resolved id or a 401/403/404 result.
    /// </summary>
    protected async Task<ChildResolution> ResolveChildForParentAsync(long childId, long teacherId)
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

    /// <summary>
    /// Returns the calling user's id straight from the JWT, or null if unresolvable.
    /// Used by endpoints that must read the id BEFORE a ParentUser record exists —
    /// i.e. before <see cref="ResolveParentUserIdAsync"/> can succeed (parent
    /// self-initialization).
    /// </summary>
    protected long? GetCurrentUserId() => _currentUser.UserId;

    protected IActionResult ParentNotResolved() =>
        new ObjectResult(new { success = false, message = "Parent could not be resolved from token." })
        { StatusCode = StatusCodes.Status404NotFound };

    protected IActionResult NotFoundError(string message) =>
        new ObjectResult(new { success = false, code = message, message = _localizer[message].Value })
        {
            StatusCode = (int)HttpStatusCode.NotFound,
        };

    protected IActionResult ForbiddenError(string message) =>
        new ObjectResult(new { success = false, code = message, message = _localizer[message].Value })
        {
            StatusCode = (int)HttpStatusCode.Forbidden,
        };

    protected readonly struct ChildResolution
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