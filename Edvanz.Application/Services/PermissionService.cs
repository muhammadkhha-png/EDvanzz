using Edvanz.Application.Dtos;
using Edvanz.Application.Dtos.AssistantDtos;
using Edvanz.Application.Dtos.ModulesPermissions;
using Edvanz.Application.Dtos.PermissionsDtos;
using Edvanz.Application.Extensions;
using Edvanz.Application.IservicesContract;
using Edvanz.Application.ServiceContract;
using Edvanz.Domain.Entities;
using Edvanz.Domain.Interfaces;
using Edvanz.Domain.Resources;
using Microsoft.Extensions.Localization;
using System;
using System.Collections.Generic;
using System.Text;

namespace Edvanz.Application.Services
{
    public class PermissionService : IPermissionService
    {
        private readonly ITeacherService teacherService;
        private readonly IStringLocalizer<Messages> localizer;
        private readonly IUnitOfWork unitOfWork;
        private readonly IAssistantService assisstantService;
        private readonly IPaymentService _paymentService;

        private readonly IUserAuthInvalidationService _authInvalidation;
        private readonly ICurrentUserService _currentUser;

        public PermissionService(ITeacherService teacherService, IStringLocalizer<Messages> localizer, IUnitOfWork unitOfWork, IAssistantService _assisstantService, IPaymentService paymentService, IUserAuthInvalidationService authInvalidation, ICurrentUserService currentUser)
        {
            this.teacherService = teacherService;
            this.localizer = localizer;
            this.unitOfWork = unitOfWork;
            assisstantService = _assisstantService;
            this._paymentService = paymentService;
            _authInvalidation = authInvalidation;
            _currentUser = currentUser;
        }

        /// <summary>Tenant guard: true iff the given owning-teacher id matches the caller's teacher
        /// scope (SuperAdmin always passes). Blocks cross-tenant assistant-permission changes.</summary>
        private async Task<bool> CallerOwnsTeacherAsync(long ownerTeacherId)
        {
            if (string.Equals(_currentUser.Role, "SuperAdmin", StringComparison.Ordinal))
                return true;
            var userId = _currentUser.UserId;
            if (userId is null) return false;
            long? callerTeacherId = (await unitOfWork.Users.GetTeacherByUserIdAsync(userId.Value))?.Id;
            if (callerTeacherId is null)
            {
                var asst = await unitOfWork.AssistantRepo.GetAssistantWithUserIdAsync(userId.Value);
                callerTeacherId = asst?.TeacherAccountId;
            }
            return callerTeacherId is not null && ownerTeacherId == callerTeacherId.Value;
        }
        public async Task<Result<List<ModulePermissionsDto>>> GetAvailableTeacherPermissionCatalogue(long teacherId)
        {
            // -- 1. Teacher exists ---------------------------------------------------
            var teacher = await unitOfWork.Users.GetActiveTeacherByIdAsync(teacherId);
            if (teacher == null)
                return Result<List<ModulePermissionsDto>>.Failure(localizer, "UserNotFound");

            // -- 2. Get modules assigned to this teacher -----------------------------
            var teacherModules = await unitOfWork.ModuleTeacherRepo
                .GetModulesPerTeacher(teacherId);

            if (teacherModules is null || !teacherModules.Any())
                return Result<List<ModulePermissionsDto>>.Success(
                    new List<ModulePermissionsDto>(), localizer);

            var moduleIds = teacherModules.Select(m => m.Id).ToList();

            // -- 3. Get all permissions belonging to those modules ------------------
            var permissions = await unitOfWork.GetRepository<Permission, long>()
                .GetAsync(p => moduleIds.Contains(p.ModuleId));

            // -- 4. Group by module (display fields localized per Accept-Language) --
            var result = teacherModules
                .Select(m => new ModulePermissionsDto
                {
                    id = m.Id,
                    ModuleName = localizer.GetLocalizedModuleName(m.Name),
                    permissions = permissions
                        .Where(p => p.ModuleId == m.Id)
                        .Select(p => new PermissionDto
                        {
                            permissionId = p.Id,
                            permissionName = localizer.GetLocalizedPermissionName(m.Name, p.Name),
                            isRestricted = p.IsRestricted,
                            moduleName = localizer.GetLocalizedModuleName(m.Name),
                            description = localizer.GetLocalizedPermissionDescription(m.Name, p.Name, p.Description),
                        })
                        .ToList()
                })
                .Where(m => m.permissions.Any()) 
                .ToList();

            return Result<List<ModulePermissionsDto>>.Success(result, localizer);
        }
    
        public async Task<Result<string>> UpdateAssistantPermissionsAsync(UpdateAssistantPermissionsRequest dto)
        {
            var assistant = await unitOfWork.AssistantRepo.GetByIdAsync(dto.assistantId);
            if (assistant is null)
                return Result<string>.Failure(localizer, "AssistantNotFound");

            // Tenant guard: only the owning teacher / SuperAdmin may change this assistant's
            // permissions (covers PUT update-permissions and POST apply-profile, which both route here).
            if (!await CallerOwnsTeacherAsync(assistant.TeacherAccountId))
                return Result<string>.Failure(localizer, "AssistantNotFound", System.Net.HttpStatusCode.NotFound);

            var user = await unitOfWork.Users.GetUserByIdAsync(assistant.UserId);
            if (user is null)
                return Result<string>.Failure(localizer, "UserNotFound");

            if ((dto.permissionIds is not { Count: > 0 }) &&
                (dto.permissionProfileIds is not { Count: > 0 }))
            {
                return Result<string>.Failure(localizer, "AssistantMustHaveAtLeastOnePermission");
            }

            HashSet<long> newPermissionIds;

            try
            {
                newPermissionIds = await assisstantService.ResolveAssistantPermissionsAsync(
                    dto.permissionIds,
                    dto.permissionProfileIds);
            }
            catch
            {
                return Result<string>.Failure(localizer, "InvalidPermissions");
            }

            // validate scope
            await assisstantService.ValidateTeacherScopeAsync(assistant.TeacherAccountId, newPermissionIds);

            await unitOfWork.BeginTransactionAsync();

            try
            {
                // =======================
                // 1. Update Permissions
                // =======================
                var permRepo = unitOfWork.GetRepository<UsersPermission, long>();

                var existingPerms = await permRepo.GetAsync(x => x.UserId == user.Id);

                if (existingPerms.Any())
                    await permRepo.DeleteRangeAsync(existingPerms);

                var newPerms = newPermissionIds.Select(pid => new UsersPermission
                {
                    UserId = user.Id,
                    PermissionId = pid
                });

                await permRepo.AddRangeAsync(newPerms);

                // =======================
                // 2. Wallet Logic 🔥
                // =======================
                var collectPermission = await unitOfWork.permissionRepo
                    .GetByPermissionNameAndModuleNameAsync("Collect", "Payment");

                if (collectPermission != null &&
                    newPermissionIds.Contains(collectPermission.Id))
                {
                    await _paymentService.EnsureAssistantWalletExistsAsync(
                        assistant.TeacherAccountId,
                        assistant.Id,
                        user.Id);
                }

                // =======================
                // 3. Templates Update
                // =======================
                var templateRepo = unitOfWork.GetRepository<TemplatePermissionsUsers, long>();

                var existingTemplates = await templateRepo
                    .GetAsync(x => x.AssisstantId == assistant.Id);

                if (existingTemplates.Any())
                    await templateRepo.DeleteRangeAsync(existingTemplates);

                if (dto.permissionProfileIds is { Count: > 0 })
                {
                    var newTemplates = dto.permissionProfileIds
                        .Distinct()
                        .Select(tid => new TemplatePermissionsUsers
                        {
                            TemplateId = tid,
                            AssisstantId = assistant.Id,
                        });

                    await templateRepo.AddRangeAsync(newTemplates);
                }

                // =======================
                // 4. Update timestamp
                // =======================
                // =======================
                // 4. Update timestamp
                // =======================
                assistant.UpdatedAt = DateTime.UtcNow;

                // REQ-USR-013: The whole point of this method is to change the
                // assistant's permissions. Bump their SecurityStamp and drop
                // the Redis snapshot so the change takes effect immediately.
                await _authInvalidation.InvalidateUserAsync(user.Id);

                await unitOfWork.SaveChangesAsync();
                await unitOfWork.CommitAsync();

                return Result<string>.Success("Success", localizer);
            }
            catch
            {
                await unitOfWork.RollbackAsync();
                throw;
            }
        }

    }
}
