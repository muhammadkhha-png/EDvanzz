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

        /// <summary>
        /// Extended repository for the User module ecosystem (User, Teacher, StudentUser, ParentUser, linking).
        /// </summary>
        IUserRepo Users { get; }
        IUserPermissionRepo UsersPermissions { get; }
        IRefreshTokenRepo RefreshTokenRepo { get; }
        IgoogleUserRepo googleUserRepo { get; }



        /// <summary>
        /// Extended repository for the Student Module (Module 1: teacher-scoped student records).
        /// Handles CRUD, search, filter, recycle bin, code generation, and bulk operations
        /// for TeacherStudent records.
        /// 
        /// IMPORTANT: This is separate from IUserRepo. The Student Module manages
        /// teacher-owned student DATA records, not StudentUser accounts.
        /// </summary>
        ITeacherStudentRepo Students { get; }

        /// <summary>
        /// Extended repository for the Session Module (Module 2: sessions, groups, links).
        /// Handles CRUD, search, filter, group management, and membership linking
        /// for Session, SessionGroup, and SessionLink records.
        /// </summary>
        ISessionRepo SessionsRepo { get; }
        IAssitantRepo AssistantRepo { get; }

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