using System;
using System.Collections.Generic;
using System.Text;

namespace Edvanz.Application.Dtos.AssistantDtos
{
    public class LoginActivityDto
    {
        public long? id { get; set; }
        public string assistantName { get; set; } = string.Empty;
        public string action { get; set; } = string.Empty;
        public DateTime occurredAt { get; set; }
        public string? deviceOrBrowser { get; set; }
        public string? ipAddress { get; set; }
    }
}
