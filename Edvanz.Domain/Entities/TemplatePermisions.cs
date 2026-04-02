using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;

namespace Edvanz.Domain.Entities
{
    public class TemplatePermisions
    {
        [ForeignKey(nameof(template))]
        public long TemplateId { get; set; }
        public Template template { get; set; }
        [ForeignKey(nameof(permision))]
        public long PermisionId { get; set; }
        public Permission permision { get; set; }
        public virtual ICollection<TemplatePermissionsUsers> PermissionProfiles { get; set; } = new List<TemplatePermissionsUsers>();

    }
}
