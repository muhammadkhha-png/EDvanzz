using Edvanz.Application.Dtos;
using Edvanz.Application.Dtos.TriggerDtos;
using Edvanz.Domain.Entities.Messaging;
using System;
using System.Collections.Generic;
using System.Text;

namespace Edvanz.Application.IservicesContract
{

    public interface IAutomatedTriggerService
    {
        Task<Result<List<TriggerDto>>> GetAllAsync(long teacherId);
        Task<Result<TriggerDto>> GetByIdAsync(long teacherId, long triggerId);
        Task<Result<string>> CreateAsync(long teacherId, CreateTriggerDto dto);
        Task<Result<string>> UpdateAsync(long teacherId, UpdateTriggerDto dto);
        Task<Result<string>> DeleteAsync(long teacherId, long triggerId);
        Task<Result<string>> ToggleAsync(long teacherId, long triggerId, bool activate);
      
        // Called by the dispatcher to get the active trigger for a given event type + scope.
        // Returns the most specific matching trigger (Session > SessionGroup > All).
        Task<AutomatedTrigger?> GetMatchingTriggerAsync(
            long teacherId, TriggerEventType eventType, long? sessionId, long? sessionGroupId);

        Task<Result<PaginatedResponse<List<TriggerDto>>>> GetPagedAsync(TriggerFiltetrReq req);
    }
}
