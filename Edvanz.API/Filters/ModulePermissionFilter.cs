using Edvanz.Application.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Edvanz.API.Filters
{
    public class ModulePermissionFilter : IAsyncAuthorizationFilter
    {
        private readonly string _module;
        private readonly string? _permission;
        private readonly IAuthorizationService _authService;
        private readonly bool _roleOnly;
        private readonly string[] _roles;

        public ModulePermissionFilter(
    string module,
    string permission,
    string[] roles,
    IAuthorizationService authService, bool roleOnly = false)
        {
            _module = module;
            _permission = string.IsNullOrWhiteSpace(permission) ? null : permission;
            _roles = roles ?? Array.Empty<string>();

            _authService = authService;
            _roleOnly = roleOnly;
            

        }
        public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
        {
            var user = context.HttpContext.User;

            if (_roleOnly)
            {
                if (_roles.Any(r => user.IsInRole(r)))
                {
                    return; 
                }
                context.Result = new ForbidResult();
                return;
            }

            // ✅ الترتيب الصح: module الأول
            var requirement = new PermissionRequirement(_module, _permission);
            var result = await _authService.AuthorizeAsync(user, null, requirement);

            if (!result.Succeeded)
                context.Result = new ForbidResult();
        }
        //public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
        //{
        //    var user = context.HttpContext.User;

        //    // Role check (optional)
        //    if (!string.IsNullOrEmpty(_role) && !user.IsInRole(_role))
        //    {
        //        context.Result = new ForbidResult();
        //        return;
        //    }

        //    // Module + Permission check via PermissionRequirement
        //    var requirement = new PermissionRequirement(_permission, _module);
        //    var result = await _authService.AuthorizeAsync(user, null, requirement);

        //    if (!result.Succeeded)
        //    {
        //        context.Result = new ForbidResult();
        //    }
        //}
    }
}
