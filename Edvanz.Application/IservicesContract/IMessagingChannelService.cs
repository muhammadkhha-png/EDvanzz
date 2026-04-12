using Edvanz.Application.Dtos;
using Edvanz.Application.Dtos.ChannelDtos;
using Edvanz.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Text;

namespace Edvanz.Application.IservicesContract
{
    public interface IMessagingChannelService
    {
        Task<Result<List<ChannelStatusDto>>> GetAllChannelsAsync(long teacherId);
        Task<Result<ChannelStatusDto>> GetChannelAsync(long teacherId, ChannelType type);
        Task<Result<string>> UpsertChannelAsync(long teacherId, UpsertChannelDto dto);
        Task<Result<string>> ToggleChannelAsync(long teacherId, ChannelType type, bool activate);
        Task<Result<string>> DisconnectChannelAsync(long teacherId, ChannelType type);
    }
}
