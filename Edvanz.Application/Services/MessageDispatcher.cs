using Edvanz.Application.Dtos;
using Edvanz.Application.Dtos.DispatcherDtos;
using Edvanz.Application.Dtos.MessageResolver;
using Edvanz.Application.IservicesContract;
using Edvanz.Domain.Entities;
using Edvanz.Domain.Entities.Messaging;
using Edvanz.Domain.Enums;
using Edvanz.Domain.Interfaces;
using Edvanz.Domain.Resources;
using Hangfire;
using Microsoft.Extensions.Localization;
using System;
using System.Collections.Generic;
using System.Text;

namespace Edvanz.Application.Services
{
    public class MessageDispatcher : IMessageDispatcher
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IAutomatedTriggerService _triggerService;
        private readonly IBlockResolver _resolver;
        private readonly IStringLocalizer<Messages> _localizer;
        private readonly IMessageLogService _logService;

        public MessageDispatcher(
            IUnitOfWork unitOfWork,
            IAutomatedTriggerService triggerService,
            IBlockResolver resolver,
            IStringLocalizer<Messages> localizer,IMessageLogService logService)
        {
            _unitOfWork = unitOfWork;
            _triggerService = triggerService;
            _resolver = resolver;
            _localizer = localizer;
            this._logService = logService;
        }

        // ── AUTOMATED DISPATCH ────────────────────────────────────────

        public async Task DispatchAsync(DispatchRequest request)
        {
            if (!request.StudentIds.Any()) return;

            // 1. Find the matching active trigger
            var trigger = await _triggerService.GetMatchingTriggerAsync(
                request.TeacherId, request.EventType,
                request.SessionId, request.SessionGroupId);

            if (trigger is null) return;

            // 2. Load template + blocks
            var template = await _unitOfWork.messageTemplateRepo
                .GetByIdWithBlocksAsync(trigger.MessageTemplateId);

            if (template is null || !template.IsActive) return;

            // 3. Verify channel is connected
            var channel = await _unitOfWork.GetRepository<MessagingChannel, long>()
                .FindAsync(c =>
                    c.TeacherId == request.TeacherId &&
                    c.ChannelType == template.Channel &&
                    c.IsActive);

            if (channel is null) return;

            // 4. Load students in bulk
            var students = await _unitOfWork.Students
                .GetActiveByIdsAndTeacherAsync(request.TeacherId, request.StudentIds);

            var orderedBlocks = template.Blocks.OrderBy(b => b.SortOrder).ToList();

            // 5. One Hangfire job per student (BR-MSG-003)
            foreach (var student in students)
            {
                var ctx = request.PerStudentContext?.GetValueOrDefault(student.Id)
                       ?? request.SharedContext
                       ?? new MessageResolveContext();

                ctx.StudentId = student.Id;
                ctx.StudentName = student.StudentName ?? string.Empty;
                ctx.StudentCode = student.StudentCode ?? string.Empty;

                var resolved = _resolver.Resolve(orderedBlocks, ctx);

                // ── Student phone ──────────────────────────────────────
                if (template.RecipientTarget is RecipientTarget.Student or RecipientTarget.Both)
                {
                    if (!string.IsNullOrEmpty(student.StudentPhoneNumber))
                    {
                        EnqueueOrSchedule(new MessageSendPayload
                        {
                            TeacherId = request.TeacherId,
                            StudentId = student.Id,
                            StudentName = ctx.StudentName,
                            RecipientPhone = student.StudentPhoneNumber,
                            RecipientType = RecipientTarget.Student,
                            ResolvedContent = resolved,
                            Channel = template.Channel,
                            MessageTemplateId = template.Id,
                            TriggerType = trigger.EventType,
                            IsManual = false,
                            ScheduledSendAt = BuildScheduledTime(trigger.SendTiming, trigger.ScheduledTime),
                        });
                    }
                    else
                    {
                        await LogMissingPhoneAsync(request.TeacherId, student.Id,
                            RecipientTarget.Student, template.Id, trigger.EventType);
                    }
                }

                // ── Parent phone ───────────────────────────────────────
                if (template.RecipientTarget is RecipientTarget.Parent or RecipientTarget.Both)
                {
                    if (!string.IsNullOrEmpty(student.ParentPhoneNumber))
                    {
                        EnqueueOrSchedule(new MessageSendPayload
                        {
                            TeacherId = request.TeacherId,
                            StudentId = student.Id,
                            StudentName = ctx.StudentName,
                            RecipientPhone = student.ParentPhoneNumber,
                            RecipientType = RecipientTarget.Parent,
                            ResolvedContent = resolved,
                            Channel = template.Channel,
                            MessageTemplateId = template.Id,
                            TriggerType = trigger.EventType,
                            IsManual = false,
                            ScheduledSendAt = BuildScheduledTime(trigger.SendTiming, trigger.ScheduledTime),
                        });
                    }
                    else
                    {
                        await LogMissingPhoneAsync(request.TeacherId, student.Id,
                            RecipientTarget.Parent, template.Id, trigger.EventType);
                    }
                }
            }
        }

        // ── MANUAL DISPATCH  ────────────────────

        public async Task<Result<ManualSendSummaryDto>> DispatchManualAsync(ManualMessageRequests request)
        {
            var template = await _unitOfWork.messageTemplateRepo
                .GetByIdWithBlocksAsync(request.MessageTemplateId);

            if (template is null || template.TeacherId != request.TeacherId)
                return Result<ManualSendSummaryDto>.Failure(_localizer, "TemplateNotFound");

            var channel = request.ChannelOverride ?? template.Channel;
            var recipientTarget = request.RecipientTargetOverride ?? template.RecipientTarget;

            // Verify channel active
            var channelEntity = await _unitOfWork.GetRepository<MessagingChannel, long>()
                .FindAsync(c => c.TeacherId == request.TeacherId
                             && c.ChannelType == channel
                             && c.IsActive);

            if (channelEntity is null)
                return Result<ManualSendSummaryDto>.Failure(_localizer, "ChannelNotActiveOrConfigured");

            var students = await _unitOfWork.Students
                .GetActiveByIdsAndTeacherAsync(request.TeacherId, request.StudentIds);

            var orderedBlocks = template.Blocks.OrderBy(b => b.SortOrder).ToList();
            var summary = new ManualSendSummaryDto
            {
                Channels = new List<string> { channel.ToString() }
            };

            // Build preview from first student (REQ-MSG-028)
            var firstStudent = students.FirstOrDefault();
            if (firstStudent is not null)
            {
                var previewCtx = new MessageResolveContext
                {
                    StudentId = firstStudent.Id,
                    StudentName = firstStudent.StudentName ?? string.Empty,
                    StudentCode = firstStudent.StudentCode ?? string.Empty,
                };
                summary.PreviewContent = _resolver.Resolve(orderedBlocks, previewCtx);
            }

            foreach (var student in students)
            {
                var ctx = new MessageResolveContext
                {
                    StudentId = student.Id,
                    StudentName = student.StudentName ?? string.Empty,
                    StudentCode = student.StudentCode ?? string.Empty,
                    Date = DateTime.UtcNow,
                };

                var resolved = _resolver.Resolve(orderedBlocks, ctx);

                // ── Student phone ──────────────────────────────────────
                if (recipientTarget is RecipientTarget.Student or RecipientTarget.Both)
                {
                    if (string.IsNullOrWhiteSpace(student.StudentPhoneNumber))
                    {
                        summary.SkippedNoPhone++;
                        await _logService.LogMissingPhoneAsync(
                                               request.TeacherId,
                                                  student.Id,
                                                  student.StudentName,
                                                 student.StudentCode,
                                                 RecipientTarget.Student,
                                                 template.Id,
                                                 channel
                                             );
                    }
                    else
                    {
                        EnqueueOrSchedule(new MessageSendPayload
                        {
                            TeacherId = request.TeacherId,
                            StudentId = student.Id,
                            StudentName = ctx.StudentName,
                            RecipientPhone = student.StudentPhoneNumber,
                            RecipientType = RecipientTarget.Student,
                            ResolvedContent = resolved,
                            Channel = channel,
                            MessageTemplateId = template.Id,
                            IsManual = true,
                            ScheduledSendAt = request.ScheduledSendAt
                        });

                        summary.StudentCount++;
                    }
                }
                // ── Parent phone ───────────────────────────────────────
                if (recipientTarget is RecipientTarget.Parent or RecipientTarget.Both)
                {
                    if (string.IsNullOrWhiteSpace(student.ParentPhoneNumber))
                    {
                        summary.SkippedNoPhone++;


                        await _logService.LogMissingPhoneAsync(
                          request.TeacherId,
                             student.Id,
                             student.StudentName,
                             student.StudentCode,
                            RecipientTarget.Student,
                            template.Id,
                            channel
                            
                          
                        );
                    }
                    else
                    {
                        EnqueueOrSchedule(new MessageSendPayload
                        {
                            TeacherId = request.TeacherId,
                            StudentId = student.Id,
                            StudentName = ctx.StudentName,
                            RecipientPhone = student.ParentPhoneNumber,
                            RecipientType = RecipientTarget.Parent,
                            ResolvedContent = resolved,
                            Channel = channel,
                            MessageTemplateId = template.Id,
                            IsManual = true,
                            ScheduledSendAt = request.ScheduledSendAt
                        });

                        summary.ParentCount++;
                    }
                }
            }

            summary.TotalRecipients = summary.StudentCount + summary.ParentCount;

            return Result<ManualSendSummaryDto>.Success(summary, _localizer);
        }

        // ── Helpers ───────────────────────────────────────────────────────

        /// <summary>
        /// Immediate → BackgroundJob.Enqueue (fires right away — REQ-MSG-034)
        /// Scheduled → BackgroundJob.Schedule (fires at the configured time — REQ-MSG-024)
        /// </summary>
        public static void EnqueueOrSchedule(MessageSendPayload payload)
        {
            if (payload.ScheduledSendAt is null || payload.ScheduledSendAt <= DateTime.UtcNow)
            {
                BackgroundJob.Enqueue<IMessageSenderJob>(job => job.SendAsync(payload));
            }
            else
            {
                var delay = payload.ScheduledSendAt.Value - DateTime.UtcNow;
                BackgroundJob.Schedule<IMessageSenderJob>(job => job.SendAsync(payload), delay);
            }
        }

        /// <summary>
        /// Builds the absolute DateTime to send at, based on trigger timing config.
        /// Returns null for Immediate triggers.
        /// </summary>
        private static DateTime? BuildScheduledTime(SendTimingType timing, TimeSpan? scheduledTime)
        {
            if (timing == SendTimingType.Immediate || scheduledTime is null)
                return null;

            // Fire at the configured clock time today (or tomorrow if already past)
            var todayAt = DateTime.UtcNow.Date.Add(scheduledTime.Value);
            return todayAt > DateTime.UtcNow ? todayAt : todayAt.AddDays(1);
        }

        private async Task LogMissingPhoneAsync(
            long teacherId, long studentId, RecipientTarget target,
            long templateId, TriggerEventType eventType)
        {
            var log = new MessageLog
            {
                TeacherId = teacherId,
                StudentId = studentId,
                StudentName = string.Empty,
                RecipientPhone = string.Empty,
                RecipientType = target,
                MessageTemplateId = templateId,
                ResolvedContent = string.Empty,
                Channel = ChannelType.SMS,
                Status = MessageStatus.Failed,
                FailureReason = "MissingPhoneNumber",   // REQ-MSG-009
                TriggerType = eventType,
                IsManual = false,
                SentAt = DateTime.UtcNow,
                CreateAt = DateTime.UtcNow,
            };

            await _unitOfWork.GetRepository<MessageLog, long>().AddAsync(log);
            await _unitOfWork.SaveChangesAsync();
        }



       
    }
}
