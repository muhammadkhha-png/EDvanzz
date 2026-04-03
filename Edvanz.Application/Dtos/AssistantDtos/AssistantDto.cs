using Edvanz.Application.Dtos.ModulesPermissions;
using Edvanz.Application.Dtos.TemplatePermissionsDtos;
using Edvanz.Application.Dtos.UsersPermissionsDtos;
using System;
using System.Collections.Generic;
using System.Text;

namespace Edvanz.Application.Dtos.AssistantDtos
{
    public class AssistantDto
    {
        public long id { get; set; }
        public string fullName { get; set; } = null!;
        public string username { get; set; } = null!;
        public string? email { get; set; }
        public string? phoneNumber { get; set; }
        public long teacherId { get; set; }
        public string teacherName { get; set; }
        public DateTime createdAt { get; set; }
        public string accountStatus { get; set; } = null!;
        public DateTime? deletedAt { get; set; }
        public DateTime? updatedAt { get; set; }
        public string? languagePreference { get; set; }
        public List<ModulePermissionsDto> userPermissions { get; set; }=new List<ModulePermissionsDto>();
        public List<ProfilePermissionDto> assignedPermissionsProfiles { get; set; } = new List<ProfilePermissionDto>();


    }
}
