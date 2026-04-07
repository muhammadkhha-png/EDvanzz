using Edvanz.API.Filters;
using Microsoft.AspNetCore.Mvc;

namespace Edvanz.API.Attributes
{
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = false)]
    public class ModulePermissionAttribute : TypeFilterAttribute
    {
        public ModulePermissionAttribute(
            string module = "",
            string? permission = null,
            string[]? roles = null,
            bool roleOnly = false)
            : base(typeof(ModulePermissionFilter))
        {
            Arguments = new object[]
            {
            module ?? "",
            permission ?? "",
            roles ?? Array.Empty<string>(),
            roleOnly
            };
        }
    }
}
