using Edvanz.Domain.Entities.ShareProp;
using Edvanz.Domain.Enums;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;

namespace Edvanz.Domain.Entities.Messaging
{
    public class MessageQueueItem : BaseEntity
    {
        [ForeignKey(nameof(Teacher))]
        public long TeacherId { get; set; }

        public long StudentId { get; set; }
        public string RecipientPhone { get; set; } = string.Empty;
        public RecipientTarget RecipientType { get; set; }

        public string ResolvedContent { get; set; } = string.Empty;
        public ChannelType Channel { get; set; }

        public long? TriggerId { get; set; }
        public long? MessageTemplateId { get; set; }

        public QueueItemStatus Status { get; set; } = QueueItemStatus.Pending;
        public int RetryCount { get; set; } = 0;

        public DateTime QueuedAt { get; set; } = DateTime.UtcNow;
        public DateTime? ProcessedAt { get; set; }
        public DateTime? ScheduledSendAt { get; set; }  
    }

    public enum QueueItemStatus { Pending = 1, Processing = 2, Sent = 3, Failed = 4 }
}
