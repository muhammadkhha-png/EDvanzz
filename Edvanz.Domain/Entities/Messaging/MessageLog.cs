using Edvanz.Domain.Entities.ShareProp;
using Edvanz.Domain.Enums;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;

namespace Edvanz.Domain.Entities.Messaging
{
    // كل message اتبعتت — permanent record
    public class MessageLog : BaseEntity
    {
        [ForeignKey(nameof(Teacher))]
        public long TeacherId { get; set; }
        public Teacher Teacher { get; set; } = null!;

        public long StudentId { get; set; }
        public string StudentCode { get; set; }
        public string StudentName { get; set; } = string.Empty;
        public string RecipientPhone { get; set; } = string.Empty;
        public RecipientTarget RecipientType { get; set; }  // Student | Parent

        public long? MessageTemplateId { get; set; }
        public MessageTemplate? MessageTemplate { get; set; }

        public string ResolvedContent { get; set; } = string.Empty;

        public ChannelType Channel { get; set; }
        public MessageStatus Status { get; set; }
        public string? FailureReason { get; set; }

        public TriggerEventType? TriggerType { get; set; }
        public bool IsManual { get; set; } = false;

        public DateTime SentAt { get; set; }
        public DateTime? DeliveredAt { get; set; }
    }

    public enum MessageStatus { Pending = 1, Delivered = 2, Failed = 3 }
}
