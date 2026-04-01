using Edvanz.Application.Dtos.PermissionsDtos;
using System;
using System.Collections.Generic;
using System.Text;

namespace Edvanz.Application.Dtos.ModulesPermissions
{
    public class ModulePermissionsDto
    {
        public long id { get; set; }
        public string ModuleName { get; set; }

        public List<PermissionDto> permissions { get; set; }
    }
}
