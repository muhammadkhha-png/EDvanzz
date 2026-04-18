using Edvanz.Domain.Entities.Messaging;
using Edvanz.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Text;

namespace Edvanz.Application.Dtos.TriggerDtos
{
    public class TriggerFiltetrReq
    {
        public long teacherId { get; set; }
        public TriggerEventType? eventType { get; set; }
        public TriggerScope? scope { get; set; }
        public bool? isActive { get; set; }
        public int? page { get; set; }
        public int? pageSize { get; set; }

    }
}
