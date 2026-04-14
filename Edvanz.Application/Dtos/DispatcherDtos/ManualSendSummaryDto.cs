using System;
using System.Collections.Generic;
using System.Text;

namespace Edvanz.Application.Dtos.DispatcherDtos
{
    public class ManualSendSummaryDto
    {
        public int TotalRecipients { get; set; }
        public int StudentCount { get; set; }
        public int ParentCount { get; set; }
        public string PreviewContent { get; set; } = string.Empty;  
        public List<string> Channels { get; set; } = new();
    }

}
