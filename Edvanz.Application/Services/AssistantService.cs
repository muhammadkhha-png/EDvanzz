using Edvanz.Application.Dtos;
using Edvanz.Application.Dtos.AssistantDtos;
using Edvanz.Application.Dtos.ModulesPermissions;
using Edvanz.Application.Dtos.PermissionsDtos;
using Edvanz.Application.Dtos.TemplatePermissionsDtos;
using Edvanz.Application.IservicesContract;
using Edvanz.Domain.Entities;
using Edvanz.Domain.Interfaces;
using Edvanz.Domain.Resources;
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

        public AssistantService(IUnitOfWork unitOfWork, IStringLocalizer<Messages> localizer,IUserPermissionService userPermissionService)
        {
            _unitOfWork = unitOfWork;
            this.localizer = localizer;
            this.userPermissionService = userPermissionService;
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
                isActive = (bool)assistant.User.IsActive,
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
    }
}
