using Edvanz.Application.Dtos;
using Edvanz.Application.Dtos.AuditTrial;
using Edvanz.Application.Excel;
using Edvanz.Application.IservicesContract;
using Edvanz.Application.Reporting;
using Edvanz.Application.ServiceContract;
using Edvanz.Domain.Entities;
using Edvanz.Domain.Interfaces;
using Edvanz.Domain.Resources;
using Microsoft.Extensions.Localization;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Reflection;
using System.Text;

namespace Edvanz.Application.Services
{
    public class AuditTrialService : IAudittrialService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IStringLocalizer<Messages> localizer;
        private readonly ExcelService _excelService;
        private readonly ITimeZoneService _timeZoneService;
        private readonly IPdfExportService _pdfExportService;
        // REQ-USR-030 PDF row cap. Excel stays unbounded; the cap governs the rendered
        // document, not the query. Promote to the Options pattern if it ever needs to vary.
        private const int MaxPdfRows = 5000;
        public AuditTrialService(IUnitOfWork unitOfWork, IStringLocalizer<Messages> _localizer, ExcelService _excelService, ITimeZoneService timeZoneService, IPdfExportService pdfExportService)
        {
            this._unitOfWork = unitOfWork;
            localizer = _localizer;
            this._excelService = _excelService;
            _timeZoneService = timeZoneService;
            _pdfExportService = pdfExportService;
        }

        public async Task<Result<byte[]>> ExportToExcel(AuditTrialExcelFilterQuery filter)
        {
            try
            {// 🔹 Resolve the client's local filter window to UTC before querying the
                //    UTC-stored CreatedAt: From → start of the local day; To → end of the
                //    local day (inclusive, matching the repo's <= To comparison).
                DateTime? fromUtc = filter.From.HasValue
                    ? _timeZoneService.ConvertLocalToUtc(filter.From.Value.Date, filter.TimeZoneId)
                    : null;
                DateTime? toUtc = filter.To.HasValue
                    ? _timeZoneService.ConvertLocalToUtc(filter.To.Value.Date.AddDays(1).AddTicks(-1), filter.TimeZoneId)
                    : null;

                var data = await _unitOfWork.auditTrialRepo
                    .GetTeacherAssitantsAuditTrialForExcel(
                        filter.teacherID,
                        filter.ActionType,
                        filter.AssistantName,
                        filter.Module,
                        fromUtc,
                        toUtc);

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
                    CreatedAt = _timeZoneService.ConvertUtcToLocal(a.CreateAt, filter.TimeZoneId)
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

        public async Task<Result<byte[]>> ExportToPdf(AuditTrialExcelFilterQuery filter)
        {
            try
            {
                // Resolve the client's local filter window to UTC (same as the Excel path).
                DateTime? fromUtc = filter.From.HasValue
                    ? _timeZoneService.ConvertLocalToUtc(filter.From.Value.Date, filter.TimeZoneId)
                    : null;
                DateTime? toUtc = filter.To.HasValue
                    ? _timeZoneService.ConvertLocalToUtc(filter.To.Value.Date.AddDays(1).AddTicks(-1), filter.TimeZoneId)
                    : null;

                var data = await _unitOfWork.auditTrialRepo
                    .GetTeacherAssitantsAuditTrialForExcel(
                        filter.teacherID,
                        filter.ActionType,
                        filter.AssistantName,
                        filter.Module,
                        fromUtc,
                        toUtc);

                if (data == null || !data.Any())
                {
                    return Result<byte[]>.Failure(
                        localizer, "NoDataToExport", HttpStatusCode.BadRequest);
                }

                var totalCount = data.Count;

                // Apply the PDF row cap; CreatedAt converted to the client's local zone.
                var mapped = data
                    .Take(MaxPdfRows)
                    .Select(a => new AuditTrialExcelDto
                    {
                        Id = a.Id,
                        AssistantName = a.assistant?.User?.FullName ?? "",
                        ModuleName = a.module?.Name ?? "",
                        ActionType = a.actionType.ToString(),
                        Desc = a.Desc,
                        CreatedAt = _timeZoneService.ConvertUtcToLocal(a.CreateAt, filter.TimeZoneId)
                    })
                    .ToList();

                var teacherName = await _unitOfWork.Users.GetTeacherDisplayNameAsync(filter.teacherID) ?? "";

                var header = BuildPdfHeader(filter, totalCount, teacherName);
                var columnHeaders = BuildLocalizedColumnHeaders();

                var bytes = await _pdfExportService.ExportToPdfAsync(mapped, columnHeaders, header);

                return Result<byte[]>.Success(
                    bytes, localizer, "ExportSuccess", HttpStatusCode.OK);
            }
            catch (ArgumentException ex)
            {
                return Result<byte[]>.Failure(ex.Message, HttpStatusCode.BadRequest);
            }
            catch (Exception)
            {
                return Result<byte[]>.Failure(
                    localizer, "ExportFailed", HttpStatusCode.InternalServerError);
            }
        }

        private PdfReportHeader BuildPdfHeader(AuditTrialExcelFilterQuery filter, int totalCount, string teacherName)
        {
            var localNow = _timeZoneService.ConvertUtcToLocal(DateTime.UtcNow, filter.TimeZoneId);
            var zone = string.IsNullOrWhiteSpace(filter.TimeZoneId) ? "Africa/Cairo" : filter.TimeZoneId!;
            var isArabic = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "ar";

            string rangeText;
            if (filter.From.HasValue && filter.To.HasValue)
                rangeText = $"{filter.From.Value:yyyy-MM-dd} – {filter.To.Value:yyyy-MM-dd}";
            else if (filter.From.HasValue)
                rangeText = $"{filter.From.Value:yyyy-MM-dd} – …";
            else if (filter.To.HasValue)
                rangeText = $"… – {filter.To.Value:yyyy-MM-dd}";
            else
                rangeText = localizer["AuditFilterAll"].Value;

            var filterSummary = string.Join("   ·   ",
                $"{localizer["AuditCol_Assistant"].Value}: {ValueOrAll(filter.AssistantName)}",
                $"{localizer["AuditCol_Action"].Value}: {ValueOrAll(filter.ActionType)}",
                $"{localizer["AuditCol_Module"].Value}: {ValueOrAll(filter.Module)}",
                $"{localizer["AuditFilterRange"].Value}: {rangeText}");

            var notice = totalCount > MaxPdfRows
                ? string.Format(localizer["AuditPdfTruncated"].Value, MaxPdfRows, totalCount)
                : null;

            return new PdfReportHeader
            {
                PrimaryHeading = teacherName,
                Title = localizer["AuditReportTitle"].Value,
                GeneratedAtDisplay = $"{localizer["AuditGeneratedAt"].Value}: {localNow:yyyy-MM-dd HH:mm} ({zone})",
                FilterSummary = filterSummary,
                FooterText = $"Edvanz — {localizer["AuditReportTitle"].Value}",
                TruncationNotice = notice,
                Direction = isArabic ? PdfTextDirection.RightToLeft : PdfTextDirection.LeftToRight
            };
        }

        private string ValueOrAll(string? value) =>
            string.IsNullOrWhiteSpace(value) ? localizer["AuditFilterAll"].Value : value;

        private IReadOnlyList<string> BuildLocalizedColumnHeaders()
        {
            return typeof(AuditTrialExcelDto).GetProperties()
                .Select(p => p.GetCustomAttribute<ReportColumnAttribute>())
                .Where(a => a is not null)
                .OrderBy(a => a!.Order)
                .Select(a => localizer[a!.Key].Value)
                .ToList();
        }
    }
}
