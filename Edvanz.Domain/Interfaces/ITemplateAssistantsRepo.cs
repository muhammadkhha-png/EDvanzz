using Edvanz.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Text;

namespace Edvanz.Domain.Interfaces
{
    public interface ITemplateAssistantsRepo :IGenericRepo<TemplatePermissionsUsers,(long,long)>
    {
        public Task<IReadOnlyList<TemplatePermissionsUsers>> GetAllAssistantsPerTemplateAsyns(long TemplateId);
        Task<IReadOnlyList<TemplatePermissionsUsers>> GetOtherTemplateLinksForAssistantAsync(
        List<long> userIds,
        long templateId);
    }
}
