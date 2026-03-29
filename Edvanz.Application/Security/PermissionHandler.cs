using Microsoft.AspNetCore.Authorization;
using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Text;

namespace Edvanz.Application.Security
{
    public class PermissionHandler:AuthorizationHandler<PermissionRequirement>
    {
        protected override Task HandleRequirementAsync(
       AuthorizationHandlerContext context,
       PermissionRequirement requirement)
        {
            var permissions = context.User.Claims
                .Where(c => c.Type == "Permission")
                .Select(c => c.Value)
                .ToList();

            var role = context.User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Role)?.Value;

            var tokenStamp = context.User.Claims.FirstOrDefault(c => c.Type == "SecurityStamp")?.Value;

           
            if (permissions.Contains(requirement.RequiredPermission) ||
                role == "SuperAdmin") 
            {
                context.Succeed(requirement);
            }

            return Task.CompletedTask;
        }
    }
}
