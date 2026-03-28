using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;

namespace Edvanz.Domain.Entities
{
    public class TemplateAssistant
    {
        [ForeignKey(nameof(template))]
        public long TemplateId { get; set; }
        
        public Template template { get; set; }
        [ForeignKey(nameof(assistant))]
        public long AssistantId { get; set; }
        public User assistant { get; set; }
    }
}
