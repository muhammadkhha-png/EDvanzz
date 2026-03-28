using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Edvanz.Domain.Interfaces
{
    public interface IUnitOfWork
    {
        IGenericRepo<T, Tkey> GetRepository<T, Tkey>()
                where T : class where Tkey : IEquatable<Tkey>;
        Task<int> SaveChangesAsync();
        Task BeginTransactionAsync();
        Task RollbackAsync();
        Task CommitAsync();
        //Task LogError(Exception ex);
        IUserRepo Users { get; }

    }
}
