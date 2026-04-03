using Edvanz.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Text;

namespace Edvanz.Domain.Interfaces
{
    public interface IModuleTeacherRepo:IGenericRepo<TemplatePermissionsUsers,(long,long)>
    {
        public  Task<List<Module>> GetModulesPerTeacher(long teacherId);

    }
}
