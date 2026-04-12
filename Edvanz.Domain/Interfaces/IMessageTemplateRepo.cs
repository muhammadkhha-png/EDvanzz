using Edvanz.Domain.Entities.Messaging;
using System;
using System.Collections.Generic;
using System.Text;

namespace Edvanz.Domain.Interfaces
{
    public interface IMessageTemplateRepo:IGenericRepo<MessageTemplate,long>
    {
        public Task<MessageTemplate> GetByIdWithBlocksAsync(long id);
        public Task<(IReadOnlyList<MessageTemplate> Templates, int count)> GetAllPerTeacherAsync(long teacherId,string? templateName,int? page,int? pageSize);
        public Task<bool> NameExistsAsync(long teacherId, string name, long? excludeId = null);
        Task<IReadOnlyList<MessageTemplate>> GetByIdsAsync(List<long> ids);
        Task<MessageTemplate> GetByIdAndTeacherIdAsync(long teacherId, long templateId);

    }
}
