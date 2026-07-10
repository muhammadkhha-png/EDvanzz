using DocumentFormat.OpenXml.Drawing;
using DocumentFormat.OpenXml.Wordprocessing;
using Edvanz.Application.Dtos;
using Edvanz.Application.Dtos.TriggerDtos;
using Edvanz.Application.IservicesContract;
using Edvanz.Application.ServiceContract;
using Edvanz.Domain.Constants;
using Edvanz.Domain.Entities.Messaging;
using Edvanz.Domain.Enums;
using Edvanz.Domain.Interfaces;
using Edvanz.Domain.Resources;
using Microsoft.Extensions.Localization;
using System;
using System.Collections.Generic;
using System.Text;

namespace Edvanz.Application.Services
{
    public class AutomatedTriggerService : IAutomatedTriggerService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IStringLocalizer<Messages> _localizer;
        private readonly ISubscriptionGateService _subscriptionGate;

        public AutomatedTriggerService(IUnitOfWork unitOfWork, IStringLocalizer<Messages> localizer, ISubscriptionGateService subscriptionGate)
        {
            _unitOfWork = unitOfWork;
            _localizer = localizer;
            _subscriptionGate = subscriptionGate;
        }

        // ── GET ALL ────
        public async Task<Result<List<TriggerDto>>> GetAllAsync(long teacherId)
        {
            var triggers = await _unitOfWork.automatedTriggerRepo.GetByTeacherIdAsync( teacherId);

            // Bulk load template names
            var templateIds = triggers.Select(t => t.MessageTemplateId).Distinct().ToList();
            var templates = await _unitOfWork.messageTemplateRepo.GetByIdsAsync(templateIds);
            var templateMap = templates.ToDictionary(t => t.Id, t => t.Name);

            var result = triggers.Select(t => MapToDto(t, templateMap)).ToList();
            return Result<List<TriggerDto>>.Success(result, _localizer);
        }

        // ── GET BY ID ─────────────────────────────────────────────────
        public async Task<Result<TriggerDto>> GetByIdAsync(long teacherId, long triggerId)
        {
            var trigger = await _unitOfWork.automatedTriggerRepo.GetByTeacherIdAndTriggerId(teacherId, triggerId);

            if (trigger is null)
                return Result<TriggerDto>.Failure(_localizer, "TriggerNotFound");

            var template = await _unitOfWork.messageTemplateRepo.GetByIdAsync(trigger.MessageTemplateId);

            return Result<TriggerDto>.Success(
                MapToDto(trigger, new Dictionary<long, string>
                {
                    [trigger.MessageTemplateId] = template?.Name ?? string.Empty
                }),
                _localizer);
        }

        // ── CREATE ────────────────────────────────────────────────────
        public async Task<Result<string>> CreateAsync(long teacherId, CreateTriggerDto dto)
        {
            // Free-tier quota: automated triggers (default 0 → subscriber-only; see ModuleQuota table).
            if (!await _subscriptionGate.CanCreateAsync(
                    teacherId, ModuleQuotaKeys.Triggers,
                    () => _unitOfWork.automatedTriggerRepo.CountByTeacherAsync(teacherId)))
                return Result<string>.Failure(
                    _localizer, SubscriptionConstants.Messages.SubscriptionRequired,
                    System.Net.HttpStatusCode.Forbidden);

            // Validate template exists and belongs to teacher
            var template = await _unitOfWork.messageTemplateRepo.GetByIdAndTeacherIdAsync(teacherId, dto.MessageTemplateId);
               
            if (template is null)
                return Result<string>.Failure(_localizer, "TemplateNotFound");

            // Validate scheduled time if needed (REQ-MSG-024)
            if (dto.SendTiming == SendTimingType.Scheduled && dto.ScheduledTime is null)
                return Result<string>.Failure(_localizer, "ScheduledTimeRequiredForScheduledTrigger");

            // Validate threshold for threshold-based events
            if (IsThresholdEvent(dto.EventType) && (dto.ThresholdValue is null || dto.ThresholdValue <= 0))
                return Result<string>.Failure(_localizer, "ThresholdValueRequired");

            // Validate scope has the required ID
            if (dto.Scope == TriggerScope.Session && dto.SessionId is null)
                return Result<string>.Failure(_localizer, "SessionIdRequiredForSessionScope");
            if (dto.Scope == TriggerScope.SessionGroup && dto.SessionGroupId is null)
                return Result<string>.Failure(_localizer, "SessionGroupIdRequiredForSessionGroupScope");

            var trigger = new AutomatedTrigger
            {
                TeacherId = teacherId,
                MessageTemplateId = dto.MessageTemplateId,
                EventType = dto.EventType,
                IsActive = true,
                SendTiming = dto.SendTiming,
                ScheduledTime = dto.SendTiming == SendTimingType.Scheduled ? dto.ScheduledTime : null,
                ThresholdValue = dto.ThresholdValue,
                Scope = dto.Scope,
                SessionId = dto.Scope == TriggerScope.Session ? dto.SessionId : null,
                SessionGroupId = dto.Scope == TriggerScope.SessionGroup ? dto.SessionGroupId : null,
                UpdatedAt = DateTime.UtcNow,
                CreateAt = DateTime.UtcNow,
            };

            await _unitOfWork.GetRepository<AutomatedTrigger, long>().AddAsync(trigger);
            await _unitOfWork.SaveChangesAsync();

            return Result<string>.Success("TriggerCreated", _localizer);
        }

        // ── UPDATE ────────────────────────────────────────────────────
        public async Task<Result<string>> UpdateAsync(long teacherId, UpdateTriggerDto dto)
        {

            var isTeacherExist = await _unitOfWork.Users.GetActiveTeacherByIdAsync(dto.teacherId);
            if (isTeacherExist is null)
                return Result<string>.Failure(_localizer, "TeacherNotFound");

            var trigger = await _unitOfWork.automatedTriggerRepo.GetByTeacherIdAndTriggerId(teacherId, dto.TriggerId);
            if (trigger is null)
                return Result<string>.Failure(_localizer, "TriggerNotFound");

            if (dto.MessageTemplateId.HasValue)
            {
                var template = await _unitOfWork.messageTemplateRepo.GetByIdAndTeacherIdAsync(teacherId, dto.MessageTemplateId.Value);
                if (template is null)
                    return Result<string>.Failure(_localizer, "TemplateNotFound");
                trigger.MessageTemplateId = dto.MessageTemplateId.Value;
            }

            if (dto.SendTiming.HasValue)
            {
                trigger.SendTiming = dto.SendTiming.Value;
                trigger.ScheduledTime = dto.SendTiming == SendTimingType.Scheduled
                    ? dto.ScheduledTime
                    : null;
            }

            if (dto.ThresholdValue.HasValue) trigger.ThresholdValue = dto.ThresholdValue;
            if (dto.Scope.HasValue)
            {
                trigger.Scope = dto.Scope.Value;
                trigger.SessionId = dto.Scope == TriggerScope.Session ? dto.SessionId : null;
                trigger.SessionGroupId = dto.Scope == TriggerScope.SessionGroup ? dto.SessionGroupId : null;
            }

            trigger.UpdatedAt = DateTime.UtcNow;
            await _unitOfWork.automatedTriggerRepo.UpdateAsync(trigger);
            await _unitOfWork.SaveChangesAsync();

            return Result<string>.Success("TriggerUpdated", _localizer);
        }

        // ── DELETE ────────────────────────────────────────────────────
        public async Task<Result<string>> DeleteAsync(long teacherId, long triggerId)
        {
            var isTeacherExist = await _unitOfWork.Users.GetActiveTeacherByIdAsync(teacherId);
            if (isTeacherExist is null)
                return Result<string>.Failure(_localizer, "TeacherNotFound");

            var trigger = await _unitOfWork.automatedTriggerRepo.GetByTeacherIdAndTriggerId(teacherId, triggerId);
            if (trigger is null)
                return Result<string>.Failure(_localizer, "TriggerNotFound");

            await _unitOfWork.automatedTriggerRepo.DeleteAsync(trigger);
            await _unitOfWork.SaveChangesAsync();

            return Result<string>.Success("TriggerDeleted", _localizer);
        }

        // ── TOGGLE ─
        public async Task<Result<string>> ToggleAsync(long teacherId, long triggerId, bool activate)
        {
            var isTeacherExist = await _unitOfWork.Users.GetActiveTeacherByIdAsync(teacherId);
            if (isTeacherExist is null)
                return Result<string>.Failure(_localizer, "TeacherNotFound");

            var trigger = await _unitOfWork.automatedTriggerRepo.GetByTeacherIdAndTriggerId(teacherId, triggerId);
            if (trigger is null)
                return Result<string>.Failure(_localizer, "TriggerNotFound");

            if (trigger.IsActive == activate)
                return Result<string>.Failure(_localizer,
                    activate ? "TriggerAlreadyActive" : "TriggerAlreadyInactive");

            trigger.IsActive = activate;
            trigger.UpdatedAt = DateTime.UtcNow;

            await _unitOfWork.automatedTriggerRepo.UpdateAsync(trigger);
            await _unitOfWork.SaveChangesAsync();

            return Result<string>.Success(
                activate ? "TriggerActivated" : "TriggerDeactivated", _localizer);
        }

        // ── GET MATCHING TRIGGER (used by dispatcher) ─────────────────
        // Priority: Session-specific > SessionGroup-specific > 
        public async Task<AutomatedTrigger?> GetMatchingTriggerAsync(
            long teacherId, TriggerEventType eventType, long? sessionId, long? sessionGroupId)
        {
            var candidates = await _unitOfWork.automatedTriggerRepo.GetActiveByTeacherAndEvent(teacherId, eventType.ToString());

            // Most specific first
            if (sessionId.HasValue)
            {
                var sessionTrigger = candidates
                    .FirstOrDefault(t => t.Scope == TriggerScope.Session && t.SessionId == sessionId);
                if (sessionTrigger is not null) return sessionTrigger;
            }

            if (sessionGroupId.HasValue)
            {
                var groupTrigger = candidates
                    .FirstOrDefault(t => t.Scope == TriggerScope.SessionGroup
                                      && t.SessionGroupId == sessionGroupId);
                if (groupTrigger is not null) return groupTrigger;
            }

            return candidates.FirstOrDefault(t => t.Scope == TriggerScope.All);
        }

        public async Task<Result<PaginatedResponse<List<TriggerDto>>>> GetPagedAsync(TriggerFiltetrReq req)
        {
            var isTeacherExist= await _unitOfWork.Users.GetActiveTeacherByIdAsync(req.teacherId);
            if (isTeacherExist is null)
                return Result<PaginatedResponse<List<TriggerDto>>>.Failure(_localizer, "TeacherNotFound");

            var (triggers, count) = await _unitOfWork.automatedTriggerRepo.GetFilteredAsync(
                req.teacherId,
                req.eventType,
                req.scope,
                req.isActive,
                req.pageSize,
                req.page
            );

            // 🔹 Load templates
            var templateIds = triggers.Select(t => t.MessageTemplateId).Distinct().ToList();
            var templates = await _unitOfWork.messageTemplateRepo.GetByIdsAsync(templateIds);
            var templateMap = templates.ToDictionary(t => t.Id, t => t.Name);

            var data = triggers
                .Select(t => MapToDto(t, templateMap))
                .ToList();

            var response = new PaginatedResponse<List<TriggerDto>>
            {
                data = data,
                totalCount = count,
                page = req.page ?? 1,
                pageSize = req.pageSize ?? count,
                totalPages = (int)Math.Ceiling((double)count / (req.pageSize ?? count))
            };

            return Result<PaginatedResponse<List<TriggerDto>>>.Success(response, _localizer);
        }

        // ── Helpers ───────────────────────────────────────────────────

        private static bool IsThresholdEvent(TriggerEventType type) =>
            type is TriggerEventType.ConsecutiveAbsenceAlert
                  or TriggerEventType.ConsecutiveNonPayment
                  or TriggerEventType.GradeBelowThreshold;

        private static TriggerDto MapToDto(AutomatedTrigger t, Dictionary<long, string> templateMap) => new()
        {
            Id = t.Id,
            EventType = t.EventType,
            MessageTemplateId = t.MessageTemplateId,
            TemplateName = templateMap.GetValueOrDefault(t.MessageTemplateId, string.Empty),
            IsActive = t.IsActive,
            SendTiming = t.SendTiming,
            ScheduledTime = t.ScheduledTime,
            ThresholdValue = t.ThresholdValue,
            Scope = t.Scope,
            SessionId = t.SessionId,
            SessionGroupId = t.SessionGroupId,
        };
    }
}
