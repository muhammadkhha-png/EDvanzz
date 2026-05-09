using Edvanz.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Text;

namespace Edvanz.Domain.Interfaces
{
    public interface IModuleTeacherRepo:IGenericRepo<TemplatePermissionsUsers,(long,long)>
    {
        public  Task<List<Module>> GetModulesPerTeacher(long teacherId);

        /// <summary>
        /// Returns true when the named module is active for the given teacher.
        ///
        /// "Active" means a row exists in <c>TutorModuleAccess</c> tying that teacher
        /// to that module. The TutorModule entity has no IsActive flag — presence is
        /// activation, absence is deactivation. Removing the row is how a super-admin
        /// instantaneously deactivates a module per BR-ADM-010.
        ///
        /// Used by the VCM service for student and parent runtime gates that cannot
        /// rely on the JWT module claim (students and parents have no such claim).
        /// Teacher and assistant endpoints already enforce module-active at JWT issue
        /// time via <c>[ModulePermission(...)]</c>.
        /// </summary>
        /// <param name="teacherId">The teacher whose module activation is being checked.</param>
        /// <param name="moduleName">The module name as seeded in <c>Models.Name</c>
        /// (e.g. <c>VideoConstants.ModuleName</c>).</param>
        Task<bool> IsModuleActiveAsync(long teacherId, string moduleName);

    }
}
