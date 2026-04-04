using System;
using System.Collections.Generic;
using System.Text;

namespace Edvanz.Application.Dtos.AssistantDtos
{
     public class UpdateAssistantPermissionsRequest
        {
        public long assistantId { get; set; }
        public List<long>? PermissionIds { get; set; }
            public List<long>? PermissionProfileIds { get; set; }
        }
}
