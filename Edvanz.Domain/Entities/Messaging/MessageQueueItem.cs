using Edvanz.Domain.Entities.ShareProp;
using Edvanz.Domain.Enums;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;

namespace Edvanz.Domain.Entities.Messaging
{
 
    public enum QueueItemStatus { Pending = 1, Processing = 2, Sent = 3, Failed = 4 }
}
