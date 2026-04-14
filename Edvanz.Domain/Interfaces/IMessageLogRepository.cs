using Edvanz.Domain.Entities.Messaging;
using Edvanz.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Text;

namespace Edvanz.Domain.Interfaces
{
    public interface IMessageLogRepository :IGenericRepo<MessageLog,long>
    {
        Task<(List<MessageLog> Data, int TotalCount)> GetPagedAsync(
        long teacherId,
        string? studentName,
        string? studentCode,
        DateTime? from,
        DateTime? to,
        TriggerEventType? triggerType,
        MessageStatus? status,
        ChannelType? channel,
        int page,
        int pageSize);
    }
}
