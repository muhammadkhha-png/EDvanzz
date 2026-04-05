using Edvanz.Domain.Entities;
using Edvanz.Domain.Interfaces;
using Edvanz.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Edvanz.Infrastructure.Repositories
{
    public class TemplateRepo : GenericRepo<Template, long>, ITemplateRepo
    {
        public TemplateRepo(EdvanzDbContext context) : base(context)
        {
        }


        public async Task<IReadOnlyList<Template>> GetAllTemplatesPerTeacherAsyns(long TeacherId)
        {
            return await _context.Templates.Include(t => t.Profiles).ThenInclude(t => t.permision).ThenInclude(t=>t.module).Where(t => t.TeacherId == TeacherId).ToListAsync();
        }
        public override async Task<Template?> GetByIdAsync(long id)
        {
            return await _context.Templates.Include(t => t.Profiles).ThenInclude(t => t.permision).ThenInclude(t => t.module).FirstOrDefaultAsync(t => t.Id == id);
        }

        public async Task<bool> ValidateTemplateNamePerTeacher(
            long teacherId,
            string profileName,
            long? excludeTemplateId = null)
        {
            return await _context.Templates
                .AnyAsync(t =>
                    t.TeacherId == teacherId &&
                    t.Name == profileName.Trim() &&
                    (excludeTemplateId == null || t.Id != excludeTemplateId));
        }
    }
}
