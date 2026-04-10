using Edvanz.Application.Dtos;
using Edvanz.Application.Dtos.AssistantDtos;
using Edvanz.Application.Dtos.ModulesPermissions;
using Edvanz.Application.Dtos.PermissionsDtos;
using Edvanz.Application.Dtos.TemplatePermissionsDtos;
using Edvanz.Application.IservicesContract;
using Edvanz.Application.ServiceContract;
using Edvanz.Domain.Entities;
using Edvanz.Domain.Enums;
using Edvanz.Domain.Interfaces;
using Edvanz.Domain.Resources;
using Edvanz.Domain.ServiceContract;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Localization;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Net;
using System.Text;
using System.Linq;

namespace Edvanz.Application.Services
{
    public class AssistantService : IAssistantService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IStringLocalizer<Messages> localizer;
        private readonly IUserPermissionService userPermissionService;
        private readonly IPasswordService passwordService;
        private readonly ICurrentUserService currentUserService;
        private readonly IPaymentService _paymentService;

        public AssistantService(IUnitOfWork unitOfWork, IStringLocalizer<Messages> localizer,IUserPermissionService userPermissionService ,IPasswordService passwordService,ICurrentUserService _currentUserService,IPaymentService paymentService)
        {
            _unitOfWork = unitOfWork;
            this.localizer = localizer;
            this.userPermissionService = userPermissionService;
            this.passwordService = passwordService;
            currentUserService = _currentUserService;
            this._paymentService = paymentService;
        }

        public async Task<Result<PaginatedResponse<List<AssistantListDto>>>> GetAssistantListPerTeacher(
            AssistantPerTeacherFilterDto req)
        {
            try
            {
                var (assistants, totalCount) = await _unitOfWork.AssistantRepo
                    .GetListAssistantsPerTeacher(
                        teacherId: req.teacherId,
                        isAcitve: req.isAcitve,
                        fullName: req.fullName,
                        username: req.username,
                        isAssignedToTeacher: null, 
                        sortby: req.sortBy,
                        sortDirection: req.sortDirection,
                        page: req.Page,
                        pageSize: req.PageSize
                    );

                
                var dtoList = assistants.Select(a => new AssistantListDto
                {
                    id = a.Id,
                    fullName = a.User.FullName,
                    username = a.User.Username,
                    email = a.User.Email,
                    phoneNumber = a.User.PhoneNumber ?? string.Empty,
                    isActive = (bool)a.User.IsActive,
                    teacherId = a.TeacherAccountId,
                    teacherName = a.Teacher.User.FullName,
                    accountStatus = a.AccountStatus.ToString(),
                    createdAt=a.CreateAt,
                    deletedAt=a.DeletedAt,
                    languagePreference = a.LanguagePreference,
                    updatedAt=a.UpdatedAt,
                }).ToList();

                
                var response = new PaginatedResponse<List<AssistantListDto>>
                {
                    totalCount = totalCount, 
                    page = req.Page,
                    pageSize = req.PageSize,
                    totalPages = (int)Math.Ceiling((double)dtoList.Count / req.PageSize),
                    data = dtoList
                };

                return Result<PaginatedResponse<List<AssistantListDto>>>.Success(response, localizer);
            }
            catch (Exception ex)
            {
                
                return Result<PaginatedResponse<List<AssistantListDto>>>.Failure(
                    localizer,
                    "ServerError",
                    HttpStatusCode.InternalServerError);
            }
        }
        public async Task<Result<AssistantDto>> GetByAssistantIdAsync(long id)
        {
            var assistant = await _unitOfWork.AssistantRepo.GetAssistantWithPermissionsAsync(id);

            if (assistant == null)
                return Result<AssistantDto>.Failure(localizer, "UserNotFound");

            if (assistant.User == null)
                return Result<AssistantDto>.Failure(localizer, "UserDataNotFound");

            if (assistant.Teacher?.User == null)
                return Result<AssistantDto>.Failure(localizer, "TeacherDataNotFound");

            var groupedPermissions = assistant.User.Permissions
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
                }).ToList();
           List<ProfilePermissionDto> PermissionsProfile=new List<ProfilePermissionDto>();
             PermissionsProfile = assistant.PermissionProfiles.Select(p => new ProfilePermissionDto { profileId = p.TemplateId, profileName = p.template.Name }).ToList();
            var dto = new AssistantDto
            {
                id = assistant.Id,
                fullName = assistant.User.FullName,
                username = assistant.User.Username,
                email = assistant.User.Email,
                phoneNumber = assistant.User.PhoneNumber,
             
                teacherId = assistant.TeacherAccountId,
                teacherName = assistant.Teacher.User.FullName,
                createdAt = assistant.CreateAt,
                accountStatus = assistant.AccountStatus.ToString(),
                deletedAt = assistant.DeletedAt,
                updatedAt = assistant.UpdatedAt,
                languagePreference = assistant.LanguagePreference,
                userPermissions = groupedPermissions,
                assignedPermissionsProfiles = PermissionsProfile
            };

            return Result<AssistantDto>.Success(dto, localizer);
        }

        /// <summary>
        /// Creates a new assistant account under a teacher's space.
        ///
        /// Flow:
        ///   1.  Validate teacher exists and caller owns it
        ///   2.  Username / phone / email uniqueness check
        ///   3.  At least one permission source must be provided (permissionIds OR templates OR both)
        ///   4.  If individual permissionIds sent   -> validate they all exist in Permission table
        ///   5.  If permissionProfileIds sent       -> validate they all exist in Template table
        ///   6.  Create User row (UserType.Assistant) + first SaveChanges to get User.Id
        ///   7.  Create Assistant row               + second SaveChanges to get Assistant.Id
        ///   8.  Bulk-insert UsersPermission rows   (individual perms on the User entity)
        ///   9.  Bulk-insert TemplatePermissionsUsers rows (template links on the Assistant entity)
        ///   10. Final SaveChanges + Commit
        ///   11. Re-fetch with navigations and return string
        /// </summary>

       
        public async Task<Result<string?>> InitializeAssistantAsync(CreateAssistantDto dto)
        {
            var teacher = await _unitOfWork.Users.GetActiveTeacherByIdAsync(dto.teacherId);
            if (teacher is null)
                return Result<string?>.Failure(localizer, "TeacherNotFound");

            var modules = await _unitOfWork.ModuleTeacherRepo.GetModulesPerTeacher(teacher.Id);

            if (modules == null || !modules.Any())
                return Result<string?>.Failure(localizer, "TeacherHaven'tAnyOpenModules");

            var moduleIds = modules.Select(m => m.Id);

            var availablePermissionIds =
                await _unitOfWork.permissionRepo.GetPermissionIdsByModuleIdsAsync(moduleIds);

            var existingUser = await _unitOfWork.Users.FindExistingUserByCredentialsAsync(
                dto.phoneNumber ?? string.Empty,
                dto.username,
                dto.email ?? string.Empty);

            if (existingUser is not null)
            {
                if (!string.IsNullOrEmpty(dto.phoneNumber) && existingUser.PhoneNumber == dto.phoneNumber)
                    return Result<string?>.Failure(localizer, "repeatedPhoneNumber");

                if (existingUser.Username == dto.username)
                    return Result<string?>.Failure(localizer, "repeatedUserName");

                if (!string.IsNullOrEmpty(dto.email) && existingUser.Email == dto.email)
                    return Result<string?>.Failure(localizer, "repeatedEmail");
            }

            if ((dto.permissionIds is not { Count: > 0 }) &&
                (dto.permissionProfileIds is not { Count: > 0 }))
                return Result<string?>.Failure(localizer, "AssistantMustHaveAtLeastOnePermission");

            HashSet<long> allPermissionIds;

            try
            {
                allPermissionIds = await ResolveAssistantPermissionsAsync(
                    dto.permissionIds,
                    dto.permissionProfileIds);
            }
            catch (Exception ex) when (ex.Message == "InvalidPermissions" || ex.Message == "InvalidTemplates")
            {
                return Result<string?>.Failure(localizer, "InvalidPermissions");
            }

            await ValidateTeacherScopeAsync(teacher.Id, allPermissionIds);

            var collectPermission = await _unitOfWork.permissionRepo
                .GetByPermissionNameAndModuleNameAsync("Collect", "Payment");

            var newUser = new User
            {
                UserType = UserType.Assistant,
                FullName = dto.fullName,
                Username = dto.username,
                Email = dto.email,
                PhoneNumber = dto.phoneNumber,
                PasswordHashed = passwordService.HashPassword(dto.password),
                IsActive = true,
                IsVerified = false,
                CreateAt = DateTime.UtcNow,
                CreateByUserId = currentUserService.UserId,
            };

            await _unitOfWork.BeginTransactionAsync();

            try
            {
                await _unitOfWork.Users.AddAsync(newUser);
                await _unitOfWork.SaveChangesAsync();

                var assistant = new Assistant
                {
                    UserId = newUser.Id,
                    TeacherAccountId = dto.teacherId,
                    AccountStatus = AccountStatus.Active,
                    CreateAt = DateTime.UtcNow,
                };

                await _unitOfWork.AssistantRepo.AddAsync(assistant);
                await _unitOfWork.SaveChangesAsync();

                if (allPermissionIds.Any())
                {
                    var userPermissions = allPermissionIds
                        .Select(pid => new UsersPermission
                        {
                            UserId = newUser.Id,
                            PermissionId = pid,
                        }).ToList();

                    await _unitOfWork.GetRepository<UsersPermission, long>()
                        .AddRangeAsync(userPermissions);
                }

                if (dto.permissionProfileIds is { Count: > 0 })
                {
                    var templateLinks = dto.permissionProfileIds
                        .Distinct()
                        .Select(tid => new TemplatePermissionsUsers
                        {
                            TemplateId = tid,
                            AssisstantId = assistant.Id,
                        }).ToList();

                    await _unitOfWork.GetRepository<TemplatePermissionsUsers, long>()
                        .AddRangeAsync(templateLinks);
                }

                if (collectPermission != null &&
                    allPermissionIds.Contains(collectPermission.Id))
                {
                    await _paymentService.EnsureAssistantWalletExistsAsync(
                        dto.teacherId,
                        assistant.Id,
                        newUser.Id);
                }

                await _unitOfWork.SaveChangesAsync();
                await _unitOfWork.CommitAsync();

                return Result<string?>.Success("Success", localizer);
            }
            catch
            {
                await _unitOfWork.RollbackAsync();
                throw;
            }
        }
        public async Task<Result<string?>> UpdateAssistantAsync(UpdateAssistantRequest dto)
        {
            var assistant = await _unitOfWork.AssistantRepo.GetByIdAsync(dto.assistantId);
            if (assistant is null)
                return Result<string?>.Failure(localizer, "AssistantNotFound");

            var user = await _unitOfWork.Users.GetUserByIdAsync(assistant.UserId);
            if (user is null)
                return Result<string?>.Failure(localizer, "UserNotFound");

            if (!string.IsNullOrEmpty(dto.username) && dto.username != user.Username)
            {
                var existing = await _unitOfWork.Users
                    .FindExistingUserByCredentialsAsync(string.Empty, dto.username, null);

                if (existing is not null && existing.Id != user.Id)
                    return Result<string?>.Failure(localizer, "repeatedUserName");
            }

            if (!string.IsNullOrEmpty(dto.phoneNumber) && dto.phoneNumber != user.PhoneNumber)
            {
                var existing = await _unitOfWork.Users
                    .FindExistingUserByCredentialsAsync(dto.phoneNumber, string.Empty, null);

                if (existing is not null && existing.Id != user.Id)
                    return Result<string?>.Failure(localizer, "repeatedPhoneNumber");
            }

            if (!string.IsNullOrEmpty(dto.email) && dto.email != user.Email)
            {
                var existing = await _unitOfWork.Users
                    .FindExistingUserByCredentialsAsync(string.Empty, string.Empty, dto.email);

                if (existing is not null && existing.Id != user.Id)
                    return Result<string?>.Failure(localizer, "repeatedEmail");
            }

            HashSet<long>? newPermissionIds = null;
            bool permissionsUpdated =
                dto.permissionIds is { Count: > 0 } ||
                dto.permissionProfileIds is { Count: > 0 };

            if (permissionsUpdated)
            {
                try
                {
                    newPermissionIds = await ResolveAssistantPermissionsAsync(
                        dto.permissionIds,
                        dto.permissionProfileIds);

                }
                catch
                {
                    return Result<string?>.Failure(localizer, "InvalidPermissions");
                }

                await ValidateTeacherScopeAsync(assistant.TeacherAccountId, newPermissionIds);
            }

            user.FullName = dto.fullName ?? user.FullName;
            user.Username = dto.username ?? user.Username;
            user.PhoneNumber = dto.phoneNumber ?? user.PhoneNumber;
            user.Email = dto.email ?? user.Email;

            if (!string.IsNullOrEmpty(dto.newPassword))
                user.PasswordHashed = passwordService.HashPassword(dto.newPassword);

            await _unitOfWork.BeginTransactionAsync();

            try
            {
                await _unitOfWork.Users.UpdateAsync(user);

                if (permissionsUpdated && newPermissionIds is not null)
                {
                    var repo = _unitOfWork.GetRepository<UsersPermission, long>();

                    var existing = await repo.GetAsync(x => x.UserId == user.Id);

                    if (existing.Any())
                        await repo.DeleteRangeAsync(existing);

                    var newRows = newPermissionIds.Select(pid => new UsersPermission
                    {
                        UserId = user.Id,
                        PermissionId = pid
                    });

                    await repo.AddRangeAsync(newRows);
                }
                if (permissionsUpdated && newPermissionIds is not null)
                {
                    var collectPermission = await _unitOfWork.permissionRepo
                        .GetByPermissionNameAndModuleNameAsync("Collect", "Payment");

                    if (collectPermission != null &&
                        newPermissionIds.Contains(collectPermission.Id))
                    {
                        await _paymentService.EnsureAssistantWalletExistsAsync(
                            assistant.TeacherAccountId,
                            assistant.Id,
                            user.Id);
                    }
                }
                if (dto.permissionProfileIds != null)
                {
                    var templateRepo = _unitOfWork.GetRepository<TemplatePermissionsUsers, long>();

                    var existingTemplates = await templateRepo
                        .GetAsync(x => x.AssisstantId == assistant.Id);

                    if (existingTemplates.Any())
                        await templateRepo.DeleteRangeAsync(existingTemplates);

                    if (dto.permissionProfileIds.Any())
                    {
                        var newTemplates = dto.permissionProfileIds
                            .Distinct()
                            .Select(tid => new TemplatePermissionsUsers
                            {
                                TemplateId = tid,
                                AssisstantId = assistant.Id,
                            }).ToList();

                        await templateRepo.AddRangeAsync(newTemplates);
                    }
                }
               assistant.UpdatedAt = DateTime.UtcNow;
                await _unitOfWork.SaveChangesAsync();
                await _unitOfWork.CommitAsync();

                return Result<string?>.Success("Success", localizer);
            }
            catch
            {
                await _unitOfWork.RollbackAsync();
                throw;
            }
        }
        public async Task<Result<string>> ToggleStatus(ToggleAccountStatus req)
        {
            // -- 1. Fetch assistant ---------------------------------------------------
            var assistant = await _unitOfWork.AssistantRepo.GetByIdAsync(req.accountId);
            if (assistant is null)
                return Result<string>.Failure(localizer, "AssistantNotFound");


            // -- 3. Fetch linked user -------------------------------------------------
            var user = await _unitOfWork.Users.GetUserByIdAsync(assistant.UserId);
            if (user is null)
                return Result<string>.Failure(localizer, "UserNotFound");

            // -- 4. Already in the requested status? ----------------------------------
            if (assistant.AccountStatus == req.targetStatus)
                return Result<string>.Failure(localizer, "AssistantAlreadyInThisStatus");

            // -- 5. Apply status ------------------------------------------------------
            assistant.AccountStatus = req.targetStatus;
                
            // Active → user can login | Inactive / Suspended → user cannot login
            user.IsActive = req.targetStatus == AccountStatus.Active;
            if (req.targetStatus == AccountStatus.Inactive)
            {
                assistant.DeactivatedAt = DateTime.UtcNow;
            }
            else if (req.targetStatus == AccountStatus.Suspended)
            {
                assistant.DeletedAt = DateTime.UtcNow.AddMinutes(2);
            }
            else if (req.targetStatus == AccountStatus.Active)
            {
                assistant.DeactivatedAt = null;
                assistant.DeletedAt = null;
                assistant.UpdatedAt = DateTime.UtcNow;
            }
            // -- 6. Persist -----------------------------------------------------------
            await _unitOfWork.BeginTransactionAsync();

            try
            {
                await _unitOfWork.AssistantRepo.UpdateAsync(assistant);
                await _unitOfWork.Users.UpdateAsync(user);

                await _unitOfWork.SaveChangesAsync();
                await _unitOfWork.CommitAsync();

                var messageKey = req.targetStatus switch
                {
                    AccountStatus.Active => "AssistantActivated",
                    AccountStatus.Inactive => "AssistantDeactivated",
                    AccountStatus.Suspended => "AssistantSuspended",
                    _ => "StatusUpdated"
                };

                return Result<string>.Success(messageKey, localizer);
            }
            catch
            {
                await _unitOfWork.RollbackAsync();
                throw;
            }
        }
      

        public async Task<Result<List<LoginActivityDto>>> GetLoginActivityAsync(long assistantId)
        {
            // -- 1. Fetch assistant ---------------------------------------------------
            var assistant = await _unitOfWork.AssistantRepo.GetByIdAsync(assistantId);
            if (assistant is null)
                return Result<List<LoginActivityDto>>.Failure(localizer, "AssistantNotFound");

            //// -- 2. Ownership check ---------------------------------------------------
            //var isSuperAdmin = currentUserService.Role == nameof(UserType.SuperAdmin);

            //if (!isSuperAdmin && assistant.TeacherAccountId != currentUserService.TeacherId)
            //    return Result<List<LoginActivityDto>>.Failure(localizer, "UnauthorizedTeacher");

            // -- 3. Fetch logs --------------------------------------------------------
            var logs = await _unitOfWork.GetRepository<LoginActivityAssistantLog, long>()
                .GetAsync(l => l.AssistantId == assistantId);

            // -- 4. Map ---------------------------------------------------------------
            var result = logs
                .OrderByDescending(l => l.CreateAt)
                .Select(l => new LoginActivityDto
                {
                    id = l.Id,
                    assistantName = assistant.User.FullName,
                    action = l.ActionType.ToString(),
                    occurredAt = l.CreateAt,
                    deviceOrBrowser = l.DeviceOrBrowser,
                    ipAddress = l.IpAddress,
                })
                .ToList();

            return Result<List<LoginActivityDto>>.Success(result, localizer, "Success");
        }

        public async Task RecordLoginActivityAsync(
    long assistantId,
    LoginAcitvityActionType actionType,
    HttpContext httpContext)
        {
            var log = new LoginActivityAssistantLog
            {
                AssistantId = assistantId,
                ActionType = actionType,
                CreateAt = DateTime.UtcNow,
                DeviceOrBrowser = httpContext.Request.Headers["User-Agent"].ToString(),
                IpAddress = httpContext.Connection.RemoteIpAddress?.ToString(),
            };

            await _unitOfWork.GetRepository<LoginActivityAssistantLog, long>()
                .AddAsync(log);

            await _unitOfWork.SaveChangesAsync();
        }



        private async Task<HashSet<long>> ResolveAssistantPermissionsAsync(
    IEnumerable<long>? permissionIds,
    IEnumerable<long>? templateIds)
        {
            var result = new HashSet<long>();

            // 1. Individual permissions
            if (permissionIds != null && permissionIds.Any())
            {
                var distinct = permissionIds.Distinct().ToList();

                var validCount = await _unitOfWork
                    .GetRepository<Permission, long>()
                    .CountAsync(p => distinct.Contains(p.Id));

                if (validCount != distinct.Count)
                    throw new Exception("InvalidPermissions");

                result.UnionWith(distinct);
            }

            // 2. Templates → Permissions
            if (templateIds != null && templateIds.Any())
            {
                var distinctTemplates = templateIds.Distinct().ToList();

                var templatePermissions = await _unitOfWork
                    .GetRepository<TemplatePermisions, long>()
                    .GetAsync(tp => distinctTemplates.Contains(tp.TemplateId));

                var foundTemplates = templatePermissions
                    .Select(t => t.TemplateId)
                    .Distinct()
                    .ToList();

                if (foundTemplates.Count != distinctTemplates.Count)
                    throw new Exception("InvalidTemplates");

                result.UnionWith(templatePermissions.Select(t => t.PermisionId));
            }

            return result;
        }

        private async Task ValidateTeacherScopeAsync(long teacherId, HashSet<long> permissionIds)
        {
            var modules = await _unitOfWork.ModuleTeacherRepo.GetModulesPerTeacher(teacherId);

            var moduleIds = modules.Select(m => m.Id);

            var availablePermissionIds =
                await _unitOfWork.permissionRepo.GetPermissionIdsByModuleIdsAsync(moduleIds);

            var outOfScope = permissionIds
                .Where(p => !availablePermissionIds.Contains(p))
                .ToList();

            if (outOfScope.Any())
                throw new Exception("AssistantPermissionsOutOfTeacherScope");
        }
    }
}
