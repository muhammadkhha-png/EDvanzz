using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;

namespace Edvanz.Domain.Entities
{
    public class TemplatePermissionsUsers
    {
        [ForeignKey(nameof(template))]
        public long TemplateId { get; set; }
        
        public Template template { get; set; }
        [ForeignKey(nameof(assissntat))]
        public long AssisstantId { get; set; }
        public Assistant assissntat { get; set; }
    }
}
