using Edvanz.Application.Dtos;
using Edvanz.Application.Dtos.MessageLogDtos;
using System;
using System.Collections.Generic;
using System.Text;

namespace Edvanz.Application.IservicesContract
{
    public interface IMessageLogService
    {
        Task<Result<PaginatedResponse<List<MessageLogDto>>>> GetHistoryAsync(MessageLogQueryDto query);
        Task<Result<string>> ResendAsync(long teacherId, long messageLogId);
    }
}
