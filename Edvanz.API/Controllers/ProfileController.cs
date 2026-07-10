using Edvanz.API.Attributes;
using Edvanz.Application.Dtos.TemplatePermissionsDtos;
using Edvanz.Application.IservicesContract;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Edvanz.API.Controllers
{
    [Route("api/permission-profiles")]
    [ApiController]
    [ServiceFilter(typeof(Edvanz.API.Filters.TenantScopeFilter))]
    public class ProfileController : ApiBaseController
    {
        private readonly IProfileService _profileService;

        public ProfileController(IProfileService profileService)
        {
            _profileService = profileService;
        }

        // ── GET /api/permission-profiles ─────────────────────────────────────
        // Get all profiles per teacher — teacherId from JWT (REQ-USR-014)
        // Access: Teacher | SuperAdmin
        [HttpGet("teacher/{teacherId:long}")]
        [ProducesResponseType(typeof(List<ProfilePermissionDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetAll(long teacherId)
        {
            // Route param renamed id -> teacherId (same URL) so the tenant filter validates it
            // against the caller and blocks cross-tenant reads of another teacher's profiles.
            var result = await _profileService
                .GetAllProfilesWithPermissionsPerTeacherAsync(teacherId);

            return ToResponse(result);
        }

        // ── GET /api/permission-profiles/{id} ─────────────────────────────────
        // Get single profile with its permissions (REQ-USR-014)
        // Access: Teacher | SuperAdmin
        [HttpGet("{id:long}")]
        [ModulePermission("Assistants")]
        [ProducesResponseType(typeof(ProfilePermissionDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetById(long id)
        {
            var result = await _profileService
                .GetAllProfileWithPermissionsByIdAsync(id);

            return ToResponse(result);
        }

        // ── POST /api/permission-profiles ────────────────────────────────────
        // Create a new permission template (REQ-USR-014)
        // teacherId injected from JWT — client never sends it
        // Access: Teacher | SuperAdmin
        [HttpPost]
        [ModulePermission("Assistants")]
        [ProducesResponseType(typeof(string), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(string), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> Create([FromBody] CreatePermissionsProfileDto dto)
        {
           

            var result = await _profileService.CreateProfileAsync(dto);

            return ToResponse(result);
        }

        // ── PUT /api/permission-profiles/{id} ─────────────────────────────────
        // Edit name and/or permissions of a template (REQ-USR-014)
        // Note: editing permissions syncs all assistants who have this template
        // Access: Teacher | SuperAdmin
        [HttpPut("{id:long}")]
        [ModulePermission("Assistants")]
        [ProducesResponseType(typeof(string), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(string), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Update(long id, [FromBody] UpdatePermissionsProfileDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            dto.templateId = id;

            var result = await _profileService.UpdateProfileAsync(dto);

            return ToResponse(result);
        }

        // ── DELETE /api/permission-profiles/{id} ──────────────────────────────
        // Delete template + remove its permissions from all assigned assistants
        // (safely — protected permissions from other templates are kept)
        // Access: Teacher | SuperAdmin
        [HttpDelete("{id:long}")]
        [ModulePermission("Assistants")]
        [ProducesResponseType(typeof(string), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Delete(long id)
        {
            var result = await _profileService.DeleteProfileAsync(id);

            return ToResponse(result);
        }
    }
}
