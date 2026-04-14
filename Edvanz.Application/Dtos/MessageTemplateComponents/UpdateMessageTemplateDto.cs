using Edvanz.Domain.Enums;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace Edvanz.Application.Dtos.MessageTemplateComponents
{
    public class UpdateMessageTemplateDto
    {
        public long teacherID { get; set; }
        public long templateId { get; set; }

        [MaxLength(100)]
        public string? name { get; set; }

        public ChannelType? channel { get; set; }
        public RecipientTarget? recipientTarget { get; set; }

        /// <summary>
        /// If provided → replaces ALL existing blocks (full replace strategy).
        /// If null → blocks are not touched.
        /// </summary>
        public List<MessageBlockDto>? blocks { get; set; }
    }
}
