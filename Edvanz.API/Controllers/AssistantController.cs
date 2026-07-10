using Edvanz.API.Attributes;
using Edvanz.Application.Dtos;
using Edvanz.Application.Dtos.AssistantDtos;
using Edvanz.Application.IservicesContract;
using Edvanz.Application.Services;
using Edvanz.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc;

namespace Edvanz.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    // Tenant isolation: overwrite any teacherId in the body/route with the JWT-resolved teacher
    // (Teacher → own id, Assistant → owning teacher). Without this, CreateAssistant took teacherId
    // straight from the body — a cross-tenant IDOR. SuperAdmin is exempt (may target any teacher).
    [ServiceFilter(typeof(Edvanz.API.Filters.TenantScopeFilter))]
    public class AssistantController : ApiBaseController
    {
        private readonly IAssistantService assistantService;

        public AssistantController(IAssistantService _assistantService)
        {
            assistantService = _assistantService;
        }

        // ── GET /api/assistant ────────────────────────────────────────────────
        // Paginated list of assistants per teacher (REQ-USR-009)
        // Access: Teacher | SuperAdmin
        [HttpGet]
        [ModulePermission(roles: new[] { "Teacher", "SuperAdmin" }, roleOnly: true)]
        [ProducesResponseType(typeof(Result<PaginatedResponse<List<AssistantListDto>>>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetAssistantPerTeacher([FromQuery] AssistantPerTeacherFilterDto req)
        {
            var res = await assistantService.GetAssistantListPerTeacher(req);
            return ToResponse(res);
        }

        // ── GET /api/assistant/{id} ───────────────────────────────────────────
        // Get full detail for a single assistant (REQ-USR-006)
        // Access: Teacher | SuperAdmin
        [HttpGet("{id:long}")]
        [ModulePermission(roles: new[] { "Teacher", "SuperAdmin" }, roleOnly: true)]
        [ProducesResponseType(typeof(AssistantDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetById(long id)
        {
            var res = await assistantService.GetByAssistantIdAsync(id);
            return ToResponse(res);
        }

        // ── POST /api/assistant ───────────────────────────────────────────────
        // Create a new assistant account (REQ-USR-001, REQ-USR-002, REQ-USR-004)
        // teacherId resolved from JWT — client never sends it
        // Access: Teacher | SuperAdmin
        [HttpPost]
        [ModulePermission(roles: new[] { "Teacher", "SuperAdmin" }, roleOnly: true)]
        [ProducesResponseType(typeof(string), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(string), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> Create([FromBody] CreateAssistantDto dto)
        {
            var result = await assistantService.InitializeAssistantAsync(dto);
            return ToResponse(result);
        }

        // ── PUT /api/assistant/{id} ───────────────────────────────────────────
        // Edit any field of an assistant account (REQ-USR-006)
        // Access: Teacher | SuperAdmin
        [HttpPut("{id:long}")]
        [ModulePermission(roles: new[] { "Teacher", "SuperAdmin" }, roleOnly: true)]
        [ProducesResponseType(typeof(string), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(string), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Update(long id, [FromBody] UpdateAssistantRequest dto)
        {
            dto.assistantId = id;
            var result = await assistantService.UpdateAssistantAsync(dto);
            return ToResponse(result);
        }

        // ── DELETE /api/assistant/{id} ────────────────────────────────────────
        // Soft-delete an assistant account (REQ-USR-005)
        // Access: Teacher | SuperAdmin
        //[HttpDelete("{id:long}")]
        //[ModulePermission("Assistants")]
        //[ProducesResponseType(typeof(string), StatusCodes.Status200OK)]
        //[ProducesResponseType(StatusCodes.Status404NotFound)]
        //public async Task<IActionResult> Delete(long id)
        //{
        //    var result = await assistantService.DeleteAssistantAsync(id);
        //    return ToResponse(result);
        //}

        // ── PATCH /api/assistant/{id}/deactivate ──────────────────────────────
        // Deactivate without deleting (REQ-USR-008)
        // Access: Teacher | SuperAdmin
        [HttpPatch("{id:long}/deactivate")]
        [ModulePermission(roles: new[] { "Teacher", "SuperAdmin" }, roleOnly: true)]
        [ProducesResponseType(typeof(string), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Deactivate(long id)
        {
            var result = await assistantService.ToggleStatus(new ToggleAccountStatus
            {
                accountId = id,
                targetStatus = AccountStatus.Inactive
            });
            return ToResponse(result);
        }

        // ── PATCH /api/assistant/{id}/activate ────────────────────────────────
        // Reactivate a deactivated assistant (REQ-USR-008)
        // Access: Teacher | SuperAdmin
        [HttpPatch("{id:long}/activate")]
        [ModulePermission(roles: new[] { "Teacher", "SuperAdmin" }, roleOnly: true)]
        [ProducesResponseType(typeof(string), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Activate(long id)
        {
            var result = await assistantService.ToggleStatus(new ToggleAccountStatus
            {
                accountId = id,
                targetStatus = AccountStatus.Active
            });
            return ToResponse(result);
        }



        [HttpPatch("{id:long}/delete")]
        [ModulePermission(roles: new[] { "Teacher", "SuperAdmin" }, roleOnly: true)]

        [ProducesResponseType(typeof(string), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Delete(long id)
        {
            var result = await assistantService.ToggleStatus(new ToggleAccountStatus
            {
                accountId = id,
                targetStatus = AccountStatus.Suspended
            });

            return ToResponse(result);
        }


        // ── GET /api/assistant/{id}/login-activity ────────────────────────────
        // View login/logout history for a specific assistant (REQ-USR-028)
        // Access: Teacher | SuperAdmin
        [HttpGet("{id:long}/login-activity")]
        [ModulePermission(roles: new[] { "Teacher", "SuperAdmin" }, roleOnly: true)]
        [ProducesResponseType(typeof(List<LoginActivityDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetLoginActivity(long id)
        {
            var result = await assistantService.GetLoginActivityAsync(id);
            return ToResponse(result);
        }
    }
}
