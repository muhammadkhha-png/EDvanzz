using Edvanz.Application.Dtos;
using Edvanz.Application.Dtos.PermissionsDtos;
using Edvanz.Application.Dtos.TemplatePermissionsDtos;
using Edvanz.Application.IservicesContract;
using Edvanz.Domain.Entities;
using Edvanz.Domain.Enums;
using Edvanz.Domain.Interfaces;
using Edvanz.Domain.Resources;
using Microsoft.Extensions.Localization;
using System;
using System.Collections.Generic;
using System.Text;

namespace Edvanz.Application.Services
{
    public class ProfileService : IProfileService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IStringLocalizer<Messages> localizer;

        public ProfileService(IUnitOfWork unitOfWork, IStringLocalizer<Messages> _localizer)
        {
            _unitOfWork = unitOfWork;
            localizer = _localizer;
        }
        public async Task<Result<List<ProfilePermissionDto>>> GetAllProfilesWithPermissionsPerTeacherAsync(long teacherId)
        {
            var templates = await _unitOfWork.templateRepo
                .GetAllTemplatesPerTeacherAsyns(teacherId);

            if (templates is null || !templates.Any())
                return Result<List<ProfilePermissionDto>>.Success(
                    new List<ProfilePermissionDto>(), localizer);

            var result = templates.Select(t => new ProfilePermissionDto
            {
                profileId = t.Id,
                profileName = t.Name,
                Permissions = t.Profiles.Select(p => new PermissionDto
                {
                    permissionId = p.permision.Id,
                    permissionName = $"{p.permision.module.Name}.{p.permision.Name}",
                    isRestricted = p.permision.IsRestricted,
                }).ToList()
            }).ToList();

            return Result<List<ProfilePermissionDto>>.Success(result, localizer);
        }
        public async Task<Result<ProfilePermissionDto>> GetAllProfileWithPermissionsByIdAsync(long profileId)
        {
            var template = await _unitOfWork.templateRepo.GetByIdAsync(profileId);

            if (template is null)
                return Result<ProfilePermissionDto>.Failure(localizer, "ProfileNotFound");

            var result = new ProfilePermissionDto
            {
                profileId = template.Id,
                profileName = template.Name,
                Permissions = template.Profiles.Select(p => new PermissionDto
                {
                    permissionId = p.permision.Id,
                    permissionName = $"{p.permision.module.Name}.{p.permision.Name}",
                    isRestricted = p.permision.IsRestricted,
                }).ToList()
            };

            return Result<ProfilePermissionDto>.Success(result, localizer);
        }
        public async Task<Result<string>> CreateProfileAsync(CreatePermissionsProfileDto dto)
        {
            // -- 1. At least one permission ------------------------------------------
            if (dto.PermissionsIds is not { Count: > 0 })
                return Result<string>.Failure(localizer, "ProfileMustHaveAtLeastOnePermission");

            // -- 2. No duplicate permissions in the request itself -------------------
            var distinctPermIds = dto.PermissionsIds.Distinct().ToList();

            // -- 3. Validate all permission IDs exist --------------------------------
            var validPermCount = await _unitOfWork.GetRepository<Permission, long>()
                .CountAsync(p => distinctPermIds.Contains(p.Id));

            if (validPermCount != distinctPermIds.Count)
                return Result<string>.Failure(localizer, "OneOrMorePermissionsInvalid");

            // -- 4. Template name must be unique per teacher -------------------------
            var nameExists = await _unitOfWork.templateRepo.ValidateTemplateNamePerTeacher(dto.teacherId, dto.profileName, excludeTemplateId: null);
                

            if (nameExists)
                return Result<string>.Failure(localizer, "TemplateNameAlreadyExists");

            await _unitOfWork.BeginTransactionAsync();

            try
            {
                
                var template = new Template
                {
                    Name = dto.profileName.Trim(),
                    TeacherId = dto.teacherId,
                    CreateAt = DateTime.UtcNow,
                };

                await _unitOfWork.templateRepo.AddAsync(template);
                await _unitOfWork.SaveChangesAsync();   

                // -- 6. Bulk-insert TemplatePermisions rows ---------------------------
                var templatePermissions = distinctPermIds
                    .Select(pid => new TemplatePermisions
                    {
                        TemplateId = template.Id,
                        PermisionId = pid,
                    })
                    .ToList();

                await _unitOfWork.GetRepository<TemplatePermisions, long>()
                    .AddRangeAsync(templatePermissions);

                await _unitOfWork.SaveChangesAsync();
                await _unitOfWork.CommitAsync();

                return Result<string>.Success("ProfileCreated", localizer);
            }
            catch
            {
                await _unitOfWork.RollbackAsync();
                throw;
            }
        }
        public async Task<Result<string>> UpdateProfileAsync(UpdatePermissionsProfileDto dto)
        {
            // -- 1. Template exists --------------------------------------------------
            var template = await _unitOfWork.templateRepo.GetByIdAsync(dto.templateId);
            if (template is null)
                return Result<string>.Failure(localizer, "ProfileNotFound");

            

            // -- 3. Name uniqueness (excluding current template) ---------------------
            if (!string.IsNullOrWhiteSpace(dto.profileName) &&
                dto.profileName.Trim() != template.Name)
            {
                var nameExists = await _unitOfWork.templateRepo
                    .ValidateTemplateNamePerTeacher(
                        template.TeacherId,
                        dto.profileName.Trim(), excludeTemplateId: dto.templateId
                       );

                if (nameExists)
                    return Result<string>.Failure(localizer, "TemplateNameAlreadyExists");
            }

            // -- 4. Validate new permissions if provided -----------------------------
            List<long>? distinctPermIds = null;
            if (dto.PermissionsIds is { Count: > 0 })
            {
                distinctPermIds = dto.PermissionsIds.Distinct().ToList();

                var validPermCount = await _unitOfWork.GetRepository<Permission, long>()
                    .CountAsync(p => distinctPermIds.Contains(p.Id));

                if (validPermCount != distinctPermIds.Count)
                    return Result<string>.Failure(localizer, "OneOrMorePermissionsInvalid");
            }

            await _unitOfWork.BeginTransactionAsync();

            try
            {
                // -- 5. Update name if provided --------------------------------------
                if (!string.IsNullOrWhiteSpace(dto.profileName))
                    template.Name = dto.profileName.Trim();

                await _unitOfWork.templateRepo.UpdateAsync(template);

                // -- 6. Replace permissions + sync affected users --------------------
                if (distinctPermIds is { Count: > 0 })
                {
                    // Current permissions in the template
                    var existing = await _unitOfWork.templatePermissionsRepo.GetTemplatePermissions( dto.templateId);
                        

                    var oldPermissions = existing.Select(tp => tp.PermisionId).ToHashSet();
                    var newPermissionsSet = distinctPermIds.ToHashSet();

                    var addedPermissions = newPermissionsSet.Except(oldPermissions).ToList();
                    var removedPermissions = oldPermissions.Except(newPermissionsSet).ToList();

                    // -- 6a. Replace TemplatePermisions rows -------------------------
                    if (existing.Any())
                        await _unitOfWork.GetRepository<TemplatePermisions, long>()
                            .DeleteRangeAsync(existing);

                    var newTemplatePerms = distinctPermIds
                        .Select(pid => new TemplatePermisions
                        {
                            TemplateId = dto.templateId,
                            PermisionId = pid,
                        })
                        .ToList();

                    await _unitOfWork.GetRepository<TemplatePermisions, long>()
                        .AddRangeAsync(newTemplatePerms);

                    // -- 6b. Get all assistants who have this template ---------------
                    var affectedLinks = await _unitOfWork.templateAssistantsRepo.GetAllAssistantsPerTemplateAsyns(dto.templateId);

                    var affectedUserIds = affectedLinks
                        .Select(tpu => tpu.assissntat.UserId)
                        .Distinct()
                        .ToList();

                    if (affectedUserIds.Any())
                    {
                        // -- 6c. Add new permissions to all affected users -----------
                        if (addedPermissions.Any())
                        {
                            // Fetch what they already have to avoid duplicates
                            var existingUserPerms = await _unitOfWork.UsersPermissions.GetExistingUserPermissionsAsync(affectedUserIds, addedPermissions);
                               

                            var alreadyHas = existingUserPerms
                                .Select(up => (up.UserId, up.PermissionId))
                                .ToHashSet();

                            var toInsert = affectedUserIds
                                .SelectMany(uid => addedPermissions
                                    .Where(pid => !alreadyHas.Contains((uid, pid)))
                                    .Select(pid => new UsersPermission
                                    {
                                        UserId = uid,
                                        PermissionId = pid,
                                    }))
                                .ToList();

                            if (toInsert.Any())
                                await _unitOfWork.UsersPermissions
                                    .AddRangeAsync(toInsert);
                        }

                        // -- 6d. Remove permissions safely ---------------------------
                        // Only remove if not coming from another template or direct grant
                        if (removedPermissions.Any())
                        {
                            // Find all OTHER templates these users have
                            var otherTemplateLinks = await _unitOfWork.templateAssistantsRepo.GetOtherTemplateLinksForAssistantAsync(affectedUserIds,dto.templateId);

                            // Find permissions coming from those other templates
                            var otherTemplateIds = otherTemplateLinks
                                .Select(t => t.TemplateId)
                                .Distinct()
                                .ToList();

                            var protectedPermissions = new HashSet<long>();

                            if (otherTemplateIds.Any())
                            {
                                var otherTemplatePerms = await _unitOfWork.GetRepository<TemplatePermisions, long>()
                                    .GetAsync(tp => otherTemplateIds.Contains(tp.TemplateId));

                                protectedPermissions = otherTemplatePerms
                                    .Select(tp => tp.PermisionId)
                                    .ToHashSet();
                            }

                            // Safe to remove = removed AND not protected by another template
                            var safeToRemove = removedPermissions
                                .Where(pid => !protectedPermissions.Contains(pid))
                                .ToList();

                            if (safeToRemove.Any())
                            {
                                var toDelete = await _unitOfWork.UsersPermissions.GetExistingUserPermissionsAsync(affectedUserIds,safeToRemove);

                                if (toDelete.Any())
                                    await _unitOfWork.UsersPermissions
                                        .DeleteRangeAsync(toDelete);
                            }
                        }
                    }
                }

                await _unitOfWork.SaveChangesAsync();
                await _unitOfWork.CommitAsync();

                return Result<string>.Success("ProfileUpdated", localizer);
            }
            catch
            {
                await _unitOfWork.RollbackAsync();
                throw;
            }
        }
        public async Task<Result<string>> DeleteProfileAsync(long templateId)
        {
            // -- 1. Template exists --------------------------------------------------
            var template = await _unitOfWork.templateRepo.GetByIdAsync(templateId);
            if (template is null)
                return Result<string>.Failure(localizer, "ProfileNotFound");

            

            await _unitOfWork.BeginTransactionAsync();

            try
            {
                // -- 3. Get all permissions in this template -------------------------
                var templatePermissions = await _unitOfWork.templatePermissionsRepo
                    .GetTemplatePermissions(templateId);

                var permissionIdsInTemplate = templatePermissions
                    .Select(tp => tp.PermisionId)
                    .ToList();

                // -- 4. Get all assistants who have this template --------------------
                var affectedLinks = await _unitOfWork.templateAssistantsRepo
                    .GetAllAssistantsPerTemplateAsyns(templateId);

                var affectedUserIds = affectedLinks
                    .Select(tpu => tpu.assissntat.UserId)
                    .Distinct()
                    .ToList();

                // -- 5. Remove permissions from affected users (safely) --------------
                if (affectedUserIds.Any() && permissionIdsInTemplate.Any())
                {
                    // Find other templates these users have (excluding current)
                    var otherTemplateLinks = await _unitOfWork.templateAssistantsRepo
                        .GetOtherTemplateLinksForAssistantAsync(affectedUserIds, templateId);

                    var otherTemplateIds = otherTemplateLinks
                        .Select(t => t.TemplateId)
                        .Distinct()
                        .ToList();

                    var protectedPermissions = new HashSet<long>();

                    if (otherTemplateIds.Any())
                    {
                        var otherTemplatePerms = await _unitOfWork.templatePermissionsRepo
                            .GetTemplatePermissionsByIds(otherTemplateIds);

                        protectedPermissions = otherTemplatePerms
                            .Select(tp => tp.PermisionId)
                            .ToHashSet();
                    }

                    // Safe to remove = in this template AND not protected by another template
                    var safeToRemove = permissionIdsInTemplate
                        .Where(pid => !protectedPermissions.Contains(pid))
                        .ToList();

                    if (safeToRemove.Any())
                    {
                        var toDelete = await _unitOfWork.UsersPermissions
                            .GetExistingUserPermissionsAsync(affectedUserIds, safeToRemove);

                        if (toDelete.Any())
                            await _unitOfWork.UsersPermissions.DeleteRangeAsync(toDelete);
                    }
                }

                // -- 6. Delete TemplatePermissionsUsers rows -------------------------
                if (affectedLinks.Any())
                    await _unitOfWork.GetRepository<TemplatePermissionsUsers, long>()
                        .DeleteRangeAsync(affectedLinks);

                // -- 7. Delete TemplatePermisions rows -------------------------------
                if (templatePermissions.Any())
                    await _unitOfWork.GetRepository<TemplatePermisions, long>()
                        .DeleteRangeAsync(templatePermissions);

                // -- 8. Delete the Template itself -----------------------------------
                await _unitOfWork.templateRepo.DeleteAsync(template);

                await _unitOfWork.SaveChangesAsync();
                await _unitOfWork.CommitAsync();

                return Result<string>.Success("ProfileDeleted", localizer);
            }
            catch
            {
                await _unitOfWork.RollbackAsync();
                throw;
            }
        }
    }
}
