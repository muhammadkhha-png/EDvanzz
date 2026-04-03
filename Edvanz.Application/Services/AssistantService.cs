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
using Microsoft.Extensions.Localization;
using System;
using System.Collections.Generic;
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
            // An assistant with zero permissions is functionally useless (BR-USR-002).
            // The caller satisfies this with:
            //   - individual permissionIds only
            //   - template permissionProfileIds only
            //   - a mix of both
            // Both paths ultimately resolve to User <-> Permission rows at the DB level.
            var hasIndividualPerms = dto.permissionIds is { Count: > 0 };
            var hasTemplates = dto.permissionProfileIds is { Count: > 0 };

            if (!hasIndividualPerms && !hasTemplates)
                return Result<string?>.Failure(localizer, "AssistantMustHaveAtLeastOnePermission");

            // -- 4. Validate individual permission IDs --------------------------------
            // De-duplicate before the DB call so Count comparison is accurate.
            List<long>? distinctPermIds = null;
            if (hasIndividualPerms)
            {
                distinctPermIds = dto.permissionIds!.Distinct().ToList();

                var validPermCount = await _unitOfWork.GetRepository<Permission, long>()
                    .CountAsync(p => distinctPermIds.Contains(p.Id));

                if (validPermCount != distinctPermIds.Count)
                    return Result<string?>.Failure(localizer, "OneOrMorePermissionsInvalid");
            }

            // -- 5. Validate template IDs ---------------------------------------------
            // Templates are a shortcut: they expand to their underlying Permission rows.
            // Both paths end up as UsersPermission / TemplatePermissionsUsers links.
            List<long>? distinctTemplateIds = null;
            if (hasTemplates)
            {
                distinctTemplateIds = dto.permissionProfileIds!.Distinct().ToList();

                var validTemplateCount = await _unitOfWork.GetRepository<Template, long>()
                    .CountAsync(t => distinctTemplateIds.Contains(t.Id));

                if (validTemplateCount != distinctTemplateIds.Count)
                    return Result<string?>.Failure(localizer, "OneOrMorePermissionsInvalid");
            }

            // -- 6. Build User entity -------------------------------------------------
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
                // -- 7. Persist User -> get newUser.Id --------------------------------
                await _unitOfWork.Users.AddAsync(newUser);
                await _unitOfWork.SaveChangesAsync();

                // -- 8. Persist Assistant -> get assistant.Id -------------------------
                var assistant = new Assistant
                {
                    UserId = newUser.Id,
                    TeacherAccountId = dto.teacherId,
                    AccountStatus = AccountStatus.Active,
                    CreateAt = DateTime.UtcNow,
                };

                await _unitOfWork.AssistantRepo.AddAsync(assistant);
                await _unitOfWork.SaveChangesAsync();

                // -- 9. Individual permissions -> UsersPermission rows ----------------
                if (distinctPermIds is { Count: > 0 })
                {
                    var userPermissions = distinctPermIds
                        .Select(pid => new UsersPermission
                        {
                            UserId = newUser.Id,
                            PermissionId = pid,
                        })
                        .ToList();

                    await _unitOfWork.GetRepository<UsersPermission, long>()
                        .AddRangeAsync(userPermissions);
                }

                // -- 10. Template assignments -> TemplatePermissionsUsers rows ---------
                if (distinctTemplateIds is { Count: > 0 })
                {
                    var templateLinks = distinctTemplateIds
                        .Select(tid => new TemplatePermissionsUsers
                        {
                            TemplateId = tid,
                            AssisstantId = assistant.Id,
                        })
                        .ToList();

                    await _unitOfWork.GetRepository<TemplatePermissionsUsers, long>()
                        .AddRangeAsync(templateLinks);
                }

                await _unitOfWork.SaveChangesAsync();
                await _unitOfWork.CommitAsync();

                // -- 11. Re-fetch with navigations and map ----------------------------
                var created = await _unitOfWork.AssistantRepo.GetAssistantWithPermissionsAsync(assistant.Id);

                return Result<string?>.Success("Success", localizer);
            }
            catch
            {
                await _unitOfWork.RollbackAsync();
                throw;
            }
        }

      
        
    }
}
