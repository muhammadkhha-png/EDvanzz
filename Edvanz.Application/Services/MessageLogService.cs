using Edvanz.Application.Dtos;
using Edvanz.Application.Dtos.DispatcherDtos;
using Edvanz.Application.Dtos.MessageLogDtos;
using Edvanz.Application.IservicesContract;
using Edvanz.Domain.Entities.Messaging;
using Edvanz.Domain.Interfaces;
using Edvanz.Domain.Resources;
using Microsoft.Extensions.Localization;
using System;
using System.Collections.Generic;
using System.Text;

namespace Edvanz.Application.Services
{
    public class MessageLogService : IMessageLogService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IStringLocalizer<Messages> _localizer;

        public MessageLogService(IUnitOfWork unitOfWork, IStringLocalizer<Messages> localizer)
        {
            _unitOfWork = unitOfWork;
            _localizer = localizer;
        }

        // ── GET HISTORY ────────────────────
        public async Task<Result<PaginatedResponse<List<MessageLogDto>>>> GetHistoryAsync(
            MessageLogQueryDto query)
        {
            var (data, totalCount) = await _unitOfWork.messageLogRepo
                .GetPagedAsync(
                    teacherId: query.TeacherId,
                    studentName: query.StudentName,
                    studentCode: query.StudentCode,
                    from: query.From,
                    to: query.To,
                    triggerType: query.TriggerType,
                    status: query.Status,
                    channel: query.Channel,
                    page: query.Page,
                    pageSize: query.PageSize);

            var mapped = data.Select(l => new MessageLogDto
            {
                Id = l.Id,
                StudentName = l.StudentName,
                RecipientPhone = l.RecipientPhone,
                RecipientType = l.RecipientType.ToString(),
                Channel = l.Channel.ToString(),
                ResolvedContent = l.ResolvedContent,
                Status = l.Status.ToString(),
                FailureReason = l.FailureReason,
                TriggerType = l.TriggerType?.ToString(),
                IsManual = l.IsManual,
                SentAt = l.SentAt,
                DeliveredAt = l.DeliveredAt,
            }).ToList();

            var response = new PaginatedResponse<List<MessageLogDto>>
            {
                totalCount = totalCount,
                page = query.Page,
                pageSize = query.PageSize,
                totalPages = (int)Math.Ceiling((double)totalCount / query.PageSize),
                data = mapped,
            };

            return Result<PaginatedResponse<List<MessageLogDto>>>.Success(response, _localizer);
        }

        // ── RESEND ─────
        // Re-queues the exact same resolved content to the same phone
        public async Task<Result<string>> ResendAsync(long teacherId, long messageLogId)
        {
            var log = await _unitOfWork.GetRepository<MessageLog, long>()
                .FindAsync(l => l.Id == messageLogId && l.TeacherId == teacherId);

            if (log is null)
                return Result<string>.Failure(_localizer, "MessageLogNotFound");

            if (log.Status != MessageStatus.Failed)
                return Result<string>.Failure(_localizer, "OnlyFailedMessagesCanBeResent");

            var channel = await _unitOfWork.GetRepository<MessagingChannel, long>()
                .FindAsync(c => c.TeacherId == teacherId
                             && c.ChannelType == log.Channel
                             && c.IsActive);

            if (channel is null)
                return Result<string>.Failure(_localizer, "ChannelNotActiveOrConfigured");

           MessageDispatcher.EnqueueOrSchedule(new MessageSendPayload
            {
                TeacherId = teacherId,
                StudentId = log.StudentId,
                StudentName = log.StudentName,
                StudentCode = log.StudentCode,
               RecipientPhone = log.RecipientPhone,
                RecipientType = log.RecipientType,
                ResolvedContent = log.ResolvedContent,
                Channel = log.Channel,
                MessageTemplateId = log.MessageTemplateId,
                IsManual = log.IsManual,
                TriggerType = log.TriggerType,
                ScheduledSendAt = null // resend = immediate
            });

            return Result<string>.Success("MessageRequeued", _localizer);
        }
    }

}
