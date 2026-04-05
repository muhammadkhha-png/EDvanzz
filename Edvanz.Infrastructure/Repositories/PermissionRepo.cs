using Edvanz.Domain.Entities;
using Edvanz.Domain.Interfaces;
using Edvanz.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Edvanz.Infrastructure.Repositories
{
    public class PermissionRepo: GenericRepo<Permission, long>, IPermissionRepo
    {
        public PermissionRepo(EdvanzDbContext context) : base(context)
        {
        }
    
        public async Task<IReadOnlyList<Permission>> GetPermissionsByModuleIdsAsync(List<long> moduleIds)
        {
            return await _context.Set<Permission>()
                .AsNoTracking()
                .Include(p => p.module)
                .Where(p => moduleIds.Contains(p.ModuleId))
                .ToListAsync();
        }
    }
}
