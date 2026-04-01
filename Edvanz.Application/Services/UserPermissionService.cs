using Edvanz.Application.Dtos;
using Edvanz.Application.Dtos.ModulesPermissions;
using Edvanz.Application.Dtos.PermissionsDtos;
using Edvanz.Application.Dtos.UsersPermissionsDtos;
using Edvanz.Application.IservicesContract;
using Edvanz.Domain.Interfaces;
using Edvanz.Domain.Resources;
using Microsoft.Extensions.Localization;
using System;
using System.Collections.Generic;
using System.Text;

namespace Edvanz.Application.Services
{
    public class UserPermissionService : IUserPermissionService
    {
        private readonly IUnitOfWork unitOfWork;
        private readonly IStringLocalizer<Messages> localizer;

        public UserPermissionService(IUnitOfWork unitOfWork, IStringLocalizer<Messages> localizer)
        {
            this.unitOfWork = unitOfWork;
            this.localizer = localizer;
        }
        public async Task<List<string>> GetUserPermissionsToToken(long userId)
        {
            var userPermissions = await unitOfWork.UsersPermissions.GetUserPermissionsAsync(userId);

            var permissionsForToken = userPermissions
                .Select(up => $"{up.Permission.module.Name}.{up.Permission.Name}")
                .ToList();

            return permissionsForToken;
        }

        public async Task<Result<UserPermissionDto>> GetUserPermissionsToView(long userId)
        {
            // 1️⃣ Get all user permissions
            var userPermissions = await unitOfWork.UsersPermissions.GetUserPermissionsAsync(userId);

            // 2️⃣ Check if the user has no permissions
            var groupedByModule = userPermissions != null && userPermissions.Any()
                ? userPermissions
                    .GroupBy(up => up.Permission.module)
                    .Select(g => new ModulePermissionsDto
                    {
                        id = g.Key.Id,
                        ModuleName = g.Key.Name,
                        permissions = g.Select(p => new PermissionDto
                        {
                            permissionId = p.Permission.Id,
                            permissionName = p.Permission.Name,
                            isRestricted = p.Permission.IsRestricted
                        }).ToList()
                    }).ToList()
                : new List<ModulePermissionsDto>(); // return empty list if no permissions

            // 3️⃣ Create the DTO
            var dto = new UserPermissionDto
            {
                userId = (int)userId,
                UserPermissions = groupedByModule
            };

            return Result<UserPermissionDto>.Success(dto, localizer);
        }
    }
}