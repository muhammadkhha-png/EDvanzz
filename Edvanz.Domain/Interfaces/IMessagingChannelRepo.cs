using Edvanz.Domain.Entities.Messaging;
using Edvanz.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Text;

namespace Edvanz.Domain.Interfaces
{
    public interface IMessagingChannelRepo:IGenericRepo<MessagingChannel,long>
    {
        public Task<IReadOnlyList<MessagingChannel>> GetByTeacherId(long teacherId);
        public Task<MessagingChannel> GetByTeacherIdAndChannelType(long teacherId, ChannelType channelType);
    }
}
