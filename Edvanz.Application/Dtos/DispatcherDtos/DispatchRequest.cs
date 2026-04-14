using Edvanz.Application.Dtos.MessageResolver;
using Edvanz.Domain.Entities.Messaging;
using System;
using System.Collections.Generic;
using System.Text;

namespace Edvanz.Application.Dtos.DispatcherDtos
{
    public class DispatchRequest
    {
        public long TeacherId { get; set; }
        public TriggerEventType EventType { get; set; }

        // The students affected by this event 
        public List<long> StudentIds { get; set; } = new();

        // Scope info — used to find the right trigger 
        public long? SessionId { get; set; }
        public long? SessionGroupId { get; set; }

        // Context data — dispatcher fills what it can, resolver uses what's needed
        public MessageResolveContext? SharedContext { get; set; }

    
        public Dictionary<long, MessageResolveContext>? PerStudentContext { get; set; }
    }
}
