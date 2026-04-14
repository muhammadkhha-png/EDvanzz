using Edvanz.Application.Dtos.DispatcherDtos;
using System;
using System.Collections.Generic;
using System.Text;

namespace Edvanz.Application.IservicesContract
{
    public interface IMessageSenderJob
    {
        public Task SendAsync(MessageSendPayload payload);
    }
}
