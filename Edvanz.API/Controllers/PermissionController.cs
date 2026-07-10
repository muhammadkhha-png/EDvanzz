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

        public PermissionController(IPermissionService _permissionService)
        {
            this._permissionService = _permissionService;
        }

       
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



       
        [HttpGet("teacher/{teacherId}")]
        [ModulePermission(roles: new[] { "Teacher", "SuperAdmin" }, roleOnly: true)]
        public async Task<IActionResult> GetAvailablePermissionsPerTeacher(long teacherId)
        {
            // Route param renamed id -> teacherId (same URL) so the tenant filter blocks a teacher
            // from reading another teacher's permission catalogue.
            var res = await _permissionService.GetAvailableTeacherPermissionCatalogue(teacherId);
            return ToResponse(res);
        }

    }
}
