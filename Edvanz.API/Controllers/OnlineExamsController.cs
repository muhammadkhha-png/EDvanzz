using Edvanz.API.Attributes;
using Edvanz.Application.Dtos;
using Edvanz.Application.IservicesContract;
using Edvanz.Application.ServiceContract;
using Edvanz.Domain.Constants;
using Edvanz.Domain.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Edvanz.API.Controllers;

/// <summary>
/// Online Exam Module — teacher/assistant endpoints (T1–T14 minus T5s, Phase 6).
/// TeacherId always via ResolveTeacherIdAsync() — never route/body (catalog §1.3).
/// </summary>
[Route("api/online-exams")]
[Authorize]
public sealed class OnlineExamsController : ModuleSixApiBaseController
{
    private readonly IOnlineExamService _service;

    public OnlineExamsController(
        IOnlineExamService service, ICurrentUserService currentUser, IUnitOfWork unitOfWork)
        : base(currentUser, unitOfWork)
    {
        _service = service;
    }

    // T1 — POST /api/online-exams
    [HttpPost]
    [ModulePermission(OnlineExamConstants.ModuleName, OnlineExamConstants.PermissionManageExams)]
    public async Task<IActionResult> Create([FromBody] CreateOnlineExamRequest request)
    {
        long? teacherId = await ResolveTeacherIdAsync();
        if (teacherId is null) return TeacherNotResolved();
        return ToResponse(await _service.CreateAsync(teacherId.Value, GetActingUserId(), request));
    }

    // T8 — PUT /api/online-exams/{id}
    [HttpPut("{onlineExamId:long}")]
    [ModulePermission(OnlineExamConstants.ModuleName, OnlineExamConstants.PermissionManageExams)]
    public async Task<IActionResult> Update([FromRoute] long onlineExamId, [FromBody] UpdateOnlineExamRequest request)
    {
        long? teacherId = await ResolveTeacherIdAsync();
        if (teacherId is null) return TeacherNotResolved();
        return ToResponse(await _service.UpdateAsync(teacherId.Value, GetActingUserId(), onlineExamId, request));
    }

    // T9 — GET /api/online-exams/{id}
    [HttpGet("{onlineExamId:long}")]
    [ModulePermission(OnlineExamConstants.ModuleName, OnlineExamConstants.PermissionView)]
    public async Task<IActionResult> GetById([FromRoute] long onlineExamId)
    {
        long? teacherId = await ResolveTeacherIdAsync();
        if (teacherId is null) return TeacherNotResolved();
        return ToResponse(await _service.GetByIdAsync(teacherId.Value, onlineExamId));
    }

    // T4 — GET /api/online-exams
    [HttpGet]
    [ModulePermission(OnlineExamConstants.ModuleName, OnlineExamConstants.PermissionView)]
    public async Task<IActionResult> GetList([FromQuery] OnlineExamListRequest request)
    {
        long? teacherId = await ResolveTeacherIdAsync();
        if (teacherId is null) return TeacherNotResolved();
        return ToResponse(await _service.GetListAsync(teacherId.Value, request));
    }

    // T6 — GET /api/online-exams/{id}/overview
    [HttpGet("{onlineExamId:long}/overview")]
    [ModulePermission(OnlineExamConstants.ModuleName, OnlineExamConstants.PermissionView)]
    public async Task<IActionResult> GetOverview([FromRoute] long onlineExamId)
    {
        long? teacherId = await ResolveTeacherIdAsync();
        if (teacherId is null) return TeacherNotResolved();
        return ToResponse(await _service.GetOverviewAsync(teacherId.Value, onlineExamId));
    }

    // T7 — GET /api/online-exams/{id}/scope-analysis
    [HttpGet("{onlineExamId:long}/scope-analysis")]
    [ModulePermission(OnlineExamConstants.ModuleName, OnlineExamConstants.PermissionView)]
    public async Task<IActionResult> GetScopeAnalysis([FromRoute] long onlineExamId)
    {
        long? teacherId = await ResolveTeacherIdAsync();
        if (teacherId is null) return TeacherNotResolved();
        return ToResponse(await _service.GetScopeAnalysisAsync(teacherId.Value, onlineExamId));
    }

    // T12 — GET /api/online-exams/{id}/questions
    [HttpGet("{onlineExamId:long}/questions")]
    [ModulePermission(OnlineExamConstants.ModuleName, OnlineExamConstants.PermissionView)]
    public async Task<IActionResult> GetQuestions([FromRoute] long onlineExamId)
    {
        long? teacherId = await ResolveTeacherIdAsync();
        if (teacherId is null) return TeacherNotResolved();
        return ToResponse(await _service.GetQuestionsAsync(teacherId.Value, onlineExamId));
    }

    // T10 — GET /api/online-exams/{id}/questions/overview
    [HttpGet("{onlineExamId:long}/questions/overview")]
    [ModulePermission(OnlineExamConstants.ModuleName, OnlineExamConstants.PermissionView)]
    public async Task<IActionResult> GetQuestionsOverview([FromRoute] long onlineExamId)
    {
        long? teacherId = await ResolveTeacherIdAsync();
        if (teacherId is null) return TeacherNotResolved();
        return ToResponse(await _service.GetQuestionsOverviewAsync(teacherId.Value, onlineExamId));
    }

    // T2 — POST /api/online-exams/{id}/questions
    [HttpPost("{onlineExamId:long}/questions")]
    [ModulePermission(OnlineExamConstants.ModuleName, OnlineExamConstants.PermissionManageExams)]
    public async Task<IActionResult> AddQuestion([FromRoute] long onlineExamId, [FromBody] CreateOnlineExamQuestionDto request)
    {
        long? teacherId = await ResolveTeacherIdAsync();
        if (teacherId is null) return TeacherNotResolved();
        return ToResponse(await _service.AddQuestionAsync(teacherId.Value, onlineExamId, request));
    }

    // T3 — POST /api/online-exams/{id}/questions/bulk
    [HttpPost("{onlineExamId:long}/questions/bulk")]
    [ModulePermission(OnlineExamConstants.ModuleName, OnlineExamConstants.PermissionManageExams)]
    public async Task<IActionResult> AddQuestionsBulk([FromRoute] long onlineExamId, [FromBody] List<Application.Dtos.CreateOnlineExamQuestionDto> request)
    {
        long? teacherId = await ResolveTeacherIdAsync();
        if (teacherId is null) return TeacherNotResolved();
        return ToResponse(await _service.AddQuestionsBulkAsync(teacherId.Value, onlineExamId, request));
    }

    // T13 — PUT /api/online-exams/{id}/questions
    [HttpPut("{onlineExamId:long}/questions")]
    [ModulePermission(OnlineExamConstants.ModuleName, OnlineExamConstants.PermissionManageExams)]
    public async Task<IActionResult> ReplaceQuestions([FromRoute] long onlineExamId, [FromBody] ReplaceOnlineExamQuestionsRequest request)
    {
        long? teacherId = await ResolveTeacherIdAsync();
        if (teacherId is null) return TeacherNotResolved();
        return ToResponse(await _service.ReplaceQuestionsAsync(teacherId.Value, onlineExamId, request));
    }

    // T11 — PATCH /api/online-exams/{id}/status
    [HttpPatch("{onlineExamId:long}/status")]
    [ModulePermission(OnlineExamConstants.ModuleName, OnlineExamConstants.PermissionManageExams)]
    public async Task<IActionResult> UpdateStatus([FromRoute] long onlineExamId, [FromBody] UpdateOnlineExamStatusRequest request)
    {
        long? teacherId = await ResolveTeacherIdAsync();
        if (teacherId is null) return TeacherNotResolved();
        return ToResponse(await _service.UpdateStatusAsync(teacherId.Value, onlineExamId, request));
    }

    // T14 — DELETE /api/online-exams/{id}
    [HttpDelete("{onlineExamId:long}")]
    [ModulePermission(OnlineExamConstants.ModuleName, OnlineExamConstants.PermissionManageExams)]
    public async Task<IActionResult> Delete([FromRoute] long onlineExamId)
    {
        long? teacherId = await ResolveTeacherIdAsync();
        if (teacherId is null) return TeacherNotResolved();
        return ToResponse(await _service.DeleteAsync(teacherId.Value, onlineExamId));
    }
    // T5s — PATCH /api/online-exams/{id}/students/{teacherStudentId}/status
    [HttpPatch("{onlineExamId:long}/students/{teacherStudentId:long}/status")]
    [ModulePermission(OnlineExamConstants.ModuleName, OnlineExamConstants.PermissionManageExams)]
    public async Task<IActionResult> UpdateStudentStatus(
        [FromRoute] long onlineExamId, [FromRoute] long teacherStudentId,
        [FromBody] UpdateOnlineExamStudentStatusRequest request)
    {
        long? teacherId = await ResolveTeacherIdAsync();
        if (teacherId is null) return TeacherNotResolved();
        return ToResponse(await _service.UpdateStudentStatusAsync(teacherId.Value, onlineExamId, teacherStudentId, request));
    }
}