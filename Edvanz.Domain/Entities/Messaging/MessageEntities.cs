using Edvanz.Domain.Entities.ShareProp;
using Edvanz.Domain.Enums;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;

namespace Edvanz.Domain.Entities.Messaging
{
    
    public class MessageTemplate : BaseEntity
    {
        [ForeignKey(nameof(Teacher))]
        public long TeacherId { get; set; }
        public Teacher Teacher { get; set; } = null!;

        public string Name { get; set; } = string.Empty;  

      
        public ChannelType Channel { get; set; }

      
        public RecipientTarget RecipientTarget { get; set; }

        public bool IsActive { get; set; } = true;
        public DateTime UpdatedAt { get; set; }

        public virtual ICollection<MessageBlock> Blocks { get; set; } = new List<MessageBlock>();
        public virtual ICollection<AutomatedTrigger> Triggers { get; set; } = new List<AutomatedTrigger>();
    }

   
    public class MessageBlock : BaseEntity
    {
        [ForeignKey(nameof(MessageTemplate))]
        public long MessageTemplateId { get; set; }
        public MessageTemplate MessageTemplate { get; set; } = null!;

        public BlockType BlockType { get; set; }     // Dynamic | CustomText
        public DynamicBlockKey? DynamicKey { get; set; }  // StudentName | PaymentAmount | ...
        public string? CustomText { get; set; }       // للـ free-form text blocks بس

        public int SortOrder { get; set; }             // ترتيب الـ blocks في الـ template
    }

    
}
