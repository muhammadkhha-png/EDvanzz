using Edvanz.Domain.Interfaces;
using Edvanz.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace Edvanz.Infrastructure.Repositories
{
    /// <summary>
    /// Generic repository implementation using EF Core.
    /// All EF Core dependencies are isolated here in the Infrastructure layer.
    /// </summary>
    public class GenericRepo<T, Tkey> : IGenericRepo<T, Tkey> where T : class where Tkey : IEquatable<Tkey>
    {
        protected readonly EdvanzDbContext _context;
        public GenericRepo(EdvanzDbContext context) => _context = context;
        //----------------------------------------------------------------
        public async Task<IReadOnlyList<T>> GetAllAsync()
        {
            return await _context.Set<T>().AsNoTracking().ToListAsync();
        }
        //----------------------------------------------------------------
        public IQueryable<T> GetAllAsQueryable()
        {
            return _context.Set<T>().AsNoTracking().AsQueryable();
        }
        //----------------------------------------------------------------
        public async Task<T?> GetByIdAsync(int id)
        {
            return await _context.Set<T>().FindAsync(id);
        }
        //----------------------------------------------------------------
        public async Task<IReadOnlyList<T>> GetAsync(Expression<Func<T, bool>> predicate)
        {
            return await _context.Set<T>().AsNoTracking().Where(predicate).ToListAsync();
        }
        //----------------------------------------------------------------
        public async Task AddAsync(T entity)
        {
            await _context.Set<T>().AddAsync(entity);
        }
        //----------------------------------------------------------------
        public async Task AddRangeAsync(IEnumerable<T> entities)
        {
            await _context.Set<T>().AddRangeAsync(entities);
        }
        //----------------------------------------------------------------
        public async Task UpdateAsync(T entity)
        {
            _context.Entry(entity).State = EntityState.Modified;
        }
        //----------------------------------------------------------------
        public async Task DeleteAsync(T entity)
        {
            _context.Set<T>().Remove(entity);
        }
        //----------------------------------------------------------------
        public IQueryable<T> GetQueryable()
        {
            return _context.Set<T>().AsQueryable();
        }
        //----------------------------------------------------------------
        public async Task<T?> FindAsync(Expression<Func<T, bool>> predicate)
        {
            return await _context.Set<T>().FirstOrDefaultAsync(predicate);
        }
        //----------------------------------------------------------------
        public async Task<bool> AnyAsync(Expression<Func<T, bool>> predicate)
        {
            return await _context.Set<T>().AnyAsync(predicate);
        }
        //----------------------------------------------------------------
        public async Task DeleteRangeAsync(IEnumerable<T> entities)
        {
            _context.Set<T>().RemoveRange(entities);
        }
        //----------------------------------------------------------------
        /// <inheritdoc />
        public async Task<int> CountAsync(Expression<Func<T, bool>>? predicate = null)
        {
            if (predicate is null)
                return await _context.Set<T>().CountAsync();

            return await _context.Set<T>().CountAsync(predicate);
        }
        //----------------------------------------------------------------
        /// <inheritdoc />
        public async Task<int> CountAsync(IQueryable<T> query)
        {
            return await query.CountAsync();
        }
        //----------------------------------------------------------------
        /// <inheritdoc />
        public async Task<IReadOnlyList<T>> GetPagedAsync(IQueryable<T> query, int page, int pageSize)
        {
            return await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .AsNoTracking()
                .ToListAsync();
        }
        //----------------------------------------------------------------
    }
}