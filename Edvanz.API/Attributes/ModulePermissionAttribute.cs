using Edvanz.API.Filters;
using Microsoft.AspNetCore.Mvc;

namespace Edvanz.API.Attributes
{
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = true)]
    public class ModulePermissionAttribute : TypeFilterAttribute
    {
        public ModulePermissionAttribute(
            string module,
            string? permission = null,
            string? role = null)
            : base(typeof(ModulePermissionFilter))
        {
            Arguments = new object[]
            {
            module ?? "",
            permission ?? "",
            role ?? ""
            };
        }
    }
}
