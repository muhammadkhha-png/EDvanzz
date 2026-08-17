using Edvanz.API.Attributes;
using Edvanz.Domain.Constants;
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
        private readonly Edvanz.Application.IservicesContract.ICurrentUserService _currentUser;
        private readonly Edvanz.Domain.Interfaces.IUnitOfWork _unitOfWork;

        public ProfileController(
            IProfileService profileService,
            Edvanz.Application.IservicesContract.ICurrentUserService currentUser,
            Edvanz.Domain.Interfaces.IUnitOfWork unitOfWork)
        {
            _profileService = profileService;
            _currentUser = currentUser;
            _unitOfWork = unitOfWork;
        }

        /// <summary>
        /// The acting teacher, resolved from the JWT (Teacher → own id, Assistant → owning teacher).
        /// The optional <paramref name="explicitTeacherId"/> from the route is honoured ONLY for
        /// SuperAdmin (support access); for everyone else it is ignored so the token is the sole tenant
        /// differentiator — endpoints work whether or not the URL carries the id.
        /// </summary>
        private async Task<long?> ResolveTeacherIdAsync(long? explicitTeacherId = null)
        {
            if (string.Equals(_currentUser.Role, "SuperAdmin", StringComparison.Ordinal))
                return explicitTeacherId;

            // Center tier: resolve the acting teacher (X-Acting-Teacher-Id) validated against membership.
            if (string.Equals(_currentUser.Role, UserRoles.Center, StringComparison.Ordinal)
                || string.Equals(_currentUser.Role, UserRoles.CenterAssistant, StringComparison.Ordinal))
                return await _currentUser.ResolveActingTeacherIdAsync();

            var userId = _currentUser.UserId;
            if (userId is null) return null;

            long? id = (await _unitOfWork.Users.GetTeacherByUserIdAsync(userId.Value))?.Id;
            if (id is null)
            {
                var asst = await _unitOfWork.AssistantRepo.GetAssistantWithUserIdAsync(userId.Value);
                id = asst?.TeacherAccountId;
            }
            return id;
        }

        private IActionResult TeacherNotResolvedResult() =>
            new ObjectResult(new { success = false, message = "Teacher could not be resolved from token." })
            { StatusCode = StatusCodes.Status404NotFound };

        // ── GET /api/permission-profiles ─────────────────────────────────────
        // Get all profiles per teacher — teacherId from JWT (REQ-USR-014)
        // Access: Teacher | SuperAdmin
        [HttpGet("teacher")]
        [HttpGet("teacher/{teacherId:long}")]
        [ProducesResponseType(typeof(List<ProfilePermissionDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetAll([FromRoute] long? teacherId = null)
        {
            var id = await ResolveTeacherIdAsync(teacherId);
            if (id is null) return TeacherNotResolvedResult();

            var result = await _profileService
                .GetAllProfilesWithPermissionsPerTeacherAsync(id.Value);

            return ToResponse(result);
        }

        // ── GET /api/permission-profiles/{id} ─────────────────────────────────
        // Get single profile with its permissions (REQ-USR-014)
        // Access: Teacher | SuperAdmin
        [HttpGet("{id:long}")]
        [ModulePermission(roles: new[] { "Teacher", "SuperAdmin" }, roleOnly: true)]
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
        [ModulePermission(roles: new[] { "Teacher", "SuperAdmin" }, roleOnly: true)]
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
        [ModulePermission(roles: new[] { "Teacher", "SuperAdmin" }, roleOnly: true)]
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
        [ModulePermission(roles: new[] { "Teacher", "SuperAdmin" }, roleOnly: true)]
        [ProducesResponseType(typeof(string), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Delete(long id)
        {
            var result = await _profileService.DeleteProfileAsync(id);

            return ToResponse(result);
        }
    }
}
