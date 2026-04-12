using Edvanz.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Text;

namespace Edvanz.Application.Dtos.MessageTemplateComponents
{
    public class MessageBlockDto
    {
        public BlockType BlockType { get; set; }
        public DynamicBlockKey? DynamicKey { get; set; }
        public string? CustomText { get; set; }
        public int SortOrder { get; set; }
    }
}
