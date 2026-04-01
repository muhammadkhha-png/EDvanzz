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
        [ForeignKey(nameof(user))]
        public long userId { get; set; }
        public User user { get; set; }
    }
}
