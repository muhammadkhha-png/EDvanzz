using Edvanz.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Text;

namespace Edvanz.Domain.Interfaces
{
    public interface IAuditTrialRepo:IGenericRepo<AuditTrail,long>
    {
        public Task<(List<AuditTrail>, int count)> GetTeacherAssitantsAuditTrial(long teacherID, string? ActionType, string? AssistantName, string? Module, DateTime? From, DateTime? To, int page, int pageSize);
        public  Task<List<AuditTrail>> GetTeacherAssitantsAuditTrialForExcel(long teacherID, string? ActionType, string? AssistantName, string? Module, DateTime? From, DateTime? To);

    }
}
