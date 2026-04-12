using Edvanz.Application.Dtos.MessageResolver;
using Edvanz.Domain.Entities.Messaging;
using System;
using System.Collections.Generic;
using System.Text;

namespace Edvanz.Application.IservicesContract
{
    public interface IBlockResolver
    {
        
        // Takes an ordered list of blocks and a context, returns the final message string.
        string Resolve(IEnumerable<MessageBlock> blocks, MessageResolveContext context);
    }

}
