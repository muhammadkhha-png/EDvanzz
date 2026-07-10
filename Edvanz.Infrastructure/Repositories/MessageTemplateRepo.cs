using Edvanz.Domain.Entities.Messaging;
using Edvanz.Domain.Interfaces;
using Edvanz.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Text;

namespace Edvanz.Infrastructure.Repositories
{
    public class MessageTemplateRepo : GenericRepo<MessageTemplate, long>, IMessageTemplateRepo
    {
        public MessageTemplateRepo(EdvanzDbContext context) : base(context)
        {
        }

        /// <inheritdoc />
        public async Task<int> CountByTeacherAsync(long teacherId)
        {
            return await _context.MessageTemplates.CountAsync(t => t.TeacherId == teacherId);
        }

        public async Task<MessageTemplate?> GetByIdWithBlocksAsync(long id)
        {
            return await _context.MessageTemplates.Include(t=>t.Blocks).AsNoTracking().FirstOrDefaultAsync(t=>t.Id==id);
        }
        public async Task<(IReadOnlyList<MessageTemplate> Templates, int count)> GetAllPerTeacherAsync(
       long teacherId,
       string? templateName,
       int? page ,
       int? pageSize )
        {
            var query = _context.MessageTemplates
                .AsNoTracking()
                .Where(t => t.TeacherId == teacherId);

            if (!string.IsNullOrWhiteSpace(templateName))
            {
                query = query.Where(t => t.Name.Contains(templateName));
            }

            var count = await query.CountAsync();

            if (page.HasValue && pageSize.HasValue)
            {
                if (page <= 0) page = 1;
                if (pageSize <= 0) pageSize = 10;

                query = query
                    .OrderByDescending(t => t.CreateAt)
                    .Skip((page.Value - 1) * pageSize.Value)
                    .Take(pageSize.Value);
            }
            else
            {
                query = query.OrderByDescending(t => t.CreateAt);
            }

            var templates = await query.ToListAsync();

            return (templates, count);
        }

        public Task<bool> NameExistsAsync(long teacherId, string name, long? excludeId = null)
        {
            return _context.MessageTemplates
                .AnyAsync(m =>
                    m.TeacherId == teacherId &&
                    m.Name == name &&
                    (!excludeId.HasValue || m.Id != excludeId.Value)
                );
        }
        public async Task<IReadOnlyList<MessageTemplate>> GetByIdsAsync(List<long> ids)
        {
            return await _context.MessageTemplates
                .AsNoTracking()
                .Where(t => ids.Contains(t.Id))
                .ToListAsync();
        }

        public Task<MessageTemplate> GetByIdAndTeacherIdAsync(long teacherId, long templateId)
        {
           return _context.MessageTemplates.AsNoTracking().FirstOrDefaultAsync(t=>t.TeacherId==teacherId && t.Id==templateId);
        }
    }
}
