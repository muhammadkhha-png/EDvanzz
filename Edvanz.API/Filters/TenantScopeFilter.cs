using Edvanz.Application.IservicesContract;
using Edvanz.Domain.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Edvanz.API.Filters;

/// <summary>
/// Enforces tenant isolation on controllers that carry the tenant identity in the ROUTE
/// (<c>{teacherId}</c>). Without this, any authenticated teacher could read/modify another
/// teacher's data simply by changing the id in the URL (confirmed IDOR: teacher2 could read
/// teacher1's sessions). The caller's real teacher scope is derived from the JWT:
///   - Teacher  → their own Teacher.Id
///   - Assistant→ their owning TeacherAccountId
///   - SuperAdmin → bypasses (platform-wide access)
/// If the route teacherId does not match the caller's scope, the request is rejected with 403.
///
/// This does NOT change any request/response shape, so the connected frontend is unaffected —
/// it already sends the caller's own teacherId. Genuinely anonymous calls (no JWT) are passed
/// through so the global authorization policy remains the gate for those.
/// </summary>
public sealed class TenantScopeFilter : IAsyncActionFilter
{
    private readonly ICurrentUserService _currentUser;
    private readonly IUnitOfWork _unitOfWork;

    public TenantScopeFilter(ICurrentUserService currentUser, IUnitOfWork unitOfWork)
    {
        _currentUser = currentUser;
        _unitOfWork = unitOfWork;
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        if (TryGetRouteTeacherId(context, out long routeTeacherId))
        {
            long? userId = _currentUser.UserId;

            // Only enforce for authenticated callers; anonymous requests are handled by the
            // global authorization policy (they cannot own a matching teacher scope anyway).
            if (userId is not null &&
                !string.Equals(_currentUser.Role, "SuperAdmin", StringComparison.Ordinal))
            {
                long? callerTeacherId;
                if (string.Equals(_currentUser.Role, "Assistant", StringComparison.Ordinal))
                    callerTeacherId = (await _currentUser.GetAssistantDataAsync())?.TeacherAccountId;
                else
                    callerTeacherId = (await _unitOfWork.Users.GetTeacherByUserIdAsync(userId.Value))?.Id;

                if (callerTeacherId is null || callerTeacherId.Value != routeTeacherId)
                {
                    context.Result = new ObjectResult(new
                    {
                        success = false,
                        message = "Forbidden: cross-tenant access is not allowed."
                    })
                    {
                        StatusCode = StatusCodes.Status403Forbidden
                    };
                    return;
                }
            }
        }

        await next();
    }

    private static bool TryGetRouteTeacherId(ActionExecutingContext context, out long teacherId)
    {
        teacherId = 0;
        // Match common casings used across the route templates.
        foreach (var key in new[] { "teacherId", "teacherID", "teacherid" })
        {
            if (context.RouteData.Values.TryGetValue(key, out var raw) &&
                long.TryParse(raw?.ToString(), out teacherId))
            {
                return true;
            }
        }
        return false;
    }
}
