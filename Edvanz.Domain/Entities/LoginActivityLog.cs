using Edvanz.Domain.Entities.ShareProp;
using Edvanz.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Text;

namespace Edvanz.Domain.Entities
{
    public class LoginActivityAssistantLog:BaseEntity
    {
        public long AssistantId { get; set; }
        public Assistant assistant { get; set; }
        public LoginAcitvityActionType ActionType { get; set; }
        public DateTime CreateAt { get; set; } = DateTime.UtcNow;
    }
}
