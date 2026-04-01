using Edvanz.Application.Dtos;
using Edvanz.Application.Dtos.UsersPermissionsDtos;
using System;
using System.Collections.Generic;
using System.Text;

namespace Edvanz.Application.IservicesContract
{
    public interface IUserPermissionService
    {
        public Task<List<string>> GetUserPermissionsToToken(long userID);
        public Task<Result<UserPermissionDto>> GetUserPermissionsToView(long userId);
    }
}
