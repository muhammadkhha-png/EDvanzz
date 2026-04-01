using System;
using System.Collections.Generic;
using System.Text;

namespace Edvanz.Application.Dtos.PermissionsDtos
{
    public class PermissionDto
    {
        public long permissionId { get; set; }
        public string permissionName { get; set; }
        public bool isRestricted { get; set; }

    }
}
