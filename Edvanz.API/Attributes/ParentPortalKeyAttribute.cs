using Edvanz.API.Filters;
using Microsoft.AspNetCore.Mvc;

namespace Edvanz.API.Attributes;

/// <summary>
/// Applies <see cref="ParentPortalKeyFilter"/>: the platform kill switch plus the constant-time
/// <c>X-Portal-Key</c> shared-secret check. Put it on the CLASS of any controller whose routes are
/// reachable without a JWT by the public parent portal — it is the only gate those routes have.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
public sealed class ParentPortalKeyAttribute : TypeFilterAttribute
{
    public ParentPortalKeyAttribute() : base(typeof(ParentPortalKeyFilter))
    {
    }
}
