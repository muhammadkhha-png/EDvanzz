using Edvanz.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Text;

namespace Edvanz.Application.Dtos.MessageTemplateComponents
{
    public class MessageBlockResponseDto
    {
        public long Id { get; set; }
        public BlockType BlockType { get; set; }
        public DynamicBlockKey? DynamicKey { get; set; }
        public string? CustomText { get; set; }
        public int SortOrder { get; set; }
        public string PreviewLabel { get; set; } = string.Empty;  // e.g. "[Student Name]"
    }
}
