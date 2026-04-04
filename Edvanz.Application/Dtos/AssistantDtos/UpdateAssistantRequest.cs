using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Security;
using System.Text;

namespace Edvanz.Application.Dtos.AssistantDtos
{
    public class UpdateAssistantRequest
    {
        public long assistantId { get; set; }
        [MaxLength(150)]
        public string? fullName { get; set; }

        [MaxLength(50)]
        public string? username { get; set; }

        public string? newPassword { get; set; }
        public string? phoneNumber { get; set; }
        [EmailAddress]
        public string? email { get; set; }

        public List<long>? PermissionProfileIds { get; set; }

        public List<long>? PermissionsId { get; set; }
    }
}
