using Edvanz.Application.Dtos;
using Edvanz.Application.Dtos.AssistantDtos;
using Edvanz.Application.Dtos.ModulesPermissions;
using System;
using System.Collections.Generic;
using System.Text;

namespace Edvanz.Application.IservicesContract
{
    public interface IPermissionService
    {
        public Task<Result<List<ModulePermissionsDto>>> GetAvailableTeacherPermissionCatalogue(long teacherId);
        public Task<Result<string>> UpdateAssistantPermissionsAsync(UpdateAssistantPermissionsRequest dto);


    }
}
