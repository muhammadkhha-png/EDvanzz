using Edvanz.Domain.Entities.Messaging;
using Edvanz.Domain.Enums;
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
        Task<(IReadOnlyList<AutomatedTrigger> triggers, int count)> GetFilteredAsync(long teacherId, TriggerEventType? eventType,
        TriggerScope? scope, bool? isActive,
        int? pageSize, int? page);
    }
}
