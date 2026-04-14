using Edvanz.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Text;

namespace Edvanz.Application.Dtos.TriggerDtos
{
    public class UpdateTriggerDto
    {
        public long TriggerId { get; set; }
        public long teacherId { get; set; }
        public long? MessageTemplateId { get; set; }
        public SendTimingType? SendTiming { get; set; }
        public TimeSpan? ScheduledTime { get; set; }
        public int? ThresholdValue { get; set; }
        public TriggerScope? Scope { get; set; }
        public long? SessionId { get; set; }
        public long? SessionGroupId { get; set; }
    }
}
