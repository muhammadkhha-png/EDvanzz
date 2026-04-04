using Edvanz.Application.Dtos;
using Edvanz.Application.Dtos.AssistantDtos;
using Edvanz.Application.Dtos.ModulesPermissions;
using Edvanz.Application.Dtos.PermissionsDtos;
using Edvanz.Application.Dtos.TemplatePermissionsDtos;
using Edvanz.Application.IservicesContract;
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

namespace Edvanz.Application.Services
{
    public class AssistantService : IAssistantService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IStringLocalizer<Messages> localizer;
        private readonly IUserPermissionService userPermissionService;
        private readonly IPasswordService passwordService;
        private readonly ICurrentUserService currentUserService;

        public AssistantService(IUnitOfWork unitOfWork, IStringLocalizer<Messages> localizer,IUserPermissionService userPermissionService ,IPasswordService passwordService,ICurrentUserService _currentUserService)
        {
            _unitOfWork = unitOfWork;
            this.localizer = localizer;
            this.userPermissionService = userPermissionService;
            this.passwordService = passwordService;
            currentUserService = _currentUserService;
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
                    totalCount = dtoList.Count, 
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
            var PermissionsProfile = assistant.PermissionProfiles.Select(p => new ProfilePermissionDto { profileId = p.TemplateId, profileName = p.template.Name }).ToList();
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
            // -- 1. Teacher ownership check -------------------------------------------
            var teacher = await _unitOfWork.Users.GetActiveTeacherByIdAsync(dto.teacherId);
            if (teacher is null)
                return Result<string?>.Failure(localizer, "TeacherNotFound");

            // -- 2. Uniqueness check --------------------------------------------------
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

            // -- 3. At least one permission source is mandatory -----------------------
            var hasIndividualPerms = dto.permissionIds is { Count: > 0 };
            var hasTemplates = dto.permissionProfileIds is { Count: > 0 };

            if (!hasIndividualPerms && !hasTemplates)
                return Result<string?>.Failure(localizer, "AssistantMustHaveAtLeastOnePermission");

            // -- 4. Validate individual permission IDs --------------------------------
            List<long>? distinctPermIds = null;
            if (hasIndividualPerms)
            {
                distinctPermIds = dto.permissionIds!.Distinct().ToList();

                var validPermCount = await _unitOfWork.GetRepository<Permission, long>()
                    .CountAsync(p => distinctPermIds.Contains(p.Id));

                if (validPermCount != distinctPermIds.Count)
                    return Result<string?>.Failure(localizer, "OneOrMorePermissionsInvalid");
            }

            // -- 5. Validate template IDs + fetch their permissions -------------------
            // Templates are just a grouping shortcut.
            // We resolve them to their Permission rows here and insert directly
            // into UsersPermission — no TemplatePermissionsUsers link is stored.
            List<long>? permissionsFromTemplates = null;
            if (hasTemplates)
            {
                var distinctTemplateIds = dto.permissionProfileIds!.Distinct().ToList();

                var templatePermissions = await _unitOfWork.GetRepository<TemplatePermisions, long>()
                    .GetAsync(tp => distinctTemplateIds.Contains(tp.TemplateId));

                // If any template ID returned no rows → it doesn't exist
                var foundTemplateIds = templatePermissions
                    .Select(tp => tp.TemplateId)
                    .Distinct()
                    .ToList();

                if (foundTemplateIds.Count != distinctTemplateIds.Count)
                    return Result<string?>.Failure(localizer, "OneOrMoreTemplatesInvalid");

                permissionsFromTemplates = templatePermissions
                    .Select(tp => tp.PermisionId)
                    .Distinct()
                    .ToList();
            }

            // -- 6. Merge both permission sources into one set ------------------------
            // HashSet deduplicates automatically — a permission present in both
            // individual list and a template is only inserted once.
            var allPermissionIds = new HashSet<long>();

            if (distinctPermIds is { Count: > 0 })
                allPermissionIds.UnionWith(distinctPermIds);

            if (permissionsFromTemplates is { Count: > 0 })
                allPermissionIds.UnionWith(permissionsFromTemplates);

            // -- 7. Build User entity -------------------------------------------------
            var hashedPassword = passwordService.HashPassword(dto.password);

            var newUser = new User
            {
                UserType = UserType.Assistant,
                FullName = dto.fullName,
                Username = dto.username,
                Email = dto.email,
                PhoneNumber = dto.phoneNumber,
                PasswordHashed = hashedPassword,
                IsActive = true,
                IsVerified = false,
                CreateAt = DateTime.UtcNow,
                CreateByUserId = currentUserService.UserId,
            };

            await _unitOfWork.BeginTransactionAsync();

            try
            {
                // -- 8. Persist User -> get newUser.Id --------------------------------
                await _unitOfWork.Users.AddAsync(newUser);
                await _unitOfWork.SaveChangesAsync();

                // -- 9. Persist Assistant -> get assistant.Id -------------------------
                var assistant = new Assistant
                {
                    UserId = newUser.Id,
                    TeacherAccountId = dto.teacherId,
                    AccountStatus = AccountStatus.Active,
                    CreateAt = DateTime.UtcNow,
                };

                await _unitOfWork.AssistantRepo.AddAsync(assistant);
                await _unitOfWork.SaveChangesAsync();

                // -- 10. Insert merged permissions -> UsersPermission rows ------------
                if (allPermissionIds.Count > 0)
                {
                    var userPermissions = allPermissionIds
                        .Select(pid => new UsersPermission
                        {
                            UserId = newUser.Id,
                            PermissionId = pid,
                        })
                        .ToList();

                    await _unitOfWork.GetRepository<UsersPermission, long>()
                        .AddRangeAsync(userPermissions);
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
        public async Task<Result<string?>> UpdateAssistantAsync( UpdateAssistantRequest dto)
        {
            // -- 1. Fetch assistant + user --------------------------------------------
            var assistant = await _unitOfWork.AssistantRepo.GetByIdAsync(dto.assistantId);
            if (assistant is null)
                return Result<string?>.Failure(localizer, "AssistantNotFound");


            var user = await _unitOfWork.Users.GetUserByIdAsync(assistant.UserId);
            if (user is null)
                return Result<string?>.Failure(localizer, "UserNotFound");

            // -- 3. Uniqueness checks (only if value is changing) ---------------------
            if (!string.IsNullOrEmpty(dto.username) && dto.username != user.Username)
            {
                var existingByUsername = await _unitOfWork.Users
                    .FindExistingUserByCredentialsAsync(string.Empty, dto.username, null);

                if (existingByUsername is not null && existingByUsername.Id != user.Id)
                    return Result<string?>.Failure(localizer, "repeatedUserName");
            }

            if (!string.IsNullOrEmpty(dto.phoneNumber) && dto.phoneNumber != user.PhoneNumber)
            {
                var existingByPhone = await _unitOfWork.Users
                    .FindExistingUserByCredentialsAsync(dto.phoneNumber, string.Empty, null);

                if (existingByPhone is not null && existingByPhone.Id != user.Id)
                    return Result<string?>.Failure(localizer, "repeatedPhoneNumber");
            }

            if (!string.IsNullOrEmpty(dto.email) && dto.email != user.Email)
            {
                var existingByEmail = await _unitOfWork.Users
                    .FindExistingUserByCredentialsAsync(string.Empty, string.Empty, dto.email);

                if (existingByEmail is not null && existingByEmail.Id != user.Id)
                    return Result<string?>.Failure(localizer, "repeatedEmail");
            }

            // -- 4. Resolve new permissions if provided -------------------------------
            // Only recalculate permissions if the caller sent at least one of the two lists.
            // If both are null → keep existing permissions untouched.
            var hasIndividualPerms = dto.PermissionsId is { Count: > 0 };
            var hasTemplates = dto.PermissionProfileIds is { Count: > 0 };
            var permissionsUpdated = hasIndividualPerms || hasTemplates;

            HashSet<long>? newPermissionIds = null;

            if (permissionsUpdated)
            {
                newPermissionIds = new HashSet<long>();

                // -- 4a. Validate + add individual permission IDs ---------------------
                if (hasIndividualPerms)
                {
                    var distinctPermIds = dto.PermissionsId!.Distinct().ToList();

                    var validPermCount = await _unitOfWork.GetRepository<Permission, long>()
                        .CountAsync(p => distinctPermIds.Contains(p.Id));

                    if (validPermCount != distinctPermIds.Count)
                        return Result<string?>.Failure(localizer, "OneOrMorePermissionsInvalid");

                    newPermissionIds.UnionWith(distinctPermIds);
                }

                // -- 4b. Validate templates + resolve their permissions ---------------
                if (hasTemplates)
                {
                    var distinctTemplateIds = dto.PermissionProfileIds!.Distinct().ToList();

                    var templatePermissions = await _unitOfWork.GetRepository<TemplatePermisions, long>()
                        .GetAsync(tp => distinctTemplateIds.Contains(tp.TemplateId));

                    var foundTemplateIds = templatePermissions
                        .Select(tp => tp.TemplateId)
                        .Distinct()
                        .ToList();

                    if (foundTemplateIds.Count != distinctTemplateIds.Count)
                        return Result<string?>.Failure(localizer, "OneOrMorePermissionsInvalid");

                    var permsFromTemplates = templatePermissions
                        .Select(tp => tp.PermisionId)
                        .Distinct();

                    newPermissionIds.UnionWith(permsFromTemplates);
                }
            }

            // -- 5. Apply user field updates ------------------------------------------
            if (!string.IsNullOrEmpty(dto.fullName))
                user.FullName = dto.fullName;

            if (!string.IsNullOrEmpty(dto.username))
                user.Username = dto.username;

            if (!string.IsNullOrEmpty(dto.phoneNumber))
                user.PhoneNumber = dto.phoneNumber;

            if (!string.IsNullOrEmpty(dto.email))
                user.Email = dto.email;

            if (!string.IsNullOrEmpty(dto.newPassword))
                user.PasswordHashed = passwordService.HashPassword(dto.newPassword);

            await _unitOfWork.BeginTransactionAsync();

            try
            {
                // -- 6. Save user changes ---------------------------------------------
                await _unitOfWork.Users.UpdateAsync(user);

                // -- 7. Replace permissions if updated --------------------------------
                // Strategy: delete all existing UsersPermission rows for this user
                // then re-insert the new merged set.
                if (permissionsUpdated && newPermissionIds is not null)
                {
                    var existingPermissions = await _unitOfWork.GetRepository<UsersPermission, long>()
                        .GetAsync(up => up.UserId == user.Id);

                    if (existingPermissions.Any())
                        await _unitOfWork.GetRepository<UsersPermission, long>()
                            .DeleteRangeAsync(existingPermissions);

                    if (newPermissionIds.Count > 0)
                    {
                        var updatedPermissions = newPermissionIds
                            .Select(pid => new UsersPermission
                            {
                                UserId = user.Id,
                                PermissionId = pid,
                            })
                            .ToList();

                        await _unitOfWork.GetRepository<UsersPermission, long>()
                            .AddRangeAsync(updatedPermissions);
                    }
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
                throw new Exception();
            }
        }
        public async Task<Result<string>> UpdateAssistantPermissionsAsync(UpdateAssistantPermissionsRequest dto)
        {
            // -- 1. Fetch assistant ---------------------------------------------------
            var assistant = await _unitOfWork.AssistantRepo.GetByIdAsync(dto.assistantId);
            if (assistant is null)
                return Result<string>.Failure(localizer, "AssistantNotFound");

            
            // -- 3. At least one source -----------------------------------------------
            var hasIndividualPerms = dto.PermissionIds is { Count: > 0 };
            var hasTemplates = dto.PermissionProfileIds is { Count: > 0 };

            if (!hasIndividualPerms && !hasTemplates)
                return Result<string>.Failure(localizer, "AssistantMustHaveAtLeastOnePermission");

            // -- 4. Resolve all permission IDs ----------------------------------------
            var allPermissionIds = new HashSet<long>();

            if (hasIndividualPerms)
            {
                var distinctPermIds = dto.PermissionIds!.Distinct().ToList();

                var validCount = await _unitOfWork.GetRepository<Permission, long>()
                    .CountAsync(p => distinctPermIds.Contains(p.Id));

                if (validCount != distinctPermIds.Count)
                    return Result<string>.Failure(localizer, "OneOrMorePermissionsInvalid");

                allPermissionIds.UnionWith(distinctPermIds);
            }

            if (hasTemplates)
            {
                var distinctTemplateIds = dto.PermissionProfileIds!.Distinct().ToList();

                var templatePermissions = await _unitOfWork.GetRepository<TemplatePermisions, long>()
                    .GetAsync(tp => distinctTemplateIds.Contains(tp.TemplateId));

                var foundTemplateIds = templatePermissions
                    .Select(tp => tp.TemplateId)
                    .Distinct()
                    .ToList();

                if (foundTemplateIds.Count != distinctTemplateIds.Count)
                    return Result<string>.Failure(localizer, "OneOrMorePermissionsInvalid");

                allPermissionIds.UnionWith(
                    templatePermissions.Select(tp => tp.PermisionId));
            }

            // -- 5. Replace UsersPermission rows --------------------------------------
            await _unitOfWork.BeginTransactionAsync();

            try
            {
                var existing = await _unitOfWork.GetRepository<UsersPermission, long>()
                    .GetAsync(up => up.UserId == assistant.UserId);

                if (existing.Any())
                    await _unitOfWork.GetRepository<UsersPermission, long>()
                        .DeleteRangeAsync(existing);

                var newPermissions = allPermissionIds
                    .Select(pid => new UsersPermission
                    {
                        UserId = assistant.UserId,
                        PermissionId = pid,
                    })
                    .ToList();

                await _unitOfWork.GetRepository<UsersPermission, long>()
                    .AddRangeAsync(newPermissions);

                await _unitOfWork.SaveChangesAsync();
                await _unitOfWork.CommitAsync();

                return Result<string>.Success("PermissionsUpdated", localizer);
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
    }
}
