using Edvanz.Domain.Entities;
using Edvanz.Domain.Interfaces;
using Edvanz.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Text;

namespace Edvanz.Infrastructure.Repositories
{
    public class TeacherModuleRepo : GenericRepo<TemplatePermissionsUsers, (long, long)>, IModuleTeacherRepo
    {
        public TeacherModuleRepo(EdvanzDbContext context) : base(context)
        {
        }

        public async Task<List<Module>> GetModulesPerTeacher(long teacherId)
        {
            return await _context.TutorModuleAccess.Where(t=>t.TutorId==teacherId)
                .Select(tpu => tpu.module).ToListAsync();

        }
    }
}
