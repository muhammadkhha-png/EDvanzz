using Edvanz.Application.Dtos.ModulesPermissions;
using System;
using System.Collections.Generic;
using System.Text;

namespace Edvanz.Application.Dtos.UsersPermissionsDtos
{
    public class UserPermissionDto
    {
        public int UserId { get; set; }
        public string UserName { get; set; }

        public List<ModulePermissionsDto> Modules { get; set; }
    }
  
}
