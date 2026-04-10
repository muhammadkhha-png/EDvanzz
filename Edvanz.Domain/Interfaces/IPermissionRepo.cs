using Edvanz.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Text;

namespace Edvanz.Domain.Interfaces
{
    public interface IPermissionRepo:IGenericRepo<Permission,long>
    {
        public  Task<IReadOnlyList<Permission>> GetPermissionsByModuleIdsAsync(List<long> moduleIds);
       public Task<HashSet<long>> GetPermissionIdsByModuleIdsAsync(IEnumerable<long> moduleIds);
        public Task<Permission?> GetByPermissionNameAndModuleNameAsync(string name, string moduleName);

    }
}
