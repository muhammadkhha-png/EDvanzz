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
        // ════════════════════════════════════════════════════════════════════════════
        // EXTENSION TO EXISTING TeacherModuleRepo (Edvanz.Infrastructure.Repositories)
        // ════════════════════════════════════════════════════════════════════════════
        //
        // Splice the following method body into the EXISTING TeacherModuleRepo class
        // alongside GetModulesPerTeacher. Do NOT replace the file — just add this one
        // method.
        //
        // The corresponding interface declaration was added to IModuleTeacherRepo in
        // Phase 3.
        // ════════════════════════════════════════════════════════════════════════════

        /// <inheritdoc />
        public async Task<bool> IsModuleActiveAsync(long teacherId, string moduleName)
        {
            // BR-ADM-010 instantaneous deactivation gate for student/parent endpoints
            // that have no `module` claim in their JWT. Single indexed lookup against
            // TutorModuleAccess joined to Models for the name match — sub-millisecond.
            //
            // Presence of the row IS activation. Absence (because the super-admin
            // removed it) IS deactivation. There is no IsActive flag on TutorModule
            // by design (Phase 2 review confirmed this matches the existing schema).
            return await _context.TutorModuleAccess
                .AnyAsync(t => t.TutorId == teacherId
                            && t.module.Name == moduleName);
        }
    }
}
