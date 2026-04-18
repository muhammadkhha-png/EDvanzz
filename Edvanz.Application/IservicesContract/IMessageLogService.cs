using Edvanz.Application.Dtos;
using Edvanz.Application.Dtos.DispatcherDtos;
using Edvanz.Application.Dtos.MessageLogDtos;
using Edvanz.Domain.Entities.Messaging;
using Edvanz.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Text;

namespace Edvanz.Application.IservicesContract
{
    public interface IMessageLogService
    {
        Task<Result<PaginatedResponse<List<MessageLogDto>>>> GetHistoryAsync(MessageLogQueryDto query);
        Task<Result<string>> ResendAsync(long teacherId, long messageLogId);
        Task SaveAsync(MessageSendPayload payload, bool success, string? error);
        public Task LogMissingPhoneAsync(
 long teacherId,
 long studentId,
 string studentName,
                                      
 string studentCode,
 RecipientTarget recipientType,
 long messageTemplateId,

 ChannelType channel);
    }
}
