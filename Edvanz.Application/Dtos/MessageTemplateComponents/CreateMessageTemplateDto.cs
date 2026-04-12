using Edvanz.Domain.Enums;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace Edvanz.Application.Dtos.MessageTemplateComponents
{
    public class CreateMessageTemplateDto
    {
        [Required, MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        public ChannelType Channel { get; set; }
        public RecipientTarget RecipientTarget { get; set; }

        [Required]
        public List<MessageBlockDto> Blocks { get; set; } = new();
    }
}
