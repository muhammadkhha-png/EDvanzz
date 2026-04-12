using Edvanz.Domain.Entities.Messaging;
using Edvanz.Domain.Enums;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace Edvanz.Application.Dtos.TriggerDtos
{
    public class CreateTriggerDto
    {
        public TriggerEventType EventType { get; set; }

        [Required]
        public long MessageTemplateId { get; set; }

        public SendTimingType SendTiming { get; set; } = SendTimingType.Immediate;

        // when SendTiming = Scheduled 
        public TimeSpan? ScheduledTime { get; set; }

        // for threshold-based triggers 
        public int? ThresholdValue { get; set; }

        public TriggerScope Scope { get; set; } = TriggerScope.All;
        public long? SessionId { get; set; }
        public long? SessionGroupId { get; set; }
    }
}
