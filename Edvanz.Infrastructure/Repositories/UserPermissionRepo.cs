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

        public async Task<IReadOnlyList<UsersPermission>> GetByPermissionId(long permissionId)
        {
            return await _context.UsersPermissions
                .AsNoTracking().Where(up=>up.PermissionId==permissionId).ToListAsync();
        }

        public async Task<IReadOnlyList<UsersPermission>> GetUserPermissionsAsync(long userId)
        {
            return await _context.Set<UsersPermission>()
            .Where(up => up.UserId == userId)
            .Include(up => up.Permission)
                .ThenInclude(p => p.module).ToListAsync();
        }

        public async Task<IReadOnlyList<UsersPermission>> GetExistingUserPermissionsAsync(
    List<long> userIds,
    List<long> permissionIds)
        {
            return await _context.Set<UsersPermission>()
                .AsNoTracking()
                .Where(up =>
                    userIds.Contains(up.UserId) &&
                    permissionIds.Contains(up.PermissionId))
                .ToListAsync();
        }

        //public async Task<List<string>> GetUserPermissionsAsync(long userId)
        //{
        //    return await _context.Set<UsersPermission>()
        //    .Where(up => up.UserId == userId)
        //    .Include(up => up.Permission)
        //        .ThenInclude(p => p.module)
        //    .Select(up => $"{up.Permission.module.name}.{up.Permission.name}")
        //    .ToListAsync();
        //}
    }
}
