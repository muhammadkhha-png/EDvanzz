using Microsoft.AspNetCore.Authorization;
using System;
using System.Collections.Generic;
using System.Text;

namespace Edvanz.Application.Security
{
    public class PermissionRequirement : IAuthorizationRequirement
    {
        public string Module { get; }
        public string? Permission { get; }

        public PermissionRequirement(string module, string? permission = null)
        {
            Module = module;
            Permission = permission;
        }
    }
}
