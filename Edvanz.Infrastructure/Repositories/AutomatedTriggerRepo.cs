using Edvanz.Application.Dtos.TriggerDtos;
using Edvanz.Domain.Entities.Messaging;
using Edvanz.Domain.Enums;
using Edvanz.Domain.Interfaces;
using Edvanz.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Text;

namespace Edvanz.Infrastructure.Repositories
{
    public class AutomatedTriggerRepo : GenericRepo<AutomatedTrigger, long>,IAutomatedTriggerRepo
    {
        public AutomatedTriggerRepo(EdvanzDbContext context) : base(context)
        {
        }

        /// <inheritdoc />
        public async Task<int> CountByTeacherAsync(long teacherId)
        {
            return await _context.AutomatedTriggers.CountAsync(t => t.TeacherId == teacherId);
        }

        public Task<AutomatedTrigger?> GetByTeacherIdAndTriggerId(long teacherId, long triggerId)
        {
          return _context.AutomatedTriggers.AsNoTracking().FirstOrDefaultAsync(t=>t.TeacherId==teacherId && t.Id==triggerId);
        }
       
        public async Task<IReadOnlyList<AutomatedTrigger>> GetByTeacherIdAsync(long teacherId)
        {
            return  await _context.AutomatedTriggers.AsNoTracking().Where(t=>t.TeacherId==teacherId).ToListAsync();
        }
        public async Task<IEnumerable<AutomatedTrigger>> GetActiveByTeacherAndEvent(
     long teacherId,
     string eventType)
        {
            return await _context.AutomatedTriggers.AsNoTracking()
                .Where(t =>
                    t.TeacherId == teacherId &&
                    t.EventType.ToString() == eventType &&
                    t.IsActive)
                .ToListAsync();
        }


        public async Task<(IReadOnlyList<AutomatedTrigger> triggers, int count)>
     GetFilteredAsync(long teacherId, TriggerEventType? eventType , TriggerScope? scope, bool? isActive,int? pageSize, int? page)
        {
            var query = _context.AutomatedTriggers
                .AsNoTracking()
                .Where(t => t.TeacherId == teacherId);

            // 🔹 Optional filters
            if (eventType.HasValue)
                query = query.Where(t => t.EventType == eventType.Value);

            if (scope.HasValue)
                query = query.Where(t => t.Scope == scope.Value);

            if ( isActive.HasValue)
                query = query.Where(t => t.IsActive ==  isActive.Value);

            var count = await query.CountAsync();

            if ( page.HasValue &&  pageSize.HasValue)
            {
                var skip = ( page.Value - 1) *  pageSize.Value;

                query = query
                    .OrderBy(t => t.Id) 
                    .Skip(skip)
                    .Take( pageSize.Value);
            }

            var data = await query.ToListAsync();

            return (data, count);
        }
    }
}
