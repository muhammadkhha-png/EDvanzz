using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace Edvanz.Domain.Interfaces
{
    /// <summary>
    /// Generic repository interface providing CRUD and query operations.
    /// All data access in the Application layer goes through this interface via IUnitOfWork.
    /// </summary>
    public interface IGenericRepo<T, Tkey> where T : class where Tkey : IEquatable<Tkey>
    {
        Task<IReadOnlyList<T>> GetAllAsync();
        Task<T?> GetByIdAsync(int id);
        Task<IReadOnlyList<T>> GetAsync(Expression<Func<T, bool>> predicate);
        Task AddAsync(T entity);
        Task AddRangeAsync(IEnumerable<T> entities);
        Task UpdateAsync(T entity);
        Task DeleteAsync(T entity);
        IQueryable<T> GetQueryable();
        Task<T?> FindAsync(Expression<Func<T, bool>> predicate);
        Task<bool> AnyAsync(Expression<Func<T, bool>> predicate);
        Task DeleteRangeAsync(IEnumerable<T> entities);

        /// <summary>
        /// Returns the count of entities matching the predicate.
        /// Used for pagination total count calculations.
        /// </summary>
        Task<int> CountAsync(Expression<Func<T, bool>>? predicate = null);

        /// <summary>
        /// Returns the count of entities from an IQueryable source.
        /// Used when the query has already been built with filters applied
        /// (e.g., by a BuildStudentListQuery method in an extended repo).
        /// Follows the same pattern as GetPagedAsync — accepts a pre-built IQueryable.
        /// </summary>
        /// <param name="query">The IQueryable with filters already applied.</param>
        Task<int> CountAsync(IQueryable<T> query);

        /// <summary>
        /// Returns a paginated list of entities from an IQueryable source.
        /// Executes the query with Skip/Take applied asynchronously.
        /// </summary>
        /// <param name="query">The IQueryable with filters and sorting already applied.</param>
        /// <param name="page">1-based page number.</param>
        /// <param name="pageSize">Number of records per page.</param>
        Task<IReadOnlyList<T>> GetPagedAsync(IQueryable<T> query, int page, int pageSize);
    }
}