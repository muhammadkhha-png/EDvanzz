using Edvanz.Application.Dtos;
using Edvanz.Application.Dtos.AuditTrial;
using Edvanz.Application.Excel;
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
    public class AuditTrialService:IAudittrialService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IStringLocalizer<Messages> localizer;
        private readonly ExcelService _excelService;

        public AuditTrialService(IUnitOfWork unitOfWork,IStringLocalizer<Messages> _localizer,ExcelService _excelService)
        {
            this._unitOfWork = unitOfWork;
            localizer = _localizer;
            this._excelService = _excelService;
        }

        public async Task<Result<byte[]>> ExportToExcel(AuditTrialExcelFilterQuery filter)
        {
            try
            {
                // 🔹 Get data from DB (IMPORTANT: await)
                var data = await _unitOfWork.auditTrialRepo
                    .GetTeacherAssitantsAuditTrialForExcel(
                        filter.teacherID,
                        filter.ActionType,
                        filter.AssistantName,
                        filter.Module,
                        filter.From,
                        filter.To);

                // 🔹 Validation
                if (data == null || !data.Any())
                {
                    return Result<byte[]>.Failure(
                        localizer,
                        "NoDataToExport",
                        HttpStatusCode.BadRequest);
                }

                // 🔹 Mapping
                var mapped = data.Select(a => new AuditTrialExcelDto
                {
                    Id = a.Id,
                    AssistantName = a.assistant?.User?.FullName ?? "",
                    ModuleName = a.module?.Name ?? "",
                    ActionType = a.actionType.ToString(),
                    Desc = a.Desc,
                    CreatedAt = a.CreateAt
                }).ToList();

                // 🔹 Generate Excel
                var bytes = _excelService.ExportToExcel(mapped);

                // 🔹 Success
                return Result<byte[]>.Success(
                    bytes,
                    localizer,
                    "ExportSuccess",
                    HttpStatusCode.OK);
            }
            catch (ArgumentException ex)
            {
                return Result<byte[]>.Failure(
                    ex.Message,
                    HttpStatusCode.BadRequest);
            }
            catch (Exception)
            {
                return Result<byte[]>.Failure(
                    localizer,
                    "ExportFailed",
                    HttpStatusCode.InternalServerError);
            }
        }

        public async Task<Result<PaginatedResponse<List<AuditTrialListDto>>>> GetAssistantsAuditTrialsPerTeacher(AuditTrailQueryRequest dto)
        {
           var page = dto.Page <= 0 ? 1 : dto.Page;
                var pageSize = dto.PageSize <= 0 ? 10 : dto.PageSize;

                var (data, count) = await _unitOfWork.auditTrialRepo
                    .GetTeacherAssitantsAuditTrial(
                        dto.teacherID,
                        dto.ActionType,
                        dto.AssistantName,
                        dto.Module,
                        dto.From,
                        dto.To,
                        page,
                        pageSize);

                var mappedData = data.Select(a => new AuditTrialListDto
                {
                    id = a.Id,
              
                    assisstantName = a.assistant?.User?.FullName,
                    moduleName = a.module?.Name,
                    acction = a.actionType.ToString(),
                    desc = a.Desc,
                    createAt = a.CreateAt
                }).ToList();

                var response = new PaginatedResponse<List<AuditTrialListDto>>
                {
                    totalCount = count,
                    page = page,
                    pageSize = pageSize,
                    totalPages = (int)Math.Ceiling((double)count / pageSize),
                    data = mappedData
                };

                return Result<PaginatedResponse<List<AuditTrialListDto>>>
                    .Success(response, localizer);
           
        }
        public async Task RecordAuditTrailAsync(CreateAuditTrailDto dto)
        {
            var audit = new AuditTrail
            {
                teacherId = dto.teacherId,
                AssistantId = dto.assistantId,
                actionType = dto.actionType,
                ModuleId = dto.moduleId,
                Desc = dto.desc,
                CreateAt = DateTime.UtcNow,
            };

            await _unitOfWork.GetRepository<AuditTrail, long>().AddAsync(audit);
            await _unitOfWork.SaveChangesAsync();
        }
    }
}
