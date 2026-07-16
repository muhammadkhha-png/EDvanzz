using Edvanz.API.Attributes;
using Edvanz.Application.Dtos;
using Edvanz.Application.Dtos.AssistantDtos;
using Edvanz.Application.IservicesContract;
using Edvanz.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Edvanz.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    [ServiceFilter(typeof(Edvanz.API.Filters.TenantScopeFilter))]
    public class PermissionController : ApiBaseController
    {
        private readonly IPermissionService _permissionService;
        private readonly Edvanz.Application.IservicesContract.ICurrentUserService _currentUser;
        private readonly Edvanz.Domain.Interfaces.IUnitOfWork _unitOfWork;

        public PermissionController(
            IPermissionService _permissionService,
            Edvanz.Application.IservicesContract.ICurrentUserService currentUser,
            Edvanz.Domain.Interfaces.IUnitOfWork unitOfWork)
        {
            this._permissionService = _permissionService;
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

       
        [HttpPut("assitant/update-permissions/{id:long}")]
        [ModulePermission(roles: new[] { "Teacher", "SuperAdmin" }, roleOnly: true)]
        [ProducesResponseType(typeof(string), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(string), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> UpdatePermissions(UpdateAssistantPermissionsRequest req)
        {
            var result = await _permissionService.UpdateAssistantPermissionsAsync(req);
            return ToResponse(result);
        }

        
        [HttpPost("{id:long}/apply-profile")]
        [ModulePermission(roles: new[] { "Teacher", "SuperAdmin" }, roleOnly: true)]
        [ProducesResponseType(typeof(string), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(string), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> ApplyProfile(long id, [FromBody] List<long> profileIds)
        {
            var result = await _permissionService.UpdateAssistantPermissionsAsync(
                new UpdateAssistantPermissionsRequest
                {
                    assistantId = id,
                    permissionProfileIds = profileIds
                });
            return ToResponse(result);
        }



       
        [HttpGet("teacher")]



       
        [ProducesResponseType(typeof(Edvanz.Application.Dtos.Result<System.Collections.Generic.List<Edvanz.Application.Dtos.ModulesPermissions.ModulePermissionsDto>>), StatusCodes.Status200OK)]
        [HttpGet("teacher/{teacherId}")]
        [ModulePermission(roles: new[] { "Teacher", "SuperAdmin" }, roleOnly: true)]
        public async Task<IActionResult> GetAvailablePermissionsPerTeacher([FromRoute] long? teacherId = null)
        {
            var id = await ResolveTeacherIdAsync(teacherId);
            if (id is null) return TeacherNotResolvedResult();

            var res = await _permissionService.GetAvailableTeacherPermissionCatalogue(id.Value);
            return ToResponse(res);
        }

    }
}
