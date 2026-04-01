using Edvanz.Application.Dtos;
using Edvanz.Application.Dtos.AssistantDtos;
using Edvanz.Application.IservicesContract;
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

        public AssistantService(IUnitOfWork unitOfWork, IStringLocalizer<Messages> localizer)
        {
            _unitOfWork = unitOfWork;
            this.localizer = localizer;
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
                    teacherName = a.Teacher.User.FullName
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
    }
}
