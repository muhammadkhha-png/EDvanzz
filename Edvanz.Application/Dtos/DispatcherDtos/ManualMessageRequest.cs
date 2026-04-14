using Edvanz.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Text;

namespace Edvanz.Application.Dtos.DispatcherDtos
{
    public class ManualMessageRequests
    {
        public long TeacherId { get; set; }
        public long MessageTemplateId { get; set; }
        public List<long> StudentIds { get; set; } = new();
        public ChannelType? ChannelOverride { get; set; }           // overrides template setting
        public RecipientTarget? RecipientTargetOverride { get; set; }
        public DateTime? ScheduledSendAt { get; set; }

        // For manual announcements  injected as CustomText overrides
        public Dictionary<string, string>? VariableOverrides { get; set; }
    }

}
