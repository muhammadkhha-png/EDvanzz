using Edvanz.Application.ServiceContract;
using Edvanz.Domain.Entities;
using Edvanz.Domain.Interfaces;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using System.Threading.Tasks;
using System.Linq;

namespace Edvanz.Application.Security
{
    public class PermissionHandler : AuthorizationHandler<PermissionRequirement>
    {

        public PermissionHandler()
        {
           
        }

      
        protected override Task HandleRequirementAsync(
       AuthorizationHandlerContext context,
       PermissionRequirement requirement)
        {
            var user = context.User;

            if (user.IsInRole("SuperAdmin"))
            {
                context.Succeed(requirement);
                return Task.CompletedTask;
            }
            else if (user.IsInRole("Student"))
            {
                context.Succeed(requirement);
                return Task.CompletedTask;
            }
          else if (user.IsInRole("Parent"))
            {
                context.Succeed(requirement);
                return Task.CompletedTask;
            }

            var permissions = user.Claims
                .Where(c => c.Type == "Permission")
                .Select(c => c.Value)
                .ToList();

            if (permissions.Contains("CompleteProfile", StringComparer.OrdinalIgnoreCase))
            {
                context.Succeed(requirement);
                return Task.CompletedTask;
            }

           
            var modules = user.Claims
                .Where(c => c.Type == "module")
                .Select(c => c.Value);

            var hasModule = modules.Contains(requirement.Module, StringComparer.OrdinalIgnoreCase);

            if (!hasModule)
            {
                context.Fail();
                return Task.CompletedTask;
            }

            // ✅ 4. لو مفيش permission → زي Teacher 
            if (string.IsNullOrEmpty(requirement.Permission))
            {
                context.Succeed(requirement);
                return Task.CompletedTask;
            }

            // ✅ 5. Check Permission (Assistant فقط)
            var hasPermission = permissions.Contains(requirement.Permission, StringComparer.OrdinalIgnoreCase);

            if (hasPermission)
            {
                context.Succeed(requirement);
            }
            else
            {
                context.Fail();
            }

            return Task.CompletedTask;
        }
    }
}