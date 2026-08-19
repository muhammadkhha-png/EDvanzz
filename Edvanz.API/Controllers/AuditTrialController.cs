using Edvanz.API.Attributes;
using Edvanz.Application.Dtos;
using Edvanz.Application.Dtos.AssistantDtos;
using Edvanz.Application.Dtos.AuditTrial;
using Edvanz.Application.IservicesContract;
using Edvanz.Application.ServiceContract;
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
    public class AuditTrialController : ApiBaseController
    {
        private readonly IAudittrialService auditTrialService;
        private readonly ITimeZoneService _timeZoneService;

        public AuditTrialController(
            IAudittrialService auditTrialService,
            ITimeZoneService timeZoneService)
        {
            this.auditTrialService = auditTrialService;
            _timeZoneService = timeZoneService;
        }
        [ModulePermission(roles: new[] { "Teacher", "SuperAdmin" }, roleOnly: true)]
        [HttpGet]
        [ProducesResponseType(typeof(Edvanz.Application.Dtos.Result<Edvanz.Application.Dtos.PaginatedResponse<System.Collections.Generic.List<Edvanz.Application.Dtos.AuditTrial.AuditTrialListDto>>>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetAssisstantsAudits([FromQuery] AuditTrailQueryRequest req)
        {
            var res = await auditTrialService.GetAssistantsAuditTrialsPerTeacher(req);
            return ToResponse(res);
        }
        [ModulePermission(roles: new[] { "Teacher", "SuperAdmin" }, roleOnly: true)]
        [HttpGet("export")]
        [ProducesResponseType(typeof(Edvanz.Application.Dtos.Result<byte[]>), StatusCodes.Status200OK)]
        public async Task<IActionResult> ExportAssistantsAudits([FromQuery] AuditTrialExcelFilterQuery req)
        {
            var result = await auditTrialService.ExportToExcel(req);

            if (!result.IsSuccess)
                return ToResponse(result);

            // Filename timestamp in the teacher's local (Africa/Cairo) time — never the
            // server's DateTime.Now (Azure runs UTC), per the app-wide timezone standard.
            var localNow = _timeZoneService.ConvertUtcToLocal(DateTime.UtcNow);
            return File(
                result.Data!,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                $"AuditTrials_{localNow:yyyyMMdd_HHmm}.xlsx"
            );
        }




    }
}
