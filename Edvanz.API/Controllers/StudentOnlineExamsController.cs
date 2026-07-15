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
/// Online Exam Module — student-facing endpoints (S1–S6). Separate controller from
/// <see cref="OnlineExamsController"/> — same rationale as <c>StudentVideosController</c>
/// vs <c>VideosController</c>: auth is role-only (no module claim for students), and
/// teacherId comes from the route (a student may link to multiple teachers), resolved to
/// teacherStudentId via the active StudentTeacherLink.
///
/// AUTH: every endpoint is <c>[ModulePermission(roles: ["Student"], roleOnly: true)]</c> —
/// role-gated only, no module/permission claim (students are never module-gated).
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

    /// <summary>
    /// S1 — Lists exams assigned to the calling student under the given teacher, split into
    /// <c>upcoming</c> (published, window open, not yet finalized) and <c>past</c> (finalized,
    /// or the window has closed).
    /// </summary>
    /// <param name="teacherId">The teacher whose exams are requested.</param>
    /// <response code="200">Upcoming/past exam lists returned.</response>
    /// <response code="401">Caller is not authenticated.</response>
    /// <response code="403">Caller has no active link with this teacher, or their enrollment was removed.</response>
    /// <response code="404">Caller has no student account.</response>
    [HttpGet("teachers/{teacherId:long}")]
    [ModulePermission(roles: new[] { "Student" }, roleOnly: true)]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(object), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetMyExams([FromRoute] long teacherId)
    {
        var resolution = await ResolveStudentForTeacherAsync(teacherId);
        if (resolution.ErrorResponse is not null) return resolution.ErrorResponse;

        return ToResponse(await _service.GetMyExamsAsync(teacherId, resolution.TeacherStudentId!.Value));
    }

    /// <summary>
    /// S2 — Fetches the take-screen for a published exam the student is assigned to: metadata,
    /// window, and every question with its options — <c>isCorrect</c> is never included on this
    /// projection (security by shape, not by DTO-mapper discipline).
    /// </summary>
    /// <param name="teacherId">The teacher who owns the exam.</param>
    /// <param name="onlineExamId">The exam to open.</param>
    /// <response code="200">Take-screen returned.</response>
    /// <response code="401">Caller is not authenticated.</response>
    /// <response code="403">Caller has no active link with this teacher, or is not assigned to this exam.</response>
    /// <response code="404">Exam not found, not published, or caller has no student account.</response>
    [HttpGet("teachers/{teacherId:long}/{onlineExamId:long}/questions")]
    [ModulePermission(roles: new[] { "Student" }, roleOnly: true)]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(object), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetTakeScreen([FromRoute] long teacherId, [FromRoute] long onlineExamId)
    {
        var resolution = await ResolveStudentForTeacherAsync(teacherId);
        if (resolution.ErrorResponse is not null) return resolution.ErrorResponse;

        return ToResponse(await _service.GetTakeScreenAsync(teacherId, resolution.TeacherStudentId!.Value, onlineExamId));
    }

    /// <summary>
    /// S4 — Submits or overwrites the answer for a single question and returns the student's
    /// running stats. Never finalizes the report — the student can keep changing answers until
    /// they bulk-submit (S3) or the window closes.
    /// </summary>
    /// <param name="teacherId">The teacher who owns the exam.</param>
    /// <param name="onlineExamId">The exam being taken.</param>
    /// <param name="request">The question id and the option ids selected.</param>
    /// <response code="200">Answer recorded; running stats returned.</response>
    /// <response code="400">Selected options are invalid for the question, or violate its single/multiple-choice rule.</response>
    /// <response code="401">Caller is not authenticated.</response>
    /// <response code="403">Caller has no active link with this teacher, or is not assigned to this exam.</response>
    /// <response code="404">Exam or question not found.</response>
    /// <response code="409">The exam window is closed, or the report is already locked (submitted or blocked).</response>
    [HttpPost("teachers/{teacherId:long}/{onlineExamId:long}/answers")]
    [ModulePermission(roles: new[] { "Student" }, roleOnly: true)]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(object), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(object), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(object), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> SubmitAnswer(
        [FromRoute] long teacherId, [FromRoute] long onlineExamId, [FromBody] SubmitOnlineExamAnswerRequest request)
    {
        var resolution = await ResolveStudentForTeacherAsync(teacherId);
        if (resolution.ErrorResponse is not null) return resolution.ErrorResponse;

        return ToResponse(await _service.SubmitAnswerAsync(teacherId, resolution.TeacherStudentId!.Value, onlineExamId, request));
    }

    /// <summary>
    /// S3 — Finalizes the exam: upserts any answers in the payload, recomputes the full score,
    /// sets <c>Passed</c>/<c>Failed</c>, and locks the report. Unanswered questions stay
    /// unanswered — this is not required to cover every question in one call.
    /// </summary>
    /// <param name="teacherId">The teacher who owns the exam.</param>
    /// <param name="onlineExamId">The exam being submitted.</param>
    /// <param name="request">Any subset of question answers to upsert before finalizing.</param>
    /// <response code="200">Exam finalized; final stats returned.</response>
    /// <response code="400">One or more selected options are invalid.</response>
    /// <response code="401">Caller is not authenticated.</response>
    /// <response code="403">Caller has no active link with this teacher, or is not assigned to this exam.</response>
    /// <response code="404">Exam or a referenced question not found.</response>
    /// <response code="409">The exam window is closed, or this exam was already submitted.</response>
    [HttpPost("teachers/{teacherId:long}/{onlineExamId:long}/submit")]
    [ModulePermission(roles: new[] { "Student" }, roleOnly: true)]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(object), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(object), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(object), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> SubmitExam(
        [FromRoute] long teacherId, [FromRoute] long onlineExamId, [FromBody] SubmitOnlineExamRequest request)
    {
        var resolution = await ResolveStudentForTeacherAsync(teacherId);
        if (resolution.ErrorResponse is not null) return resolution.ErrorResponse;

        return ToResponse(await _service.SubmitExamAsync(teacherId, resolution.TeacherStudentId!.Value, onlineExamId, request));
    }

    /// <summary>
    /// S5 — Returns the student's current stats for the exam. If the exam window has closed and
    /// the report is still <c>InProgress</c> with at least one answer, this call auto-finalizes
    /// it (lazy finalize) before returning.
    /// </summary>
    /// <param name="teacherId">The teacher who owns the exam.</param>
    /// <param name="onlineExamId">The exam to check.</param>
    /// <response code="200">Stats returned.</response>
    /// <response code="401">Caller is not authenticated.</response>
    /// <response code="403">Caller has no active link with this teacher.</response>
    /// <response code="404">Exam not found, or the student has no attempt recorded for it.</response>
    [HttpGet("teachers/{teacherId:long}/{onlineExamId:long}/result")]
    [ModulePermission(roles: new[] { "Student" }, roleOnly: true)]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(object), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetResult([FromRoute] long teacherId, [FromRoute] long onlineExamId)
    {
        var resolution = await ResolveStudentForTeacherAsync(teacherId);
        if (resolution.ErrorResponse is not null) return resolution.ErrorResponse;

        return ToResponse(await _service.GetResultAsync(teacherId, resolution.TeacherStudentId!.Value, onlineExamId));
    }

    /// <summary>
    /// S6-read — Review screen: the exam, every question, and the student's selected options.
    /// <c>isCorrect</c> and <c>awardedDegree</c> stay <c>null</c> until the report is finalized —
    /// students never see correct answers before submitting.
    /// </summary>
    /// <param name="teacherId">The teacher who owns the exam.</param>
    /// <param name="onlineExamId">The exam to review.</param>
    /// <response code="200">Review returned.</response>
    /// <response code="401">Caller is not authenticated.</response>
    /// <response code="403">Caller has no active link with this teacher.</response>
    /// <response code="404">Exam not found, or still in Draft.</response>
    [HttpGet("teachers/{teacherId:long}/{onlineExamId:long}/answers")]
    [ModulePermission(roles: new[] { "Student" }, roleOnly: true)]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(object), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
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
}
