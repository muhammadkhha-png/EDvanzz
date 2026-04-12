using Edvanz.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Text;

namespace Edvanz.Application.Dtos.MessageTemplateComponents
{
    public class MessageTemplateDetailDto
    {
        public long Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public ChannelType Channel { get; set; }
        public RecipientTarget RecipientTarget { get; set; }
        public bool IsActive { get; set; }
        public List<MessageBlockResponseDto> Blocks { get; set; } = new();
        public string Preview { get; set; } = string.Empty;   // REQ-MSG-013
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}
