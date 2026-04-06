using Edvanz.Application.Dtos.AssistantDtos;
using Edvanz.Application.Dtos.AuditTrial;
using Edvanz.Application.IservicesContract;
using Edvanz.Application.Services;
using Edvanz.Domain.Entities;
using Edvanz.Domain.Enums;
using Edvanz.Domain.Interfaces;
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
            var result = await next(); 

            if (result.Exception is null || result.ExceptionHandled)
            {
                var auditService = context.HttpContext.RequestServices.GetRequiredService<IAudittrialService>();
                var currentUser = context.HttpContext.RequestServices.GetRequiredService<ICurrentUserService>();
                var assistantData = await currentUser.GetAssistantDataAsync();



                await auditService.RecordAuditTrailAsync(new CreateAuditTrailDto
                {
                    teacherId = assistantData.TeacherAccountId,
                    assistantId = assistantData.Id,
                    actionType = _actionType,
                    moduleId = _moduleId,
                    desc = BuildDesc(context),
                });
            }
        }

       
       
        private static string BuildDesc(ActionExecutingContext context)
        {
            var controller = context.RouteData.Values["controller"]?.ToString();
            var action = context.RouteData.Values["action"]?.ToString();
            return $"{controller}.{action}";
        }
    }
}
