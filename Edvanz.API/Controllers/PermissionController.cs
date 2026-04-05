using Edvanz.API.Attributes;
using Edvanz.Application.Dtos;
using Edvanz.Application.Dtos.AssistantDtos;
using Edvanz.Application.IservicesContract;
using Edvanz.Application.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Edvanz.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class PermissionController : ApiBaseController
    {
        private readonly IPermissionService _permissionService;

        public PermissionController(IPermissionService _permissionService)
        {
            this._permissionService = _permissionService;
        }

       
        [HttpPut("assitant/update-permissions/{id:long}")]
        [ModulePermission("Assistants")]
        [ProducesResponseType(typeof(string), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(string), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> UpdatePermissions(UpdateAssistantPermissionsRequest req)
        {
            var result = await _permissionService.UpdateAssistantPermissionsAsync(req);
            return ToResponse(result);
        }

        
        [HttpPost("{id:long}/apply-profile")]
        [ModulePermission("Assistants")]
        [ProducesResponseType(typeof(string), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(string), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> ApplyProfile(long id, [FromBody] List<long> profileIds)
        {
            var result = await _permissionService.UpdateAssistantPermissionsAsync(
                new UpdateAssistantPermissionsRequest
                {
                    assistantId = id,
                    PermissionProfileIds = profileIds
                });
            return ToResponse(result);
        }



       
        [HttpGet("teacher/{id}")]
        [ModulePermission("Assistants")]
        [ProducesResponseType(typeof(PaginatedResponse<List<AssistantListDto>>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetAssistantPerTeacher(long id)
        {
            var res = await _permissionService.GetAvailableTeacherPermissionCatalogue(id);
            return ToResponse(res);
        }

    }
}
