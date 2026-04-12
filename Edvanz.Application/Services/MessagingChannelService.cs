using Edvanz.Application.Dtos;
using Edvanz.Application.Dtos.ChannelDtos;
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
    public class MessagingChannelService : IMessagingChannelService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IStringLocalizer<Messages> _localizer;
        private readonly IEncryptionService _encryption;

        public MessagingChannelService(
            IUnitOfWork unitOfWork,
            IStringLocalizer<Messages> localizer,
            IEncryptionService encryption)
        {
            _unitOfWork = unitOfWork;
            _localizer = localizer;
            _encryption = encryption;
        }

        // ── GET ALL ──
        public async Task<Result<List<ChannelStatusDto>>> GetAllChannelsAsync(long teacherId)
        {
            var channels = await _unitOfWork.messagingChannelRepo.GetByTeacherId(teacherId);

            var result = channels
                .Select(MapToDto)
                .ToList();

            return Result<List<ChannelStatusDto>>.Success(result, _localizer);
        }

        // ── GET ONE ──
        public async Task<Result<ChannelStatusDto>> GetChannelAsync(long teacherId, ChannelType type)
        {
            var channel = await _unitOfWork.messagingChannelRepo.GetByTeacherIdAndChannelType(teacherId, type);

            if (channel is null)
                return Result<ChannelStatusDto>.Failure(_localizer, "ChannelNotConfigured");

            return Result<ChannelStatusDto>.Success(MapToDto(channel), _localizer);
        }

        // ── UPSERT ────
        // Creates the channel row if not exists, updates it if it does.
        public async Task<Result<string>> UpsertChannelAsync(long teacherId, UpsertChannelDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.SenderIdOrNumber))
                return Result<string>.Failure(_localizer, "SenderIdRequired");

            if (string.IsNullOrWhiteSpace(dto.Credentials))
                return Result<string>.Failure(_localizer, "CredentialsRequired");

            var existing = await _unitOfWork.messagingChannelRepo.GetByTeacherIdAndChannelType( teacherId , dto.ChannelType);

            // Encrypt credentials before storage 
            var encryptedCreds = _encryption.Encrypt(dto.Credentials);

            if (existing is null)
            {
                var newChannel = new MessagingChannel
                {
                    TeacherId = teacherId,
                    ChannelType = dto.ChannelType,
                    SenderIdOrNumber = dto.SenderIdOrNumber.Trim(),
                    EncryptedCredentials = encryptedCreds,
                    Status = ChannelStatus.Connected,
                    IsActive = true,
                    ConnectedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    CreateAt = DateTime.UtcNow,
                };

                await _unitOfWork.GetRepository<MessagingChannel, long>().AddAsync(newChannel);
            }
            else
            {
                existing.SenderIdOrNumber = dto.SenderIdOrNumber.Trim();
                existing.EncryptedCredentials = encryptedCreds;
                existing.Status = ChannelStatus.Connected;
                existing.IsActive = true;
                existing.ConnectedAt = DateTime.UtcNow;
                existing.UpdatedAt = DateTime.UtcNow;

                await _unitOfWork.GetRepository<MessagingChannel, long>().UpdateAsync(existing);
            }

            await _unitOfWork.SaveChangesAsync();
            return Result<string>.Success("ChannelConfigured", _localizer);
        }

        // ── TOGGLE ACTIVE  ─────────────────
        public async Task<Result<string>> ToggleChannelAsync(long teacherId, ChannelType type, bool activate)
        {
            var channel = await _unitOfWork.messagingChannelRepo.GetByTeacherIdAndChannelType(teacherId , type);

            if (channel is null)
                return Result<string>.Failure(_localizer, "ChannelNotConfigured");

            if (channel.IsActive == activate)
                return Result<string>.Failure(_localizer,
                    activate ? "ChannelAlreadyActive" : "ChannelAlreadyInactive");

            channel.IsActive = activate;
            channel.UpdatedAt = DateTime.UtcNow;

            await _unitOfWork.GetRepository<MessagingChannel, long>().UpdateAsync(channel);
            await _unitOfWork.SaveChangesAsync();

            return Result<string>.Success(
                activate ? "ChannelActivated" : "ChannelDeactivated", _localizer);
        }

        // ── DISCONNECT ────────────────────────────────────────────────
        public async Task<Result<string>> DisconnectChannelAsync(long teacherId, ChannelType type)
        {
            var channel = await _unitOfWork.messagingChannelRepo.GetByTeacherIdAndChannelType(teacherId , type);

            if (channel is null)
                return Result<string>.Failure(_localizer, "ChannelNotConfigured");

            channel.Status = ChannelStatus.Disconnected;
            channel.IsActive = false;
            channel.EncryptedCredentials = null;
            channel.UpdatedAt = DateTime.UtcNow;

            await _unitOfWork.GetRepository<MessagingChannel, long>().UpdateAsync(channel);
            await _unitOfWork.SaveChangesAsync();

            return Result<string>.Success("ChannelDisconnected", _localizer);
        }

        // ── Mapping ───────────────────────────────────────────────────
        private static ChannelStatusDto MapToDto(MessagingChannel c) => new()
        {
            Id = c.Id,
            ChannelType = c.ChannelType,
            Status = c.Status,
            SenderIdOrNumber = c.SenderIdOrNumber,
            IsActive = c.IsActive,
            ConnectedAt = c.ConnectedAt,
            // EncryptedCredentials never returned — REQ-MSG-NFR-004
        };
    }
}
