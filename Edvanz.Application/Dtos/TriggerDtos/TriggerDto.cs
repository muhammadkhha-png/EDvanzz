using Edvanz.Domain.Entities.Messaging;
using Edvanz.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Text;

namespace Edvanz.Application.Dtos.TriggerDtos
{
    public class TriggerDto
    {
        public long Id { get; set; }
        public TriggerEventType EventType { get; set; }
        public string EventTypeName => EventType.ToString();
        public long MessageTemplateId { get; set; }
        public string TemplateName { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public SendTimingType SendTiming { get; set; }
        public TimeSpan? ScheduledTime { get; set; }
        public int? ThresholdValue { get; set; }
        public TriggerScope Scope { get; set; }
        public long? SessionId { get; set; }
        public long? SessionGroupId { get; set; }
    }
}
