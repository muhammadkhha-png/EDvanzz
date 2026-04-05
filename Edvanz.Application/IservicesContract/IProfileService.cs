using Edvanz.Application.Dtos;
using Edvanz.Application.Dtos.TemplatePermissionsDtos;
using System;
using System.Collections.Generic;
using System.Text;

namespace Edvanz.Application.IservicesContract
{
    public interface IProfileService
    {
        public Task<Result<List<ProfilePermissionDto>>> GetAllProfilesWithPermissionsPerTeacherAsync(long teacherId);
        public Task<Result<ProfilePermissionDto>> GetAllProfileWithPermissionsByIdAsync(long profileId);
        public Task<Result<string>> CreateProfileAsync(CreatePermissionsProfileDto dto);
        public Task<Result<string>> UpdateProfileAsync(UpdatePermissionsProfileDto dto);
        public  Task<Result<string>> DeleteProfileAsync(long templateId);


    }
}
