using Edvanz.Application.Dtos;
using Edvanz.Application.Dtos.TriggerDtos;
using Edvanz.Application.IservicesContract;
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

        public AutomatedTriggerService(IUnitOfWork unitOfWork, IStringLocalizer<Messages> localizer)
        {
            _unitOfWork = unitOfWork;
            _localizer = localizer;
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
