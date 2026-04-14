using Edvanz.Application.Dtos.DispatcherDtos;
using Edvanz.Application.IservicesContract;
using Edvanz.Domain.Entities.Messaging;
using Edvanz.Domain.Enums;
using Edvanz.Domain.Interfaces;
using Hangfire;
using System;
using System.Collections.Generic;
using System.Text;

namespace Edvanz.Application.Services
{
    public class MessageSenderJob : IMessageSenderJob
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IWhatsAppSender _whatsApp;
        private readonly ISmsSender _sms;
        private readonly IEncryptionService _encryption;

        public MessageSenderJob(
            IUnitOfWork unitOfWork,
            IWhatsAppSender whatsApp,
            ISmsSender sms,
            IEncryptionService encryption)
        {
            _unitOfWork = unitOfWork;
            _whatsApp = whatsApp;
            _sms = sms;
            _encryption = encryption;
        }

        // Hangfire بيستدعي الـ method دي — لو فشلت هيعمل retry تلقائياً
        [AutomaticRetry(Attempts = 3, DelaysInSeconds = new[] { 60, 300, 900 })]
        public async Task SendAsync(MessageSendPayload payload)
        {
            // 1. جيب الـ channel
            var channel = await _unitOfWork.GetRepository<MessagingChannel, long>()
                .FindAsync(c => c.TeacherId == payload.TeacherId
                             && c.ChannelType == payload.Channel
                             && c.IsActive);

            bool success;
            string? failureReason = null;

            if (channel is null)
            {
                success = false;
                failureReason = "ChannelNotConfigured";
            }
            else
            {
                var decryptedCreds = _encryption.Decrypt(channel.EncryptedCredentials!);

                (success, failureReason) = payload.Channel switch
                {
                    ChannelType.WhatsApp => await _whatsApp.SendAsync(
                        payload.RecipientPhone, payload.ResolvedContent,
                        decryptedCreds, channel.SenderIdOrNumber!),

                    ChannelType.SMS => await _sms.SendAsync(
                        payload.RecipientPhone, payload.ResolvedContent,
                        decryptedCreds, channel.SenderIdOrNumber!),

                    _ => (false, "UnknownChannel")
                };
            }

            // 2. اكتب في الـ MessageLog
            var log = new MessageLog
            {
                TeacherId = payload.TeacherId,
                StudentId = payload.StudentId,
                StudentCode= payload.StudentCode,
                StudentName = payload.StudentName,
                RecipientPhone = payload.RecipientPhone,
                RecipientType = payload.RecipientType,
                MessageTemplateId = payload.MessageTemplateId,
                ResolvedContent = payload.ResolvedContent,
                Channel = payload.Channel,
                Status = success ? MessageStatus.Delivered : MessageStatus.Failed,
                FailureReason = failureReason,
                TriggerType = payload.TriggerType,
                IsManual = payload.IsManual,
                SentAt = DateTime.UtcNow,
                DeliveredAt = success ? DateTime.UtcNow : null,
                CreateAt = DateTime.UtcNow,
            };

            await _unitOfWork.GetRepository<MessageLog, long>().AddAsync(log);
            await _unitOfWork.SaveChangesAsync();

            // 3. لو فشل → Hangfire هيعمل retry تلقائياً
            if (!success)
                throw new Exception($"Message send failed: {failureReason}");
        }
    }

}
