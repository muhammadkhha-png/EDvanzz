using System;
using System.Collections.Generic;
using System.Text;

namespace Edvanz.Application.Dtos.AuditTrial
{
    public class AuditTrialExcelFilterQuery
    {
        public long teacherID { get; set; }
        public string? AssistantName { get; set; }
        public string? ActionType { get; set; }
        public string? Module { get; set; }
        public DateTime? From { get; set; }
        public DateTime? To { get; set; }
    }
}
