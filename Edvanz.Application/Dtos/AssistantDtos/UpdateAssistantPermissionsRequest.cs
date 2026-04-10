using System;
using System.Collections.Generic;
using System.Text;

namespace Edvanz.Application.Dtos.AssistantDtos
{
     public class UpdateAssistantPermissionsRequest
        {
        public long assistantId { get; set; }
        public List<long>? permissionIds { get; set; }
            public List<long>? permissionProfileIds { get; set; }
        }
}
