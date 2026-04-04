using Edvanz.Application.Dtos;
using Edvanz.Application.Dtos.AssistantDtos;
using Edvanz.Domain.Enums;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Text;

namespace Edvanz.Application.IservicesContract
{
    public interface IAssistantService
    {
        public Task<Result<string>> UpdateAssistantPermissionsAsync( UpdateAssistantPermissionsRequest dto);
        public Task<Result<PaginatedResponse<List<AssistantListDto>>>> GetAssistantListPerTeacher(AssistantPerTeacherFilterDto req);
        public Task<Result<AssistantDto>> GetByAssistantIdAsync(long id);
        /// <summary>
        /// Creates a User (UserType.Assistant) + Assistant row + permission/template links.
        /// Called by POST /api/assistants.
        /// </summary>
       public Task<Result<string>> InitializeAssistantAsync(CreateAssistantDto dto);

        public Task<Result<string?>> UpdateAssistantAsync( UpdateAssistantRequest dto);
        public Task<Result<string>> ToggleStatus(ToggleAccountStatus req);
        public Task<Result<List<LoginActivityDto>>> GetLoginActivityAsync(long assistantId);
        public  Task RecordLoginActivityAsync( long assistantId,LoginAcitvityActionType actionType,HttpContext httpContext);


    }
}
    