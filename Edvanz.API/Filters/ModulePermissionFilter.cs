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
        private readonly string? _role;
        private readonly IAuthorizationService _authService;

        public ModulePermissionFilter(
    string module,
    string permission,
    string role,
    IAuthorizationService authService)
        {
            _module = module;
            _permission = string.IsNullOrWhiteSpace(permission) ? null : permission;
            _role = string.IsNullOrWhiteSpace(role) ? null : role;
            _authService = authService;
        }
        public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
        {
            var user = context.HttpContext.User;

            if (!string.IsNullOrEmpty(_role) && !user.IsInRole(_role))
            {
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
