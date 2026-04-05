using System;
using System.Collections.Generic;
using System.Text;

namespace Edvanz.Application.Dtos.TemplatePermissionsDtos
{
    public class UpdatePermissionsProfileDto
    {
        public long templateId { get; set; }
        public string? profileName { get; set; }
        public List<long>? PermissionsIds { get; set; }
    }
}
