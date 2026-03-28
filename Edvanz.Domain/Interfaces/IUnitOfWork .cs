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

        /// <summary>
        /// Returns true if a database transaction is currently active.
        /// 
        /// Used by type-specific services (Teacher, Student, Parent) to detect
        /// whether they are being called inside an outer transaction (e.g., from
        /// the User module's registration flow). When true, the service should
        /// NOT call BeginTransactionAsync/CommitAsync/RollbackAsync — the caller
        /// owns the transaction lifecycle.
        /// 
        /// This prevents nested transaction issues and ensures that if the
        /// type-specific initialization fails, the entire registration
        /// (including the Users row) is rolled back atomically.
        /// </summary>
        bool HasActiveTransaction { get; }

    }
}
