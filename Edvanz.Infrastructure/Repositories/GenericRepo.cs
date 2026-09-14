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
    /// All methods are async per project convention to maintain consistency
    /// across the entire codebase and support future async refactoring.
    /// </summary>
    public class GenericRepo<T, Tkey> : IGenericRepo<T, Tkey> where T : class where Tkey : IEquatable<Tkey>
    {
        protected readonly EdvanzDbContext _context;
        public GenericRepo(EdvanzDbContext context) => _context = context;
        //----------------------------------------------------------------
        public  async Task<IReadOnlyList<T>> GetAllAsync()
        {
            return await _context.Set<T>().AsNoTracking().ToListAsync();
        }
        //----------------------------------------------------------------
        public IQueryable<T> GetAllAsQueryable()
        {
            return _context.Set<T>().AsNoTracking().AsQueryable();
        }
        //----------------------------------------------------------------
        public virtual async Task<T?> GetByIdAsync(long id)
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
        /// <summary>
        /// FIX BUG-2: EF Core's Entry().State is synchronous — no async EF API exists.
        /// We keep async Task signature per project convention but properly await
        /// Task.CompletedTask to suppress CS1998 and maintain the all-async contract.
        /// </summary>
        public async Task UpdateAsync(T entity)
        {
            _context.Entry(entity).State = EntityState.Modified;
            await Task.CompletedTask;
        }
        //----------------------------------------------------------------
        /// <summary>
        /// FIX BUG-2: EF Core's Remove() is synchronous — no async EF API exists.
        /// Same pattern as UpdateAsync: await Task.CompletedTask for all-async contract.
        /// </summary>
        public async Task DeleteAsync(T entity)
        {
            RemoveWithoutAttachingGraph(entity);
            await Task.CompletedTask;
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
        /// <summary>
        /// FIX BUG-2: RemoveRange is synchronous — await Task.CompletedTask for async contract.
        /// </summary>
        public async Task DeleteRangeAsync(IEnumerable<T> entities)
        {
            foreach (var entity in entities)
                RemoveWithoutAttachingGraph(entity);
            await Task.CompletedTask;
        }

        //----------------------------------------------------------------
        /// <summary>
        /// Marks one entity Deleted, safely, whether or not the context is already tracking it.
        ///
        /// <para>An entity that is ALREADY tracked is removed exactly as before — no behaviour
        /// change for the overwhelmingly common case.</para>
        ///
        /// <para>A DETACHED entity (one that came back from an <c>AsNoTracking</c> query) is the
        /// dangerous case. <c>Remove</c>/<c>RemoveRange</c> attach it by walking its whole navigation
        /// graph, so an eager-loaded parent gets attached too — and if the request already tracks
        /// that parent, EF throws <c>"another instance with the same key value is already being
        /// tracked"</c>. That is a 500 with no way for the caller to see it coming. It took down
        /// every student move involving a per-class session: the period rows carried an included
        /// <c>Session</c>, and the move had already loaded the destination session.</para>
        ///
        /// <para>So for a detached entity we first look for an instance with the same key that the
        /// context is already tracking and delete THAT one; failing that we set the state directly,
        /// which tracks this row alone and never touches its navigations.</para>
        /// </summary>
        private void RemoveWithoutAttachingGraph(T entity)
        {
            var entry = _context.Entry(entity);

            if (entry.State != EntityState.Detached)
            {
                _context.Set<T>().Remove(entity);
                return;
            }

            var primaryKey = entry.Metadata.FindPrimaryKey();
            if (primaryKey is not null)
            {
                var keyValues = primaryKey.Properties
                    .Select(property => entry.Property(property.Name).CurrentValue)
                    .ToArray();

                var tracked = _context.ChangeTracker.Entries<T>()
                    .FirstOrDefault(candidate => candidate.State != EntityState.Detached
                        && HasSameKey(candidate, primaryKey, keyValues));

                if (tracked is not null)
                {
                    if (tracked.State != EntityState.Deleted)
                        tracked.State = EntityState.Deleted;
                    return;
                }
            }

            // Setting the state attaches THIS row only — RemoveRange would walk the graph.
            entry.State = EntityState.Deleted;
        }

        //----------------------------------------------------------------
        /// <summary>True when a tracked entry carries the same primary-key values.</summary>
        private static bool HasSameKey(
            Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry<T> candidate,
            Microsoft.EntityFrameworkCore.Metadata.IKey primaryKey,
            object?[] keyValues)
        {
            for (int i = 0; i < primaryKey.Properties.Count; i++)
            {
                var current = candidate.Property(primaryKey.Properties[i].Name).CurrentValue;
                if (!Equals(current, keyValues[i]))
                    return false;
            }
            return true;
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
        public IQueryable<T> GetAllTracked()
        {
            return _context.Set<T>().AsQueryable(); // without AsNoTracking to can make changes to elements
        }
    }
}