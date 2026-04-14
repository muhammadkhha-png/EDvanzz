using Edvanz.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Text;

namespace Edvanz.Application.Dtos.ChannelDtos
{
    public class UpsertChannelDto
    {
        public long teacherID { get; set; }
        public ChannelType ChannelType { get; set; }
        
        // WhatsApp: the connected phone number.
        // SMS: the sender ID or gateway sender name.
        public string SenderIdOrNumber { get; set; } = string.Empty;
        // WhatsApp: AsPI token / secret.
        // SMS: gateway API key.
        // Stored encrypted — never returned to client 
        public string Credentials { get; set; } = string.Empty;
    }
}
