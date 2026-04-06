using Edvanz.API.Attributes;
using Edvanz.Application.Dtos;
using Edvanz.Application.Dtos.AssistantDtos;
using Edvanz.Application.Dtos.AuditTrial;
using Edvanz.Application.IservicesContract;
using Edvanz.Application.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Edvanz.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuditTrialController : ApiBaseController
    {
        private readonly IAudittrialService auditTrialService;

        public AuditTrialController(IAudittrialService auditTrialService)
        {
            this.auditTrialService = auditTrialService;
        }
        [HttpGet]
        [ModulePermission("Assistants",null,"Teacher")]
     
        public async Task<IActionResult> GetAssisstantsAudits([FromQuery] AuditTrailQueryRequest req)
        {
            var res = await auditTrialService.GetAssistantsAuditTrialsPerTeacher(req);
            return ToResponse(res);
        }
        [HttpGet("export")]
        [ModulePermission("Assistants", null, "Teacher")]
        public async Task<IActionResult> ExportAssistantsAudits([FromQuery] AuditTrialExcelFilterQuery req)
        {
            var result = await auditTrialService.ExportToExcel(req);

            if (!result.IsSuccess)
                return ToResponse(result);

            return File(
                result.Data!,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                $"AuditTrials_{DateTime.Now:yyyyMMdd_HHmm}.xlsx"
            );
        }

    }
}
