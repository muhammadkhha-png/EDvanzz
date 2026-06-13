using Edvanz.Application.Dtos.AuditTrial;
using Edvanz.Application.IservicesContract;
using Edvanz.Domain.Enums;
using Microsoft.AspNetCore.Mvc.Filters;
using System.Threading.Tasks;

namespace Edvanz.API.Attributes
{
    [AttributeUsage(AttributeTargets.Method)]
    public class AuditTrailAttribute : ActionFilterAttribute
    {
        private readonly ActionType _actionType;
        private readonly long _moduleId;

        public AuditTrailAttribute(ActionType actionType, long moduleId)
        {
            _actionType = actionType;
            _moduleId = moduleId;
        }

        public override async Task OnActionExecutionAsync(
            ActionExecutingContext context,
            ActionExecutionDelegate next)
        {
            var executed = await next();

            // Only audit successful executions (unchanged behavior).
            if (executed.Exception is not null && !executed.ExceptionHandled)
                return;

            var services = context.HttpContext.RequestServices;
            var currentUser = services.GetRequiredService<ICurrentUserService>();
            var assistantData = await currentUser.GetAssistantDataAsync();

            // The audit trail records ASSISTANT actions (REQ-USR-029). If the caller is
            // not an assistant (e.g. the tutor acting directly), there is nobody to
            // attribute the row to — skip rather than NRE on assistantData below.
            if (assistantData is null)
                return;

            var auditService = services.GetRequiredService<IAudittrialService>();
            var auditContext = services.GetRequiredService<IAuditContext>();

            await auditService.RecordAuditTrailAsync(new CreateAuditTrailDto
            {
                teacherId = assistantData.TeacherAccountId,
                assistantId = assistantData.Id,
                actionType = _actionType,
                moduleId = _moduleId,
                desc = BuildDesc(context, auditContext.AffectedRecord),
            });
        }

        /// <summary>
        /// Builds the audit description (REQ-USR-029). Preference order:
        ///   1. The human-readable record label the action/service supplied via
        ///      IAuditContext (e.g. "Student 'Ahmed Mostafa' (#42)").
        ///   2. The endpoint name enriched with the route id, when present.
        ///   3. The bare endpoint name (legacy fallback).
        /// </summary>
        private static string BuildDesc(ActionExecutingContext context, string? affectedRecord)
        {
            var controller = context.RouteData.Values["controller"]?.ToString();
            var action = context.RouteData.Values["action"]?.ToString();
            var endpoint = $"{controller}.{action}";

            if (!string.IsNullOrWhiteSpace(affectedRecord))
                return $"{endpoint} — {affectedRecord}";

            var routeId = context.RouteData.Values["id"]?.ToString();
            return string.IsNullOrWhiteSpace(routeId)
                ? endpoint
                : $"{endpoint} (#{routeId})";
        }
    }
}