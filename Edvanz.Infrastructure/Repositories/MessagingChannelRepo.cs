using Edvanz.Domain.Entities.Messaging;
using Edvanz.Domain.Enums;
using Edvanz.Domain.Interfaces;
using Edvanz.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Text;

namespace Edvanz.Infrastructure.Repositories
{
    public class MessagingChannelRepo : GenericRepo<MessagingChannel, long>, IMessagingChannelRepo
    {
        public MessagingChannelRepo(EdvanzDbContext context) : base(context)
        {
        }
       

        public async Task<IReadOnlyList<MessagingChannel>> GetByTeacherId(long teacherId)
        {
           return await _context.MessagingChannels.AsNoTracking()
                .Where(c => c.TeacherId == teacherId)
                .ToListAsync();
        }
      
        public async Task<MessagingChannel?> GetByTeacherIdAndChannelType(long teacherId, ChannelType channelType)
        {
            return await _context.MessagingChannels.AsNoTracking()
                .FirstOrDefaultAsync(c => c.TeacherId == teacherId && c.ChannelType == channelType);
        }
    }
}
