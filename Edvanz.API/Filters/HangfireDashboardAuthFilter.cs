using Edvanz.Domain.Constants;
using Hangfire.Dashboard;

namespace Edvanz.API.Filters;

/// <summary>
/// Restricts the Hangfire dashboard to authenticated SuperAdmin users only.
///
/// Registered via <c>DashboardOptions.Authorization</c> in <c>Program.cs</c>.
/// The dashboard is mapped AFTER <c>UseAuthentication</c> so
/// <c>HttpContext.User</c> is fully populated by the time this filter runs.
///
/// Belt-and-braces: the App Service Access Restriction for <c>/hangfire</c>
/// (configured in Azure portal) blocks the path at the network layer before
/// this filter is even reached.
/// </summary>
public sealed class HangfireDashboardAuthFilter : IDashboardAuthorizationFilter
{
    /// <inheritdoc />
    public bool Authorize(DashboardContext context)
    {
        var http = context.GetHttpContext();

        // Deny unauthenticated callers immediately.
        if (http.User.Identity?.IsAuthenticated != true)
            return false;

        // Only SuperAdmin may access the dashboard. Reuses the same role
        // constant as PermissionHandler so a rename is compile-time caught.
        return http.User.IsInRole(UserRoles.SuperAdmin);
    }
}