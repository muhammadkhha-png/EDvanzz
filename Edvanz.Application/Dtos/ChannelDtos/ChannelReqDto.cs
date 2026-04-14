using Edvanz.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Text;

namespace Edvanz.Application.Dtos.ChannelDtos
{
    public class ChannelReqDto
    {
        public long teacherID { get; set; }
        public ChannelType type { get; set; }
    }
}
