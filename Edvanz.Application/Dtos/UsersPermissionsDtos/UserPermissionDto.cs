using Edvanz.Application.Dtos.ModulesPermissions;
using System;
using System.Collections.Generic;
using System.Text;

namespace Edvanz.Application.Dtos.UsersPermissionsDtos
{
    public class UserPermissionDto
    {
        public int userId { get; set; }
        public List<ModulePermissionsDto> UserPermissions { get; set; }

    }
  
}
