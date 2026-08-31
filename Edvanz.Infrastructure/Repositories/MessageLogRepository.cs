using Edvanz.Domain.Entities.Messaging;
using Edvanz.Domain.Enums;
using Edvanz.Domain.Helpers;
using Edvanz.Domain.Interfaces;
using Edvanz.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Text;

namespace Edvanz.Infrastructure.Repositories
{
    public class MessageLogRepository : GenericRepo<MessageLog, long>, IMessageLogRepository
    {
        public MessageLogRepository(EdvanzDbContext context) : base(context)
        {
        }

        public async Task<(List<MessageLog> Data, int TotalCount)> GetPagedAsync(
            long teacherId,
            string? studentName,
            string? studentCode,
            DateTime? from,
            DateTime? to,
            TriggerEventType? triggerType,
            MessageStatus? status,
            ChannelType? channel,
            int page,
            int pageSize)
        {
            var query = _context.MessageLogs.AsQueryable();

            query = query.Where(x => x.TeacherId == teacherId);

            if (!string.IsNullOrWhiteSpace(studentName))
            {
                var nameTerm = ArabicTextNormalizer.Normalize(studentName.Trim());
                query = query.Where(x => DbSearch.ArabicNormalize(x.StudentName).Contains(nameTerm));
            }

            if (!string.IsNullOrWhiteSpace(studentCode))
            {
                var codeTerm = ArabicTextNormalizer.Normalize(studentCode.Trim());
                query = query.Where(x => DbSearch.ArabicNormalize(x.StudentCode).Contains(codeTerm));
            }

            if (from.HasValue)
                query = query.Where(x => x.CreateAt >= from.Value);

            if (to.HasValue)
                query = query.Where(x => x.CreateAt <= to.Value);

            if (triggerType.HasValue)
                query = query.Where(x => x.TriggerType == triggerType);

            if (status.HasValue)
                query = query.Where(x => x.Status == status);

            if (channel.HasValue)
                query = query.Where(x => x.Channel == channel);

            var totalCount = await query.CountAsync();

            var data = await query
                .OrderByDescending(x => x.CreateAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return (data, totalCount);
        }
    }
}
