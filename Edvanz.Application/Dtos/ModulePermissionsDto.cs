using System;
using System.Collections.Generic;
using System.Text;

namespace Edvanz.Application.Dtos
{
    public class ModulePermissionsDto
    {
        public string ModuleName { get; set; }

        public List<string> Permissions { get; set; }
    }
}
