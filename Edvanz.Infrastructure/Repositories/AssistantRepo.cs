using Edvanz.Domain.Entities;
using Edvanz.Domain.Enums;
using Edvanz.Domain.Interfaces;
using Edvanz.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Text;

namespace Edvanz.Infrastructure.Repositories
{
    public class AssistantRepo : GenericRepo<Assistant, long>, IAssitantRepo
    {
        public AssistantRepo(EdvanzDbContext context) : base(context)
        {
        }

        public async Task<(IReadOnlyList<Assistant> , int )> GetListAssistantsPerTeacher(
      long? teacherId,
      bool? isActive,
      string? fullName,
      string? username,
      bool? isAssignedToTeacher,
      AssistantSortBy? sortBy = null,
      SortDirection? sortDirection = null,
      int page = 1,
      int pageSize = 20)
        {
            // Default sorting
            var sort = sortBy ?? AssistantSortBy.CreatedAt;
            var direction = sortDirection ?? SortDirection.Desc;

            // Base query with includes
            IQueryable<Assistant> query = _context.Set<Assistant>()
                .Include(a => a.User)
                .Include(a => a.Teacher).ThenInclude(t=>t.User);

            // ── FILTERS ──
            if (teacherId.HasValue)
                query = query.Where(a => a.TeacherAccountId == teacherId.Value);

            if (isActive.HasValue)
                query = query.Where(a => (bool)(isActive.Value ? a.User.IsActive : !a.User.IsActive));

            if (!string.IsNullOrWhiteSpace(fullName))
            {
                var nameTerm = fullName.Trim().ToLower();
                query = query.Where(a => a.User.FullName.ToLower().Contains(nameTerm));
            }

            if (!string.IsNullOrWhiteSpace(username))
            {
                var usernameTerm = username.Trim().ToLower();
                query = query.Where(a => a.User.Username.ToLower().Contains(usernameTerm));
            }

            if (isAssignedToTeacher.HasValue && teacherId.HasValue)
                query = isAssignedToTeacher.Value
                    ? query.Where(a => a.TeacherAccountId == teacherId.Value)
                    : query.Where(a => a.TeacherAccountId != teacherId.Value);
            var totalCount = await query.CountAsync();
            // ── SORT ──
            query = sort switch
            {
                AssistantSortBy.fullName => direction == SortDirection.Asc
                    ? query.OrderBy(a => a.User.FullName)
                    : query.OrderByDescending(a => a.User.FullName),

                // Default: CreatedAt
                _ => direction == SortDirection.Asc
                    ? query.OrderBy(a => a.CreateAt)
                    : query.OrderByDescending(a => a.CreateAt)
            };

            // ── PAGINATION ──
            query = query.Skip((page - 1) * pageSize).Take(pageSize);

            // ── EXECUTE ──
            var list = await query.AsNoTracking().ToListAsync();

            return (list, totalCount); ;
        }
        public override async Task<Assistant?> GetByIdAsync(long id)
        {
           return await _context.Assistants.Include(a => a.User)
                .Include(a => a.Teacher).ThenInclude(t => t.User).FirstOrDefaultAsync(a=>a.Id==id);
        }

        public async Task<Assistant?> GetAssistantWithPermissionsAsync(long id)
        {
            return await _context.Assistants
                .Include(a => a.User)
                    .ThenInclude(u => u.Permissions)
                        .ThenInclude(up => up.Permission)
                            .ThenInclude(p => p.module).Include(a => a.PermissionProfiles)
                .Include(a => a.Teacher)
                    .ThenInclude(t => t.User)
                .FirstOrDefaultAsync(a => a.Id == id);
        }

        public Task<Assistant?> GetAssistantWithUserIdAsync(long userId)
        {
            return _context.Assistants
                .FirstOrDefaultAsync(a => a.UserId == userId);
        }
    }
}