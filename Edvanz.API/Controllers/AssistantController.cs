using Edvanz.Application.Dtos.AssistantDtos;
using Edvanz.Application.IservicesContract;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Edvanz.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AssistantController : ApiBaseController
    {
        private readonly IAssistantService assistantService;

        public AssistantController(IAssistantService _assistantService)
        {
            assistantService = _assistantService;
        }
        [HttpGet("assistants-per-teacher")]
        public async Task<IActionResult> GetAssistantPerTeacher([FromQuery] AssistantPerTeacherFilterDto req) 
        {
            var res = await assistantService.GetAssistantListPerTeacher(req);
            return ToResponse(res);
        }
    }
}
