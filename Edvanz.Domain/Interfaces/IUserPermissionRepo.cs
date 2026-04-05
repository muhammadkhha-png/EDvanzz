using Edvanz.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Text;

namespace Edvanz.Domain.Interfaces
{
    public interface IUserPermissionRepo : IGenericRepo<UsersPermission,(long,long) >
    {
        //public  Task<List<string>> GetUserPermissionsAsync(long userId);
        public Task<IReadOnlyList<UsersPermission>> GetUserPermissionsAsync(long userId);
        public Task<IReadOnlyList<UsersPermission>> GetByPermissionId(long permissionId);
        Task<IReadOnlyList<UsersPermission>> GetExistingUserPermissionsAsync(
    List<long> userIds,
    List<long> permissionIds);
    }
}
