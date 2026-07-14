using Edvanz.API.Attributes;
using Edvanz.Application.Dtos;
using Edvanz.Application.IservicesContract;
using Edvanz.Application.ServiceContract;
using Edvanz.Domain.Enums;
using Edvanz.Domain.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Net;

namespace Edvanz.API.Controllers;

/// <summary>
/// Online Exam Module — student-facing endpoints (S1, S2, S6-read; S3/S4/S5 land
/// Phase 5). Separate controller from <see cref="OnlineExamsController"/> — same
/// rationale as <c>StudentVideosController</c> vs <c>VideosController</c>: auth is
/// role-only (no module claim for students), and teacherId comes from the route
/// (a student may link to multiple teachers), resolved to teacherStudentId via the
/// active StudentTeacherLink.
/// </summary>
[Route("api/online-exams/student")]
[Authorize]
public sealed class StudentOnlineExamsController : ApiBaseController
{
    private readonly IStudentOnlineExamService _service;
    private readonly ICurrentUserService _currentUser;
    private readonly IUnitOfWork _unitOfWork;

    public StudentOnlineExamsController(
        IStudentOnlineExamService service, ICurrentUserService currentUser, IUnitOfWork unitOfWork)
    {
        _service = service;
        _currentUser = currentUser;
        _unitOfWork = unitOfWork;
    }

    // S1 — GET /api/online-exams/student/teachers/{teacherId}
    [HttpGet("teachers/{teacherId:long}")]
    [ModulePermission(roles: new[] { "Student" }, roleOnly: true)]
    public async Task<IActionResult> GetMyExams([FromRoute] long teacherId)
    {
        var resolution = await ResolveStudentForTeacherAsync(teacherId);
        if (resolution.ErrorResponse is not null) return resolution.ErrorResponse;

        return ToResponse(await _service.GetMyExamsAsync(teacherId, resolution.TeacherStudentId!.Value));
    }

    // S2 — GET /api/online-exams/student/teachers/{teacherId}/{onlineExamId}/questions
    [HttpGet("teachers/{teacherId:long}/{onlineExamId:long}/questions")]
    [ModulePermission(roles: new[] { "Student" }, roleOnly: true)]
    public async Task<IActionResult> GetTakeScreen([FromRoute] long teacherId, [FromRoute] long onlineExamId)
    {
        var resolution = await ResolveStudentForTeacherAsync(teacherId);
        if (resolution.ErrorResponse is not null) return resolution.ErrorResponse;

        return ToResponse(await _service.GetTakeScreenAsync(teacherId, resolution.TeacherStudentId!.Value, onlineExamId));
    }

    // S6-read — GET /api/online-exams/student/teachers/{teacherId}/{onlineExamId}/answers
    [HttpGet("teachers/{teacherId:long}/{onlineExamId:long}/answers")]
    [ModulePermission(roles: new[] { "Student" }, roleOnly: true)]
    public async Task<IActionResult> GetReview([FromRoute] long teacherId, [FromRoute] long onlineExamId)
    {
        var resolution = await ResolveStudentForTeacherAsync(teacherId);
        if (resolution.ErrorResponse is not null) return resolution.ErrorResponse;

        return ToResponse(await _service.GetReviewAsync(teacherId, resolution.TeacherStudentId!.Value, onlineExamId));
    }

    // ══════════════════════════════════════════════════════════════════════
    // PRIVATE HELPERS — verbatim copy of StudentVideosController's resolution
    // pattern (studentUserId → active StudentTeacherLink(teacherId) → TeacherStudentId)
    // ══════════════════════════════════════════════════════════════════════

    private async Task<StudentResolution> ResolveStudentForTeacherAsync(long teacherId)
    {
        long? userId = _currentUser.UserId;
        if (userId is null)
            return StudentResolution.Error(Unauthorized());

        var studentUser = await _unitOfWork.Users.GetActiveStudentUserByUserIdAsync(userId.Value);
        if (studentUser is null)
            return StudentResolution.Error(NotFoundError("StudentUserNotFound"));

        var link = await _unitOfWork.Users.GetActiveStudentTeacherLinkAsync(studentUser.Id, teacherId);
        if (link is null || link.LinkStatus != LinkStatus.Active)
            return StudentResolution.Error(ForbiddenError("TeacherLinkNotFound"));

        if (link.TeacherStudentId is null)
            return StudentResolution.Error(ForbiddenError("StudentEnrollmentRemoved"));

        return StudentResolution.Ok(link.TeacherStudentId.Value);
    }

    private IActionResult NotFoundError(string message) =>
        new ObjectResult(new { success = false, message }) { StatusCode = (int)HttpStatusCode.NotFound };

    private IActionResult ForbiddenError(string message) =>
        new ObjectResult(new { success = false, message }) { StatusCode = (int)HttpStatusCode.Forbidden };

    private readonly struct StudentResolution
    {
        public long? TeacherStudentId { get; }
        public IActionResult? ErrorResponse { get; }

        private StudentResolution(long? id, IActionResult? error)
        {
            TeacherStudentId = id;
            ErrorResponse = error;
        }

        public static StudentResolution Ok(long teacherStudentId) => new(teacherStudentId, null);
        public static StudentResolution Error(IActionResult response) => new(null, response);
    }
    // S4 — POST /api/online-exams/student/teachers/{teacherId}/{onlineExamId}/answers
    [HttpPost("teachers/{teacherId:long}/{onlineExamId:long}/answers")]
    [ModulePermission(roles: new[] { "Student" }, roleOnly: true)]
    public async Task<IActionResult> SubmitAnswer(
        [FromRoute] long teacherId, [FromRoute] long onlineExamId, [FromBody] SubmitOnlineExamAnswerRequest request)
    {
        var resolution = await ResolveStudentForTeacherAsync(teacherId);
        if (resolution.ErrorResponse is not null) return resolution.ErrorResponse;

        return ToResponse(await _service.SubmitAnswerAsync(teacherId, resolution.TeacherStudentId!.Value, onlineExamId, request));
    }

    // S3 — POST /api/online-exams/student/teachers/{teacherId}/{onlineExamId}/submit
    [HttpPost("teachers/{teacherId:long}/{onlineExamId:long}/submit")]
    [ModulePermission(roles: new[] { "Student" }, roleOnly: true)]
    public async Task<IActionResult> SubmitExam(
        [FromRoute] long teacherId, [FromRoute] long onlineExamId, [FromBody] SubmitOnlineExamRequest request)
    {
        var resolution = await ResolveStudentForTeacherAsync(teacherId);
        if (resolution.ErrorResponse is not null) return resolution.ErrorResponse;

        return ToResponse(await _service.SubmitExamAsync(teacherId, resolution.TeacherStudentId!.Value, onlineExamId, request));
    }

    // S5 — GET /api/online-exams/student/teachers/{teacherId}/{onlineExamId}/result
    [HttpGet("teachers/{teacherId:long}/{onlineExamId:long}/result")]
    [ModulePermission(roles: new[] { "Student" }, roleOnly: true)]
    public async Task<IActionResult> GetResult([FromRoute] long teacherId, [FromRoute] long onlineExamId)
    {
        var resolution = await ResolveStudentForTeacherAsync(teacherId);
        if (resolution.ErrorResponse is not null) return resolution.ErrorResponse;

        return ToResponse(await _service.GetResultAsync(teacherId, resolution.TeacherStudentId!.Value, onlineExamId));
    }
}