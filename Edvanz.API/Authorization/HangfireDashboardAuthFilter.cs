using Edvanz.Domain.Constants;
using Hangfire.Dashboard;

namespace Edvanz.API.Authorization;

/// <summary>
/// Hangfire dashboard authorization guard (B-SEC-1).
///
/// Restricts the <c>/hangfire</c> dashboard to authenticated SuperAdmin
/// accounts only. Integrates with Hangfire's own request pipeline via
/// <see cref="IDashboardAuthorizationFilter"/>, which Hangfire invokes after
/// ASP.NET Core's authentication middleware has already populated
/// <c>HttpContext.User</c> — guaranteed by positioning
/// <c>app.UseHangfireDashboard</c> AFTER <c>app.UseAuthorization()</c>
/// in <c>Program.cs</c>.
///
/// THREAT MODEL:
///   Without this filter anyone who discovers the dashboard URL can:
///   - Read every enqueued/failed job's serialised arguments (teacher IDs,
///     student phone numbers, payment references, subscription IDs).
///   - Requeue, delete, or manually trigger jobs — including sensitive flows
///     such as payment-expiry sweeps and WhatsApp outbound sends.
///   This filter closes that exposure at the application layer.
///
/// DEFENCE IN DEPTH:
///   This is the in-process layer. Pair it with an Azure App Service
///   Access Restriction on the <c>/hangfire</c> path limited to your admin
///   IP (or a VNet/private endpoint) for network-layer protection. The two
///   controls are independent — either can fail without breaking the other.
///
/// PIPELINE INVARIANT:
///   <c>app.UseHangfireDashboard</c> MUST appear after
///   <c>app.UseAuthentication()</c>, <c>SecurityStampValidationMiddleware</c>,
///   and <c>app.UseAuthorization()</c> in Program.cs so that the JWT bearer
///   token is parsed and <c>HttpContext.User</c> is populated when
///   <see cref="Authorize"/> is called. If the order is wrong, the User
///   principal will be anonymous and every request will be denied with 401.
/// </summary>
public sealed class HangfireDashboardAuthFilter : IDashboardAuthorizationFilter
{
    /// <summary>
    /// Called by Hangfire on every request to a dashboard route.
    /// Returns <see langword="true"/> only when the request carries a valid,
    /// non-expired JWT that belongs to an authenticated SuperAdmin user.
    ///
    /// Any other caller — anonymous, Teacher, Student, Assistant, Parent —
    /// receives an HTTP 401 from Hangfire's own dashboard middleware.
    /// </summary>
    /// <param name="context">
    /// Hangfire dashboard context. <c>context.GetHttpContext()</c> provides
    /// the underlying <see cref="Microsoft.AspNetCore.Http.HttpContext"/>
    /// populated by the ASP.NET Core pipeline (requires
    /// <c>Hangfire.AspNetCore</c> package which is already referenced).
    /// </param>
    /// <returns>
    /// <see langword="true"/> if the user is authenticated and in the
    /// <c>SuperAdmin</c> role; <see langword="false"/> otherwise.
    /// </returns>
    public bool Authorize(DashboardContext context)
    {
        var httpContext = context.GetHttpContext();

        // Both conditions are required:
        //   IsAuthenticated — ensures the JWT was present, valid, and non-expired.
        //   IsInRole        — ensures the token belongs to a SuperAdmin, not a Teacher/Assistant.
        // IsInRole returns false (not throws) for an unauthenticated principal,
        // so the IsAuthenticated guard is technically redundant but makes intent explicit.
        return httpContext.User.Identity?.IsAuthenticated == true
            && httpContext.User.IsInRole(UserRoles.SuperAdmin);
    }
}
