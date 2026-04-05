using System;
using System.Collections.Generic;
using System.Text;

namespace Edvanz.Application.Dtos.TemplatePermissionsDtos
{
    public class CreatePermissionsProfileDto
    {
        public string profileName { get; set; }
        public long teacherId { get; set; }

        public List<long> PermissionsIds { get; set; }=new List<long>();
    }
}
