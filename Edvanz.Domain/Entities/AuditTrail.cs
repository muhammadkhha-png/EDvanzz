using Edvanz.Domain.Entities.ShareProp;
using Edvanz.Domain.Enums;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;

namespace Edvanz.Domain.Entities
{
    public class AuditTrail:BaseEntity
    {
        [ForeignKey(nameof(Teacher))]
        public long teacherId { get; set; }
        public Teacher Teacher { get; set; }
        [ForeignKey(nameof(assistant))]
        public long? AssistantId { get; set; }
        public Assistant assistant { get; set; }
        public ActionType actionType { get; set; }
        [ForeignKey(nameof(module))]
        public long ModuleId { get; set; }
        public Module module { get; set; }
        public string Desc { get; set; }
        public DateTime CreateAt { get; set; }
    }
}
