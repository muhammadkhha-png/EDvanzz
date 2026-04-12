using Edvanz.Domain.Entities.Messaging;
using System;
using System.Collections.Generic;
using System.Text;

namespace Edvanz.Domain.Interfaces
{
    public interface IAutomatedTriggerRepo:IGenericRepo<AutomatedTrigger,long>
    {
        Task<IReadOnlyList<AutomatedTrigger>> GetByTeacherIdAsync(long teacherId);
        Task<AutomatedTrigger?> GetByTeacherIdAndTriggerId(long teacherId, long triggerId);
        Task<IEnumerable<AutomatedTrigger>> GetActiveByTeacherAndEvent(
       long teacherId,
       string eventType);

    }
}
