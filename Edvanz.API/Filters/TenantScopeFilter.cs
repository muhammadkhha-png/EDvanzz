using Edvanz.Application.IservicesContract;
using Edvanz.Domain.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using System.Reflection;

namespace Edvanz.API.Filters;

/// <summary>
/// Enforces tenant isolation on controllers that carry the tenant identity in the ROUTE or BODY
/// (a <c>teacherId</c> route segment, or a DTO/param named <c>teacherId</c>).
///
/// It does NOT trust the client-supplied value: for a Teacher/Assistant caller it OVERWRITES every
/// teacherId the request carries with the caller's REAL Teacher.Id, resolved from the JWT
/// (Teacher → own id, Assistant → owning TeacherAccountId). This both:
///   - fixes clients that send the wrong id (e.g. the user/account id instead of the Teacher.Id), and
///   - makes cross-tenant access impossible — a caller can only ever operate on their own scope.
/// SuperAdmin is exempt (platform-wide access, uses the supplied id). Anonymous requests pass through
/// to the global authorization policy. No request/response SHAPE changes, so the frontend is unaffected.
/// </summary>
public sealed class TenantScopeFilter : IAsyncActionFilter
{
    private static readonly string[] TeacherIdKeys =
        { "teacherId", "teacherID", "teacherid", "TeacherId", "TeacherID" };

    private readonly ICurrentUserService _currentUser;
    private readonly IUnitOfWork _unitOfWork;

    public TenantScopeFilter(ICurrentUserService currentUser, IUnitOfWork unitOfWork)
    {
        _currentUser = currentUser;
        _unitOfWork = unitOfWork;
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        long? userId = _currentUser.UserId;

        // Only act on authenticated, non-SuperAdmin callers. Anonymous → global policy; SuperAdmin →
        // may target any teacher via the supplied id.
        if (userId is not null &&
            !string.Equals(_currentUser.Role, "SuperAdmin", StringComparison.Ordinal) &&
            RequestCarriesTenantId(context))
        {
            long? callerTeacherId = await ResolveCallerTeacherIdAsync(userId.Value);
            if (callerTeacherId is null)
            {
                // Authenticated but maps to no teacher (e.g. a student/parent token) — must not reach
                // teacher-scoped endpoints.
                context.Result = Forbidden();
                return;
            }

            OverwriteTenantId(context, callerTeacherId.Value);
        }

        await next();
    }

    private async Task<long?> ResolveCallerTeacherIdAsync(long userId)
    {
        long? id = (await _unitOfWork.Users.GetTeacherByUserIdAsync(userId))?.Id;
        if (id is null)
        {
            var assistant = await _unitOfWork.AssistantRepo.GetAssistantWithUserIdAsync(userId);
            id = assistant?.TeacherAccountId;
        }
        return id;
    }

    /// <summary>True if the request carries a teacherId in the route or a named action argument.</summary>
    private static bool RequestCarriesTenantId(ActionExecutingContext context)
    {
        foreach (var key in TeacherIdKeys)
            if (context.RouteData.Values.ContainsKey(key))
                return true;

        foreach (var kv in context.ActionArguments)
        {
            if (kv.Value is null) continue;

            // Scalar param literally named teacherId.
            if (kv.Value is long && IsTeacherIdKey(kv.Key))
                return true;

            // DTO body with a teacherId property.
            var type = kv.Value.GetType();
            if (!IsSimple(type) && FindTeacherIdProperty(type) is not null)
                return true;
        }
        return false;
    }

    /// <summary>Forces every teacherId the request carries to the caller's real Teacher.Id.</summary>
    private static void OverwriteTenantId(ActionExecutingContext context, long teacherId)
    {
        foreach (var key in TeacherIdKeys)
            if (context.RouteData.Values.ContainsKey(key))
                context.RouteData.Values[key] = teacherId.ToString();

        foreach (var key in context.ActionArguments.Keys.ToList())
        {
            var value = context.ActionArguments[key];
            if (value is null) continue;

            if (value is long && IsTeacherIdKey(key))
            {
                context.ActionArguments[key] = teacherId;
                continue;
            }

            var type = value.GetType();
            if (IsSimple(type)) continue;

            var prop = FindTeacherIdProperty(type);
            if (prop is not null && prop.CanWrite)
            {
                var target = Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType;
                if (target == typeof(long))
                    prop.SetValue(value, teacherId);
            }
        }
    }

    private static PropertyInfo? FindTeacherIdProperty(Type type)
    {
        foreach (var name in TeacherIdKeys)
        {
            var prop = type.GetProperty(name);
            if (prop is not null &&
                (Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType) == typeof(long))
                return prop;
        }
        return null;
    }

    private static bool IsTeacherIdKey(string key) =>
        TeacherIdKeys.Any(k => string.Equals(k, key, StringComparison.OrdinalIgnoreCase));

    private static bool IsSimple(Type t) =>
        t.IsPrimitive || t == typeof(string) || t == typeof(decimal) || t == typeof(Guid) ||
        t == typeof(DateTime) || t.IsEnum;

    private static IActionResult Forbidden() =>
        new ObjectResult(new { success = false, message = "Forbidden: cross-tenant access is not allowed." })
        {
            StatusCode = StatusCodes.Status403Forbidden
        };
}
