    using System;
using System.Collections.Generic;
using System.Text;

namespace Edvanz.Application.Dtos.AuditTrial
{
    public class AuditTrailQueryRequest
    {
        public long teacherID { get; set; }
        public string? AssistantName { get; set; }
        public string? ActionType { get; set; }
        public string? Module { get; set; }
        public DateTime? From { get; set; }
        public DateTime? To { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 50;
    }
}
