using Edvanz.Application.Dtos.PermissionsDtos;
using System;
using System.Collections.Generic;
using System.Text;

namespace Edvanz.Application.Dtos.TemplatePermissionsDtos
{
    public class ProfilePermissionDto
    {
        public long profileId { get; set; }
        public string profileName { get; set; }
        public List<PermissionDto>? Permissions { get; set; }
    }
}
