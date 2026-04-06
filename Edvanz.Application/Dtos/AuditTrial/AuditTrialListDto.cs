using Edvanz.Domain.Entities;
using Edvanz.Domain.Entities.ShareProp;
using Edvanz.Domain.Enums;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;

namespace Edvanz.Application.Dtos.AuditTrial
{
    public class AuditTrialListDto
    {
        public long id { get; set; }
        public string teacherName { get; set; }
        public string assisstantName { get; set; }
        public string acction { get; set; }
        public string moduleName { get; set; }
        public string desc { get; set; }
        public DateTime createAt { get; set; }

    }
 
}
