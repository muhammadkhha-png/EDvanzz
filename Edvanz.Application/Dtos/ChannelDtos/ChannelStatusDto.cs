using Edvanz.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Text;

namespace Edvanz.Application.Dtos.ChannelDtos
{
    public class ChannelStatusDto
    {
        public long Id { get; set; }
        public ChannelType ChannelType { get; set; }
        public string ChannelTypeName => ChannelType.ToString();
        public ChannelStatus Status { get; set; }
        public string StatusName => Status.ToString();
        public string? SenderIdOrNumber { get; set; }
        public bool IsActive { get; set; }
        public DateTime? ConnectedAt { get; set; }
    }
}
