using Edvanz.Domain.Resources;
using Microsoft.Extensions.Localization;
using System.Net;
using Edvanz.API.Attributes;
using Edvanz.Application.Dtos.Attendance;
using Edvanz.Application.IservicesContract;
using Edvanz.Application.ServiceContract;
using Edvanz.Domain.Enums;
using Edvanz.Domain.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Edvanz.API.Controllers;

/// <summary>
/// Attendance Module (Module 3) — parent-facing read endpoints.
///
/// SEPARATE CONTROLLER RATIONALE: like <see cref="StudentAttendanceController"/>,
/// parents carry no module claim and the service needs (teacherId, teacherStudentId).
/// The parent path additionally resolves WHICH child, then branches on how the child
/// is linked (AAM-FR-06.3).
///
/// AUTH: [ModulePermission(roles: ["Parent"], roleOnly: true)] — parent role only.
///
/// SECURITY: route childId and teacherId are untrusted. Before any service call:
///   1. JWT User.Id → ParentUser.
///   2. (parentUserId, childId) → active ParentChild, scoped to THIS parent — a
///      parent can never read another parent's child (REQ-USR-NFR-003 / AAM-BR-07).
///   3. Resolve teacherStudentId for the named teacher:
///        Method A (StudentAccount): child.StudentUserId → active StudentTeacherLink.
///        Method B (ManualProfile):  active ParentChildTeacherLink(childId, teacherId).
/// A parent reads attendance only for their own children, only under teachers actually
/// linked to that child (REQ-ATT-NFR-003). Teacher-controlled parent visibility
/// (AAM-FR-04.9) is enforced in the service via ContentViewerType.Parent.
/// </summary>
[Route("api/attendance/parent")]
[Authorize]
public sealed class ParentAttendanceController : ParentScopedApiBaseController
{
    private readonly IAttendanceService _attendanceService;

    public ParentAttendanceController(
        IAttendanceService attendanceService,
        ICurrentUserService currentUser,
        IUnitOfWork unitOfWork, IStringLocalizer<Messages> localizer)
        : base(currentUser, unitOfWork, localizer)
    {
        _attendanceService = attendanceService;
    }

    // ──────────────────────────────────────────────────────────────────────
    // CHILD ATTENDANCE SUMMARY (parent view)
    // GET /api/attendance/parent/children/{childId}/teachers/{teacherId}/summary
    // ──────────────────────────────────────────────────────────────────────
    [HttpGet("children/{childId:long}/teachers/{teacherId:long}/summary")]
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
            teacherId, resolution.TeacherStudentId!.Value, ContentViewerType.Parent);
        return ToResponse(result);
    }

    // ──────────────────────────────────────────────────────────────────────
    // CHILD ATTENDANCE — ONE MONTH (parent view)
    // GET /api/attendance/parent/children/{childId}/teachers/{teacherId}/month?year=&month=
    // ──────────────────────────────────────────────────────────────────────
    [HttpGet("children/{childId:long}/teachers/{teacherId:long}/month")]
    [ModulePermission(roles: new[] { "Parent" }, roleOnly: true)]
    [ProducesResponseType(typeof(Edvanz.Application.Dtos.Result<Edvanz.Application.Dtos.Attendance.MonthlyAttendanceSummaryDto>), StatusCodes.Status200OK)]
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
            teacherId, resolution.TeacherStudentId!.Value, request, ContentViewerType.Parent);
        return ToResponse(result);
    }

}