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
        private readonly IUnitOfWork _unitOfWork;

        public PermissionHandler(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        protected override async Task HandleRequirementAsync(
            AuthorizationHandlerContext context,
            PermissionRequirement requirement)
        {
            var permissions = context.User.Claims
                .Where(c => c.Type.Equals("Permission", System.StringComparison.OrdinalIgnoreCase))
                .Select(c => c.Value)
                .ToList();
            if (permissions.Contains("CompleteProfile", StringComparer.OrdinalIgnoreCase))
            {
                context.Succeed(requirement);
                return;
            }
            var hasAnyRole = context.User.Claims.Any(c => c.Type == ClaimTypes.Role);

            var tokenStamp = context.User.Claims.FirstOrDefault(c => c.Type == "SecurityStamp")?.Value;

            var userIdString = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (!long.TryParse(userIdString, out long userId))
            {
                return;
            }

            var user = await _unitOfWork.Users.GetByIdAsync(userId);
            if (user == null)
                return;

            if (tokenStamp != user.SecurityStamp)
            {
                context.Fail(); 
                return;
            }

            if (permissions.Contains(requirement.RequiredPermission, System.StringComparer.OrdinalIgnoreCase) || hasAnyRole)
            {
                context.Succeed(requirement);
            }

        }
    }
}