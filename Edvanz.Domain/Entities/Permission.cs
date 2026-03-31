using Edvanz.Domain.Entities.ShareProp;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;

namespace Edvanz.Domain.Entities
{
    public class Permission:BaseEntity
    {
        public string Name { get; set; }
        [ForeignKey(nameof(module))]
        public long  ModuleId { get; set; }
        public Module module { get; set; }
        public bool IsRestricted { get; set; }
    }
}
