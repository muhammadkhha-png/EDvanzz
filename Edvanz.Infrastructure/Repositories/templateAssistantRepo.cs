using Edvanz.Domain.Entities;
using Edvanz.Domain.Interfaces;
using Edvanz.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Text;

namespace Edvanz.Infrastructure.Repositories
{
    public class templateAssistantRepo : GenericRepo<TemplatePermissionsUsers, (long, long)>, ITemplateAssistantsRepo
    {
        public templateAssistantRepo(EdvanzDbContext context) : base(context)
        {
        }

        public async Task<IReadOnlyList<TemplatePermissionsUsers>> GetAllAssistantsPerTemplateAsyns(long TemplateId)
        {
            return await _context.TemplatesPermissionsOfUsers
        .AsNoTracking()
        .Include(tu => tu.assissntat)
        .Where(tu => tu.TemplateId == TemplateId)
        .ToListAsync();
        }

        public async Task<IReadOnlyList<TemplatePermissionsUsers>> GetOtherTemplateLinksForAssistantAsync(
            List<long> userIds,
            long templateId)
        {
            var assistantIds = await _context.Set<Assistant>()
                .AsNoTracking()
                .Where(a => userIds.Contains(a.UserId))
                .Select(a => a.Id)
                .ToListAsync();

            return await _context.TemplatesPermissionsOfUsers
                .AsNoTracking()
                .Where(tpu =>
                    assistantIds.Contains(tpu.AssisstantId) &&
                    tpu.TemplateId != templateId)
                .ToListAsync();
        }
    }
}
