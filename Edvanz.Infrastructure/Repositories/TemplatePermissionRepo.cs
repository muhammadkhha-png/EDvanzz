using Edvanz.Domain.Entities;
using Edvanz.Domain.Interfaces;
using Edvanz.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Text;

namespace Edvanz.Infrastructure.Repositories
{
    public class TemplatePermissionRepo : GenericRepo<TemplatePermisions, (long, long)>, ITemplatePermissionRepo
    {
        public TemplatePermissionRepo(EdvanzDbContext context) : base(context)
        {
        }

        public async Task<IReadOnlyList<TemplatePermisions>> GetTemplatePermissions(long templateID)
        {
            return await _context.TemplatesPermisions
                .AsNoTracking().Where(tp => tp.TemplateId == templateID).ToListAsync();
              
        }
        public async Task<IReadOnlyList<TemplatePermisions>> GetTemplatePermissionsByIds(List<long> templateIds)
        {
            return await _context.TemplatesPermisions
                .AsNoTracking()
                .Where(tp => templateIds.Contains(tp.TemplateId))
                .ToListAsync();
        }

    }
}
