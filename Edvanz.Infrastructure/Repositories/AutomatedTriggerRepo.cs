using Edvanz.Domain.Entities.Messaging;
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
    }
}
