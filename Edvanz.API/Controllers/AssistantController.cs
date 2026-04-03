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
    //[Authorize]
    public class AssistantController : ApiBaseController
    {
        private readonly IAssistantService assistantService;

        public AssistantController(IAssistantService _assistantService)
        {
            assistantService = _assistantService;
        }
        //[Authorize(Policy = "Teacher")]
        [HttpGet]
        public async Task<IActionResult> GetAssistantPerTeacher([FromQuery] AssistantPerTeacherFilterDto req) 
        {
            var res = await assistantService.GetAssistantListPerTeacher(req);
            return ToResponse(res);
        }
        //[Authorize(Policy = "Teacher")]
        //[Authorize(Policy = "SuperAdmin")]
        [HttpGet("{id:long}")]
        public async Task<IActionResult> GetById(long id)
        {
            var res = await assistantService.GetByAssistantIdAsync(id);
            return ToResponse(res);
        }
        // ────────────────────────────────────────────────────────────────
        // POST /api/assistants
        //
        // Creates a new assistant account under the calling teacher's space.
        //
        // teacherId is resolved from the JWT so the client never sends it.
        // The service validates:
        //   - The teacher exists and is active
        //   - The caller IS that teacher (ownership check)
        //   - Username / phone / email uniqueness
        //   - All permissionIds and permissionProfileIds actually exist
        //
        // On success → 201 Created with the full AssistantDetailDto.
        // ────────────────────────────────────────────────────────────────
        //[Authorize(Policy = "Teacher")]
        //[Authorize(Policy = "SuperAdmin")]
        [HttpPost]
        [ProducesResponseType(typeof(string), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(string), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> Create([FromBody] CreateAssistantDto dto)
        {
            var result = await assistantService.InitializeAssistantAsync(dto);
            return ToResponse(result);
        }

    }
}
