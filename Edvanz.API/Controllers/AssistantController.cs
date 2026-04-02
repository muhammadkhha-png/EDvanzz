using Edvanz.Application.Dtos.AssistantDtos;
using Edvanz.Application.IservicesContract;
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
        [HttpGet("{id:long}")]
        //[Authorize(Policy = "Teacher")]
        //[Authorize(Policy = "SuperAdmin")]
        public async Task<IActionResult> GetById(long id)
        {
            var res = await assistantService.GetByAssistantIdAsync(id);
            return ToResponse(res);
        }
        }
}
