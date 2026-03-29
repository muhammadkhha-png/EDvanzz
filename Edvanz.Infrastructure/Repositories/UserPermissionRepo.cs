using Edvanz.Domain.Entities;
using Edvanz.Domain.Interfaces;
using Edvanz.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Text;

namespace Edvanz.Infrastructure.Repositories
{
    public class UserPermissionRepo : GenericRepo<UsersPermission, (long, long)>, IUserPermissionRepo
    {
        public UserPermissionRepo(EdvanzDbContext context) : base(context)
        {
        }

        public async Task<List<string>> GetUserPermissionsAsync(long userId)
        {
            return await _context.Set<UsersPermission>()
            .Where(up => up.UserId == userId)
            .Include(up => up.Permission)
                .ThenInclude(p => p.module)
            .Select(up => $"{up.Permission.module.Name}.{up.Permission.Name}")
            .ToListAsync();
        }
    }
}
