using Edvanz.Application.Dtos;
using Edvanz.Application.Dtos.DispatcherDtos;
using System;
using System.Collections.Generic;
using System.Text;

namespace Edvanz.Application.IservicesContract
{
    public interface IMessageDispatcher
    {
       
        // Called by any service when a trigger-eligible event occurs.
        // Finds the matching trigger, resolves content per student, and queues messages.
        Task DispatchAsync(DispatchRequest request);

        // Called by the Manual Messaging flow — sends immediately to specified recipients.
        Task<Result<ManualSendSummaryDto>> DispatchManualAsync(ManualMessageRequests request);
    }
}
