using System.Net;
using Edvanz.API.Attributes;
using Edvanz.Application.IservicesContract;
using Edvanz.Application.ServiceContract;
using Edvanz.Domain.Enums;
using Edvanz.Domain.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Edvanz.API.Controllers;

/// <summary>
/// Payment Module (Module 4) — parent-facing read endpoint.
///
/// SEPARATE CONTROLLER RATIONALE: like <see cref="ParentAttendanceController"/>, parents
/// carry no module claim and the service needs (teacherId, teacherStudentId). The parent
/// path additionally resolves WHICH child, then branches on how the child is linked
/// (AAM-FR-06.3).
///
/// ONE SCREEN = ONE CALL: the tracking endpoint returns the ENTIRE Payment screen for the
/// chosen child under the chosen teacher.
///
/// AUTH: [ModulePermission(roles: ["Parent"], roleOnly: true)] — parent role only.
///
/// SECURITY: route childId and teacherId are untrusted. Before the service call:
///   1. JWT User.Id → ParentUser.
///   2. (parentUserId, childId) → active ParentChild, scoped to THIS parent.
///   3. Resolve teacherStudentId for the named teacher:
///        Method A (StudentAccount): child.StudentUserId → active StudentTeacherLink.
///        Method B (ManualProfile):  active ParentChildTeacherLink(childId, teacherId).
/// Teacher-controlled parent visibility (ParentVisibilityPayment) is enforced in the
/// service via PaymentViewerType.Parent.
/// </summary>
[Route("api/payment/parent")]
[Authorize]
public sealed class ParentPaymentController : ApiBaseController
{
    private readonly IPaymentService _paymentService;
    private readonly ICurrentUserService _currentUser;
    private readonly IUnitOfWork _unitOfWork;

    public ParentPaymentController(
        IPaymentService paymentService,
        ICurrentUserService currentUser,
        IUnitOfWork unitOfWork)
    {
        _paymentService = paymentService;
        _currentUser = currentUser;
        _unitOfWork = unitOfWork;
    }

    // ──────────────────────────────────────────────────────────────────────
    // CHILD PAYMENT TRACKING (whole screen, parent view)
    // GET /api/payment/parent/children/{childId}/teachers/{teacherId}/tracking
    // ──────────────────────────────────────────────────────────────────────
    [HttpGet("children/{childId:long}/teachers/{teacherId:long}/tracking")]
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

    // ──────────────────────────────────────────────────────────────────────
    // PRIVATE HELPERS
    // Same shape as ParentAttendanceController, plus the Method-A/B branch. A shared
    // CallerScopedApiBaseController is the pending refactor to remove the duplication.
    // ──────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Resolves the named child's TeacherStudent.Id under the named teacher, for the calling
    /// parent. Verifies parent ownership of the child, then branches on link method
    /// (AAM-FR-06.3). Returns either the resolved id or a 401/403/404 result.
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
        new ObjectResult(new { success = false, message })
        {
            StatusCode = (int)HttpStatusCode.NotFound,
        };

    private IActionResult ForbiddenError(string message) =>
        new ObjectResult(new { success = false, message })
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
