using Edvanz.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Text;

namespace Edvanz.Application.Dtos.AuditTrial
{
    public class CreateAuditTrailDto
    {
        public long teacherId { get; set; }
        public long assistantId { get; set; }
        public ActionType actionType { get; set; }
        public long moduleId { get; set; }
        public string desc { get; set; } = string.Empty;
    }
}
