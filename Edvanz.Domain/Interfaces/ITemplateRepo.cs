using Edvanz.Domain.Entities;

namespace Edvanz.Domain.Interfaces
{
    public interface ITemplateRepo : IGenericRepo<Template, long>
    {
        public Task<IReadOnlyList<Template>> GetAllTemplatesPerTeacherAsyns(long TeacherId);

        public Task<bool> ValidateTemplateNamePerTeacher(long teacherId, string profileName, long? excludeTemplateId);
    }
}
