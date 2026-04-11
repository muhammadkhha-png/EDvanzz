using Edvanz.Domain.Entities.ShareProp;
using Edvanz.Domain.Enums;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;

namespace Edvanz.Domain.Entities.Messaging
{
    public class MessagingChannel : BaseEntity
    {
        [ForeignKey(nameof(Teacher))]
        public long TeacherId { get; set; }
        public Teacher Teacher { get; set; } = null!;

        public ChannelType ChannelType { get; set; }   // WhatsApp | SMS
        public bool IsActive { get; set; } = false;
        public ChannelStatus Status { get; set; } = ChannelStatus.Disconnected;

        // WhatsApp: connected number | SMS: sender ID or gateway credentials(Saved Encrypted)
        public string? EncryptedCredentials { get; set; }
        public string? SenderIdOrNumber { get; set; }

        public DateTime? ConnectedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

   
    
}
