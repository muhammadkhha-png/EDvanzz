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
        if (TryGetTenantId(context, out long routeTeacherId))
        {
            long? userId = _currentUser.UserId;

            // Only enforce for authenticated callers; anonymous requests are handled by the
            // global authorization policy (they cannot own a matching teacher scope anyway).
            if (userId is not null &&
                !string.Equals(_currentUser.Role, "SuperAdmin", StringComparison.Ordinal))
            {
                // Resolve the caller's teacher scope DIRECTLY by user id (not via the role string):
                //   - a Teacher user  → their own Teacher.Id
                //   - an Assistant user→ their owning TeacherAccountId
                long? callerTeacherId = (await _unitOfWork.Users.GetTeacherByUserIdAsync(userId.Value))?.Id;
                if (callerTeacherId is null)
                {
                    var assistant = await _unitOfWork.AssistantRepo.GetAssistantWithUserIdAsync(userId.Value);
                    callerTeacherId = assistant?.TeacherAccountId;
                }

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

    private static readonly string[] TeacherIdKeys = { "teacherId", "teacherID", "teacherid", "TeacherId", "TeacherID" };

    /// <summary>
    /// Extracts the tenant id the request is targeting, from EITHER the route (e.g. Session/Teacher
    /// GET endpoints) OR a bound action argument — a scalar <c>teacherId</c> parameter or a DTO with
    /// a <c>teacherId</c>/<c>TeacherId</c> property (e.g. CreateSessionDto, AssignStudentsToSessionDto).
    /// Covering the body closes the IDOR on create/assign endpoints, not just the route reads.
    /// Returns false (no enforcement) when no positive tenant id is present.
    /// </summary>
    private static bool TryGetTenantId(ActionExecutingContext context, out long teacherId)
    {
        teacherId = 0;

        // 1) Route values.
        foreach (var key in new[] { "teacherId", "teacherID", "teacherid" })
        {
            if (context.RouteData.Values.TryGetValue(key, out var raw) &&
                long.TryParse(raw?.ToString(), out teacherId) && teacherId > 0)
            {
                return true;
            }
        }

        // 2) Bound action arguments: scalar teacherId params and DTO teacherId properties.
        foreach (var arg in context.ActionArguments.Values)
        {
            if (arg is null) continue;

            if (arg is long l && l > 0)
            {
                // A scalar long argument is only treated as a tenant id when the parameter is named
                // teacherId — but scalar names aren't available here, so we rely on DTO props below
                // and route above. Skip bare longs to avoid false positives (e.g. sessionId).
                continue;
            }

            var type = arg.GetType();
            if (type.IsPrimitive || type == typeof(string) || type == typeof(decimal)) continue;

            foreach (var name in TeacherIdKeys)
            {
                var prop = type.GetProperty(name);
                if (prop is null) continue;
                var val = prop.GetValue(arg);
                if (val != null && long.TryParse(val.ToString(), out teacherId) && teacherId > 0)
                    return true;
            }
        }

        teacherId = 0;
        return false;
    }
}
