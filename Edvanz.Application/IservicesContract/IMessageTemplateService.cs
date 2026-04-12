using Edvanz.Application.Dtos;
using Edvanz.Application.Dtos.MessageTemplateComponents;
using System;
using System.Collections.Generic;
using System.Text;

namespace Edvanz.Application.IservicesContract
{

    public interface IMessageTemplateService
    {
        Task<Result<PaginatedResponse<List<MessageTemplateSummaryDto>>>> GetAllAsync(TemplatesfilterReqDto req);
        Task<Result<MessageTemplateDetailDto>> GetByIdAsync(long teacherId, long templateId);
        Task<Result<string>> CreateAsync(long teacherId, CreateMessageTemplateDto dto);
        Task<Result<string>> UpdateAsync(long teacherId, UpdateMessageTemplateDto dto);
        Task<Result<string>> DeleteAsync(long teacherId, long templateId);
        Task<Result<string>> ToggleActiveAsync(long teacherId, long templateId, bool activate);
    }
}
