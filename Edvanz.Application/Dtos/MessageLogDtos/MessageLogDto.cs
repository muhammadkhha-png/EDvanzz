using System;
using System.Collections.Generic;
using System.Text;

namespace Edvanz.Application.Dtos.MessageLogDtos
{
    public class MessageLogDto
    {
        public long Id { get; set; }
        public string StudentName { get; set; } = string.Empty;
        public string RecipientPhone { get; set; } = string.Empty;
        public string RecipientType { get; set; } = string.Empty;
        public string Channel { get; set; } = string.Empty;
        public string ResolvedContent { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string? FailureReason { get; set; }
        public string? TriggerType { get; set; }
        public bool IsManual { get; set; }
        public DateTime SentAt { get; set; }
        public DateTime? DeliveredAt { get; set; }
    }

}
