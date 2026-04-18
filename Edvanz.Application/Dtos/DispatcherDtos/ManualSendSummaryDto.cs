using System;
using System.Collections.Generic;
using System.Text;

namespace Edvanz.Application.Dtos.DispatcherDtos
{
    public class ManualSendSummaryDto
    {
        public int StudentCount { get; set; }
        public int ParentCount { get; set; }
        public int SkippedNoPhone { get; set; }
        public int Failed { get; set; }
        public int TotalRecipients { get; set; }
        public List<string> Channels { get; set; } = new();
        public string? PreviewContent { get; set; }
    }

}
