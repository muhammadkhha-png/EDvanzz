using Edvanz.Domain.Enums;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace Edvanz.Application.Dtos.MessageTemplateComponents
{
    public class CreateMessageTemplateDto
    {
        public long teacherID { get; set; }
        [Required, MaxLength(100)]
        public string name { get; set; } = string.Empty;

        public ChannelType channel { get; set; }
        public RecipientTarget recipientTarget { get; set; }

        [Required]
        public List<MessageBlockDto> blocks { get; set; } = new();
    }
}
