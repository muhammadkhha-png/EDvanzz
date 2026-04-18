using Edvanz.Domain.Entities.Messaging;
using Edvanz.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Text;

namespace Edvanz.Application.Dtos.DispatcherDtos
{
    public class MessageSendPayload
    {
        public long TeacherId { get; set; }
        public long StudentId { get; set; }
        public string StudentName { get; set; } = string.Empty;
        public string StudentCode { get; set; } = string.Empty;
        public string RecipientPhone { get; set; } = string.Empty;
        public RecipientTarget RecipientType { get; set; }
        public string ResolvedContent { get; set; } = string.Empty;
        public ChannelType Channel { get; set; }
        public long? MessageTemplateId { get; set; }
        public TriggerEventType? TriggerType { get; set; }
        public bool IsManual { get; set; }
        public DateTime? ScheduledSendAt { get; set; }
        public string? EncryptedCredentials { get; set; }
    }
}
