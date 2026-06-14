using Edvanz.Application.Dtos;
using Edvanz.Application.Dtos.AuditTrial;
using System;
using System.Collections.Generic;
using System.Text;

namespace Edvanz.Application.IservicesContract
{
    public interface IAudittrialService
    {
        public  Task RecordAuditTrailAsync(CreateAuditTrailDto dto);
        public Task<Result<PaginatedResponse<List<AuditTrialListDto>>>> GetAssistantsAuditTrialsPerTeacher(AuditTrailQueryRequest dto);
        public Task<Result<byte[]>> ExportToExcel(AuditTrialExcelFilterQuery filter);
        public Task<Result<byte[]>> ExportToPdf(AuditTrialExcelFilterQuery filter);
    }
}
