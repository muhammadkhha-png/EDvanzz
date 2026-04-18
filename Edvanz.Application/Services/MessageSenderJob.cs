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
        private readonly IWhatsAppSender _whatsApp;
        private readonly ISmsSender _sms;
        private readonly IEncryptionService _encryption;
        private readonly IMessageLogService _logService;

        public MessageSenderJob(
            IWhatsAppSender whatsApp,
            ISmsSender sms,
            IEncryptionService encryption,
            IMessageLogService logService)
        {
            _whatsApp = whatsApp;
            _sms = sms;
            _encryption = encryption;
            _logService = logService;
        }

        public async Task SendAsync(MessageSendPayload payload)
        {
            var creds = _encryption.Decrypt(payload.EncryptedCredentials);

            var (success, error) = payload.Channel switch
            {
                ChannelType.WhatsApp =>
                    await _whatsApp.SendAsync(payload.RecipientPhone, payload.ResolvedContent, creds, ""),

                ChannelType.SMS =>
                    await _sms.SendAsync(payload.RecipientPhone, payload.ResolvedContent, creds, ""),

                _ => (false, "UnknownChannel")
            };

            // 🔥 THIS WAS MISSING (critical)
            await _logService.SaveAsync(payload, success, error);

            if (!success)
                throw new Exception(error);
        }
    }

}
