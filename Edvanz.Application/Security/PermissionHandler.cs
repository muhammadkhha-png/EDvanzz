using Edvanz.Application.ServiceContract;
using Edvanz.Domain.Entities;
using Edvanz.Domain.Interfaces;
using Microsoft.AspNetCore.Authorization;
using System.Data;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

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
            if (!string.IsNullOrEmpty(requirement.RoleOnly))
            {
                if (user.IsInRole(requirement.RoleOnly))
                {
                    context.Succeed(requirement);
                    return Task.CompletedTask;
                }
                context.Fail();
                return Task.CompletedTask;
            }
            // ── SuperAdmin → bypass كامل ──────────────────────────────────
            if (user.IsInRole("SuperAdmin")|| user.IsInRole("Student") || user.IsInRole("Parent"))
            {
                context.Succeed(requirement);
                return Task.CompletedTask;
            }

            // ── CompleteProfile → endpoint محدد بس، مش bypass كامل ────────
            var permissions = user.Claims
                .Where(c => c.Type == "Permission")
                .Select(c => c.Value)
                .ToList();

            if (permissions.Contains("CompleteProfile", StringComparer.OrdinalIgnoreCase))
            {
                // ✅ يعدي بس لو الـ endpoint نفسه هو CompleteProfile
                if (requirement.Permission == "CompleteProfile")
                {
                    context.Succeed(requirement);
                    return Task.CompletedTask;
                }
                context.Fail();
                return Task.CompletedTask;
            }

           

            // ── Teacher / Assistant → لازم module ────────────────────────
            var modules = user.Claims
                .Where(c => c.Type == "module")
                .Select(c => c.Value);

            if (!string.IsNullOrEmpty(requirement.Module)) // 🔹 تحقق بس لو فيه module محدد
            {
                if (modules == null || !modules.Contains(requirement.Module, StringComparer.OrdinalIgnoreCase))
                {
                    context.Fail();
                    return Task.CompletedTask;
                }
            }

            // ── Teacher → module كافي ─────────────────────────────────────
            if (user.IsInRole("Teacher"))
            {
                context.Succeed(requirement); // 🔥 ده اللي ناقص
                return Task.CompletedTask;
            }

            // ── Assistant → module + permission ──────────────────────────
            if (string.IsNullOrEmpty(requirement.Permission))
            {
                context.Fail();
                return Task.CompletedTask;
            }

            if (permissions.Contains(requirement.Permission, StringComparer.OrdinalIgnoreCase))
                context.Succeed(requirement);
            else
                context.Fail();

            return Task.CompletedTask;
        }
    }
}