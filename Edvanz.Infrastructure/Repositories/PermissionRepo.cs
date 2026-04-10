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


        public async Task<HashSet<long>> GetPermissionIdsByModuleIdsAsync(IEnumerable<long> moduleIds)
        {
            return await _context.Permissions
                .Where(p => moduleIds.Contains(p.ModuleId))
                .Select(p => p.Id)
                .ToHashSetAsync();
        }
        public async Task<Permission?> GetByPermissionNameAndModuleNameAsync(string name,string moduleName)
        {
            return await _context.Permissions
                .AsNoTracking()
                .Include(p => p.module)
                .FirstOrDefaultAsync(p => p.Name == name && p.module.Name == moduleName);
        }
    }
}
