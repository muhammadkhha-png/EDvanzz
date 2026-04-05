using Edvanz.Domain.Entities;


namespace Edvanz.Domain.Interfaces
{
    public interface ITemplatePermissionRepo :IGenericRepo<TemplatePermisions,(long,long)>
    {
        public Task<IReadOnlyList<TemplatePermisions>> GetTemplatePermissions(long templateID);
        public  Task<IReadOnlyList<TemplatePermisions>> GetTemplatePermissionsByIds(List<long> templateIds);

    }
}
