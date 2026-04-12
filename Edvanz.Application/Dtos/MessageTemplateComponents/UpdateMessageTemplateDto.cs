using Edvanz.Domain.Enums;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace Edvanz.Application.Dtos.MessageTemplateComponents
{
    public class UpdateMessageTemplateDto
    {
        public long TemplateId { get; set; }

        [MaxLength(100)]
        public string? Name { get; set; }

        public ChannelType? Channel { get; set; }
        public RecipientTarget? RecipientTarget { get; set; }

        /// <summary>
        /// If provided → replaces ALL existing blocks (full replace strategy).
        /// If null → blocks are not touched.
        /// </summary>
        public List<MessageBlockDto>? Blocks { get; set; }
    }
}
