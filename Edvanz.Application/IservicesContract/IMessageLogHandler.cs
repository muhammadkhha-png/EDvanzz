using Edvanz.Application.Dtos.DispatcherDtos;
using System;
using System.Collections.Generic;
using System.Text;

namespace Edvanz.Application.IservicesContract
{
    public interface IMessageLogHandler
    {
       Task SaveAsync(MessageSendPayload payload, bool success, string? error);
    }
}
