using Edvanz.Application.Dtos;
using Edvanz.Application.Dtos.AssistantDtos;
using Edvanz.Application.Dtos.ModulesPermissions;
using Edvanz.Application.Dtos.PermissionsDtos;
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

        public PermissionService(ITeacherService teacherService , IStringLocalizer<Messages> localizer,IUnitOfWork unitOfWork,IAssistantService _assisstantService,IPaymentService paymentService)
        {
            this.teacherService = teacherService;
            this.localizer = localizer;
            this.unitOfWork = unitOfWork;
            assisstantService = _assisstantService;
            this._paymentService = paymentService;
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

            // -- 4. Group by module -------------------------------------------------
            var result = teacherModules
                .Select(m => new ModulePermissionsDto
                {
                    id = m.Id,
                    ModuleName = m.Name,
                    permissions = permissions
                        .Where(p => p.ModuleId == m.Id)
                        .Select(p => new PermissionDto
                        {
                            permissionId = p.Id,
                            permissionName = p.Name,
                            isRestricted = p.IsRestricted,
                        })
                        .ToList()
                })
                .Where(m => m.permissions.Any()) 
                .ToList();

            return Result<List<ModulePermissionsDto>>.Success(result, localizer);
        }
        //public async Task<Result<string>> UpdateAssistantPermissionsAsync(UpdateAssistantPermissionsRequest dto)
        //{
        //    // -- 1. Fetch assistant ---------------------------------------------------
        //    var assistant = await unitOfWork.AssistantRepo.GetByIdAsync(dto.assistantId);
        //    if (assistant is null)
        //        return Result<string>.Failure(localizer, "AssistantNotFound");


        //    // -- 3. At least one source -----------------------------------------------
        //    var hasIndividualPerms = dto.permissionIds is { Count: > 0 };
        //    var hasTemplates = dto.permissionProfileIds is { Count: > 0 };

        //    if (!hasIndividualPerms && !hasTemplates)
        //        return Result<string>.Failure(localizer, "AssistantMustHaveAtLeastOnePermission");

        //    // -- 4. Resolve all permission IDs ----------------------------------------
        //    var allPermissionIds = new HashSet<long>();

        //    if (hasIndividualPerms)
        //    {
        //        var distinctPermIds = dto.permissionIds!.Distinct().ToList();

        //        var validCount = await unitOfWork.GetRepository<Permission, long>()
        //            .CountAsync(p => distinctPermIds.Contains(p.Id));

        //        if (validCount != distinctPermIds.Count)
        //            return Result<string>.Failure(localizer, "OneOrMorePermissionsInvalid");

        //        allPermissionIds.UnionWith(distinctPermIds);
        //    }

        //    if (hasTemplates)
        //    {
        //        var distinctTemplateIds = dto.permissionProfileIds!.Distinct().ToList();

        //        var templatePermissions = await unitOfWork.GetRepository<TemplatePermisions, long>()
        //            .GetAsync(tp => distinctTemplateIds.Contains(tp.templateId));

        //        var foundTemplateIds = templatePermissions
        //            .Select(tp => tp.templateId)
        //            .Distinct()
        //            .ToList();

        //        if (foundTemplateIds.Count != distinctTemplateIds.Count)
        //            return Result<string>.Failure(localizer, "OneOrMorePermissionsInvalid");

        //        allPermissionIds.UnionWith(
        //            templatePermissions.Select(tp => tp.PermisionId));
        //    }

        //    // -- 5. Replace UsersPermission rows --------------------------------------
        //    await unitOfWork.BeginTransactionAsync();

        //    try
        //    {
        //        var existing = await unitOfWork.GetRepository<UsersPermission, long>()
        //            .GetAsync(up => up.UserId == assistant.UserId);

        //        if (existing.Any())
        //            await unitOfWork.GetRepository<UsersPermission, long>()
        //                .DeleteRangeAsync(existing);

        //        var newPermissions = allPermissionIds
        //            .Select(pid => new UsersPermission
        //            {
        //                UserId = assistant.UserId,
        //                PermissionId = pid,
        //            })
        //            .ToList();

        //        await unitOfWork.GetRepository<UsersPermission, long>()
        //            .AddRangeAsync(newPermissions);

        //        await unitOfWork.SaveChangesAsync();
        //        await unitOfWork.CommitAsync();

        //        return Result<string>.Success("PermissionsUpdated", localizer);
        //    }
        //    catch
        //    {
        //        await unitOfWork.RollbackAsync();
        //        throw;
        //    }
        //}
        public async Task<Result<string>> UpdateAssistantPermissionsAsync(UpdateAssistantPermissionsRequest dto)
        {
            var assistant = await unitOfWork.AssistantRepo.GetByIdAsync(dto.assistantId);
            if (assistant is null)
                return Result<string>.Failure(localizer, "AssistantNotFound");

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
                assistant.UpdatedAt = DateTime.UtcNow;

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
