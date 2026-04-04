using Edvanz.Domain.Entities;
using Edvanz.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Text;

namespace Edvanz.Domain.Interfaces
{
 
        public interface IAssitantRepo:IGenericRepo<Assistant,long>
        {
           public Task<(IReadOnlyList<Assistant> , int)> GetListAssistantsPerTeacher(long? teacherId, bool? isAcitve, string? fullName, string? username,bool? isAssignedToTeacher, AssistantSortBy? sortby,SortDirection? sortDirection, int page, int pageSize);
        public Task<Assistant?> GetAssistantWithPermissionsAsync(long id);
        public Task<Assistant?> GetAssistantWithUserIdAsync(long id);

    }
}
