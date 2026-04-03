using Edvanz.Application.Dtos;
using Edvanz.Application.Dtos.AssistantDtos;
using System;
using System.Collections.Generic;
using System.Text;

namespace Edvanz.Application.IservicesContract
{
    public interface IAssistantService
    {
        public Task<Result<PaginatedResponse<List<AssistantListDto>>>> GetAssistantListPerTeacher(AssistantPerTeacherFilterDto req);
        public Task<Result<AssistantDto>> GetByAssistantIdAsync(long id);
        /// <summary>
        /// Creates a User (UserType.Assistant) + Assistant row + permission/template links.
        /// Called by POST /api/assistants.
        /// </summary>
        Task<Result<string>> InitializeAssistantAsync(CreateAssistantDto dto);
    }
}
