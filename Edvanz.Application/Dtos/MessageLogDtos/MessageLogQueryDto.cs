using Edvanz.Domain.Entities.Messaging;
using Edvanz.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Text;

namespace Edvanz.Application.Dtos.MessageLogDtos
{
    public class MessageLogQueryDto
    {
        public long TeacherId { get; set; }   
        public string? StudentName { get; set; }
        public string? StudentCode { get; set; }
        public DateTime? From { get; set; }
        public DateTime? To { get; set; }
        public TriggerEventType? TriggerType { get; set; }
        public MessageStatus? Status { get; set; }
        public ChannelType? Channel { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 50;
    }
}
