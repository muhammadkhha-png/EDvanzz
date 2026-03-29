using System;
using System.Collections.Generic;
using System.Text;

namespace Edvanz.Application.Dtos.ModulesPermissions
{
    public class ModulePermissionsDto
    {
        public string ModuleName { get; set; }

        public List<string> Permissions { get; set; }
    }
}
