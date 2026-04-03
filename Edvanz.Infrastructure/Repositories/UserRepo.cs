using Edvanz.Domain.Entities;
using Edvanz.Domain.Enums;
using Edvanz.Domain.Interfaces;
using Edvanz.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Edvanz.Infrastructure.Repositories
{
    /// <summary>
    /// Extended repository for the User module ecosystem.
    /// Centralizes all domain-specific query logic used by Teacher, Student, and Parent services.
    /// 
    /// ARCHITECTURAL NOTE:
    /// This repo encapsulates ALL expression-based queries so the Application layer
    /// never builds raw predicates. If a query needs to change, you edit it HERE —
    /// not in every service that uses it.
    /// 
    /// Inherits from GenericRepo&lt;User, long&gt; for basic User CRUD,
    /// and adds named methods for every entity in the User module ecosystem.
    /// 
    /// FIX BUG-2: All synchronous EF Core operations (Entry().State, Remove, RemoveRange)
    /// now use 'await Task.CompletedTask' to maintain the project's all-async convention
    /// and suppress CS1998 compiler warnings.
    /// 
    /// FIX DB-1: Added batch loading methods to eliminate N+1 query patterns in
    /// dashboard builders. These load all related data in bulk instead of per-entity loops.
    /// </summary>
    public class UserRepo : GenericRepo<User, long>, IUserRepo
    {
        public UserRepo(EdvanzDbContext context) : base(context)
        {
        }

        // ══════════════════════════════════════════════
        // USER ENTITY QUERIES
        // ══════════════════════════════════════════════

        /// <inheritdoc />
        public async Task<User?> GetByPhoneAsync(string phone)
        {
            return await _context.Users
                .FirstOrDefaultAsync(u => u.PhoneNumber == phone);
        }

        /// <inheritdoc />
        // FIX B1: Previously queried PhoneNumber instead of Username — now correctly queries Username
        public async Task<User?> GetByUserName(string userName)
        {
            return await _context.Users
                .FirstOrDefaultAsync(u => u.Username == userName && u.IsActive==true);
        }

        /// <inheritdoc />
        public async Task<User?> GetByEmail(string email)
        {
            return await _context.Users
                .FirstOrDefaultAsync(u => u.Email == email);
        }

        /// <inheritdoc />
        public async Task<User?> GetByIdAndTypeAsync(long userId, UserType userType)
        {
            return await _context.Users
                .FirstOrDefaultAsync(u => u.Id == userId && u.UserType == userType);
        }

        /// <inheritdoc />
        public async Task<User?> GetUserByIdAsync(long userId)
        {
            return await _context.Users
                .FirstOrDefaultAsync(u => u.Id == userId);
        }

        /// <inheritdoc />
        public async Task<IReadOnlyList<User>> GetAllUsersAsync()
        {
            return await _context.Users
                .AsNoTracking()
                .ToListAsync();
        }

        /// <inheritdoc />
        // FIX V1: Encapsulates the complex OR-based duplicate check that was previously
        // a raw expression in UserService.AddUser. Now the Application layer calls this
        // named method instead of building the predicate itself.
        public async Task<User?> FindExistingUserByCredentialsAsync(string phoneNumber, string username, string? email)
        {
            return await _context.Users
                .FirstOrDefaultAsync(u =>
                    u.PhoneNumber == phoneNumber ||
                    u.Username == username ||
                    (!string.IsNullOrEmpty(email) && u.Email == email));
        }

        // ══════════════════════════════════════════════
        // TEACHER ENTITY QUERIES
        // ══════════════════════════════════════════════

        /// <inheritdoc />
        public async Task<Teacher?> GetTeacherByIdAsync(long teacherId)
        {
            return await _context.Set<Teacher>()
                .FirstOrDefaultAsync(t => t.Id == teacherId);
        }

        /// <inheritdoc />
        public async Task<Teacher?> GetActiveTeacherByIdAsync(long teacherId)
        {
            return await _context.Set<Teacher>()
                .FirstOrDefaultAsync(t => t.Id == teacherId && t.DeletedAt == null &&t.StudentCapacityPackageId != null  );
        }

        /// <inheritdoc />
        public async Task<bool> TeacherExistsByUserIdAsync(long userId)
        {
            return await _context.Set<Teacher>()
                .AnyAsync(t => t.UserId == userId);
        }

        /// <inheritdoc />
        public async Task<Teacher?> GetActiveTeacherByCodeAsync(string teacherCode)
        {
            return await _context.Set<Teacher>()
                .FirstOrDefaultAsync(t =>
                    t.TeacherCode == teacherCode &&
                    t.AccountStatus == AccountStatus.Active &&
                    t.DeletedAt == null);
        }

        /// <inheritdoc />
        public async Task<IReadOnlyList<Teacher>> GetAllTeachersAsync()
        {
            return await _context.Set<Teacher>()
                .AsNoTracking()
                .ToListAsync();
        }

        /// <inheritdoc />
        public async Task AddTeacherAsync(Teacher teacher)
        {
            await _context.Set<Teacher>().AddAsync(teacher);
        }

        /// <inheritdoc />
        // FIX BUG-2: Entry().State is synchronous — await Task.CompletedTask for async contract
        public async Task UpdateTeacherAsync(Teacher teacher)
        {
            _context.Entry(teacher).State = EntityState.Modified;
            await Task.CompletedTask;
        }

        // ══════════════════════════════════════════════
        // TEACHER SUBJECT QUERIES
        // ══════════════════════════════════════════════

        /// <inheritdoc />
        public async Task<IReadOnlyList<TeacherSubject>> GetTeacherSubjectsByTeacherIdAsync(long teacherId)
        {
            return await _context.Set<TeacherSubject>()
                .AsNoTracking()
                .Where(ts => ts.TeacherId == teacherId)
                .ToListAsync();
        }

        /// <inheritdoc />
        public async Task<IReadOnlyList<TeacherSubject>> GetAllTeacherSubjectsAsync()
        {
            return await _context.Set<TeacherSubject>()
                .AsNoTracking()
                .ToListAsync();
        }

        /// <inheritdoc />
        public async Task AddTeacherSubjectAsync(TeacherSubject teacherSubject)
        {
            await _context.Set<TeacherSubject>().AddAsync(teacherSubject);
        }

        /// <inheritdoc />
        // FIX BUG-2: RemoveRange is synchronous — await Task.CompletedTask for async contract
        public async Task DeleteTeacherSubjectsAsync(IEnumerable<TeacherSubject> subjects)
        {
            _context.Set<TeacherSubject>().RemoveRange(subjects);
            await Task.CompletedTask;
        }

        // ══════════════════════════════════════════════
        // SUBJECT QUERIES
        // ══════════════════════════════════════════════

        /// <inheritdoc />
        public async Task<Subject?> GetSubjectByIdAsync(long subjectId)
        {
            return await _context.Set<Subject>()
                .FirstOrDefaultAsync(s => s.Id == subjectId);
        }

        /// <inheritdoc />
        public async Task<bool> SubjectExistsAndActiveAsync(long subjectId)
        {
            return await _context.Set<Subject>()
                .AnyAsync(s => s.Id == subjectId && s.IsActive);
        }

        /// <inheritdoc />
        public async Task<IReadOnlyList<Subject>> GetActiveSubjectsAsync()
        {
            return await _context.Set<Subject>()
                .AsNoTracking()
                .Where(s => s.IsActive)
                .ToListAsync();
        }

        /// <inheritdoc />
        public async Task<IReadOnlyList<Subject>> GetAllSubjectsAsync()
        {
            return await _context.Set<Subject>()
                .AsNoTracking()
                .ToListAsync();
        }

        // ══════════════════════════════════════════════
        // TEACHER CONFIGURATION QUERIES
        // ══════════════════════════════════════════════

        /// <inheritdoc />
        public async Task<TeacherConfiguration?> GetConfigurationByTeacherIdAsync(long teacherId)
        {
            return await _context.Set<TeacherConfiguration>()
                .FirstOrDefaultAsync(c => c.TeacherId == teacherId);
        }

        /// <inheritdoc />
        public async Task AddConfigurationAsync(TeacherConfiguration configuration)
        {
            await _context.Set<TeacherConfiguration>().AddAsync(configuration);
        }

        /// <inheritdoc />
        // FIX BUG-2: Entry().State is synchronous — await Task.CompletedTask for async contract
        public async Task UpdateConfigurationAsync(TeacherConfiguration configuration)
        {
            _context.Entry(configuration).State = EntityState.Modified;
            await Task.CompletedTask;
        }

        // ══════════════════════════════════════════════
        // TEACHER PRORATED TIER QUERIES
        // ══════════════════════════════════════════════

        /// <inheritdoc />
        public async Task<IReadOnlyList<TeacherProratedTier>> GetProratedTiersByConfigIdAsync(long configurationId)
        {
            return await _context.Set<TeacherProratedTier>()
                .AsNoTracking()
                .Where(pt => pt.TeacherConfigurationId == configurationId)
                .ToListAsync();
        }

        /// <inheritdoc />
        public async Task AddProratedTiersAsync(IEnumerable<TeacherProratedTier> tiers)
        {
            await _context.Set<TeacherProratedTier>().AddRangeAsync(tiers);
        }

        /// <inheritdoc />
        public async Task AddProratedTierAsync(TeacherProratedTier tier)
        {
            await _context.Set<TeacherProratedTier>().AddAsync(tier);
        }

        /// <inheritdoc />
        // FIX BUG-2: RemoveRange is synchronous — await Task.CompletedTask for async contract
        public async Task DeleteProratedTiersAsync(IEnumerable<TeacherProratedTier> tiers)
        {
            _context.Set<TeacherProratedTier>().RemoveRange(tiers);
            await Task.CompletedTask;
        }

        // ══════════════════════════════════════════════
        // TEACHER SUBSCRIPTION QUERIES
        // ══════════════════════════════════════════════

        /// <inheritdoc />
        public async Task<IReadOnlyList<TeacherSubscription>> GetActiveSubscriptionsByTeacherIdAsync(long teacherId)
        {
            return await _context.Set<TeacherSubscription>()
                .AsNoTracking()
                .Where(s => s.TeacherId == teacherId &&
                    (s.SubscriptionStatus == SubscriptionStatus.Active ||
                     s.SubscriptionStatus == SubscriptionStatus.ExpiringSoon))
                .ToListAsync();
        }

        /// <inheritdoc />
        public async Task<IReadOnlyList<TeacherSubscription>> GetAllSubscriptionsAsync()
        {
            return await _context.Set<TeacherSubscription>()
                .AsNoTracking()
                .ToListAsync();
        }

        // ══════════════════════════════════════════════
        // STUDENT CAPACITY PACKAGE QUERIES
        // ══════════════════════════════════════════════

        /// <inheritdoc />
        public async Task<IReadOnlyList<StudentCapacityPackage>> GetActiveCapacityPackagesAsync()
        {
            return await _context.Set<StudentCapacityPackage>()
                .AsNoTracking()
                .Where(p => p.IsActive)
                .ToListAsync();
        }

        /// <inheritdoc />
        public async Task<StudentCapacityPackage?> GetActiveCapacityPackageByIdAsync(long packageId)
        {
            return await _context.Set<StudentCapacityPackage>()
                .FirstOrDefaultAsync(p => p.Id == packageId && p.IsActive);
        }

        /// <inheritdoc />
        public async Task<StudentCapacityPackage?> GetCapacityPackageByIdAsync(long packageId)
        {
            return await _context.Set<StudentCapacityPackage>()
                .FirstOrDefaultAsync(p => p.Id == packageId);
        }

        // ══════════════════════════════════════════════
        // STUDENT USER ENTITY QUERIES
        // ══════════════════════════════════════════════

        /// <inheritdoc />
        public async Task<StudentUser?> GetActiveStudentUserByIdAsync(long studentUserId)
        {
            return await _context.Set<StudentUser>()
                .FirstOrDefaultAsync(s => s.Id == studentUserId && s.DeletedAt == null);
        }

        /// <inheritdoc />
        public async Task<bool> StudentUserExistsByUserIdAsync(long userId)
        {
            return await _context.Set<StudentUser>()
                .AnyAsync(s => s.UserId == userId);
        }

        /// <inheritdoc />
        public async Task<StudentUser?> GetStudentUserByAccountCodeAsync(string accountCode)
        {
            string normalizedCode = accountCode.Trim().ToUpperInvariant();
            return await _context.Set<StudentUser>()
                .FirstOrDefaultAsync(s => s.StudentAccountCode == normalizedCode && s.DeletedAt == null);
        }

        /// <inheritdoc />
        public async Task<StudentUser?> GetStudentUserByIdAsync(long studentUserId)
        {
            return await _context.Set<StudentUser>()
                .FirstOrDefaultAsync(s => s.Id == studentUserId);
        }

        /// <inheritdoc />
        public async Task AddStudentUserAsync(StudentUser studentUser)
        {
            await _context.Set<StudentUser>().AddAsync(studentUser);
        }

        /// <inheritdoc />
        // FIX BUG-2: Entry().State is synchronous — await Task.CompletedTask for async contract
        public async Task UpdateStudentUserAsync(StudentUser studentUser)
        {
            _context.Entry(studentUser).State = EntityState.Modified;
            await Task.CompletedTask;
        }

        // ══════════════════════════════════════════════
        // STUDENT TEACHER LINK QUERIES
        // ══════════════════════════════════════════════

        /// <inheritdoc />
        public async Task<int> CountActiveStudentTeacherLinksAsync(long studentUserId)
        {
            return await _context.Set<StudentTeacherLink>()
                .CountAsync(l => l.StudentUserId == studentUserId && l.LinkStatus == LinkStatus.Active);
        }

        /// <inheritdoc />
        public async Task<IReadOnlyList<StudentTeacherLink>> GetActiveStudentTeacherLinksAsync(long studentUserId)
        {
            return await _context.Set<StudentTeacherLink>()
                .AsNoTracking()
                .Where(l => l.StudentUserId == studentUserId && l.LinkStatus == LinkStatus.Active)
                .ToListAsync();
        }

        /// <inheritdoc />
        public async Task<StudentTeacherLink?> GetActiveStudentTeacherLinkAsync(long studentUserId, long teacherId)
        {
            return await _context.Set<StudentTeacherLink>()
                .FirstOrDefaultAsync(l =>
                    l.StudentUserId == studentUserId &&
                    l.TeacherId == teacherId &&
                    l.LinkStatus == LinkStatus.Active);
        }

        /// <inheritdoc />
        public async Task<bool> StudentTeacherLinkExistsAsync(long studentUserId, long teacherId)
        {
            return await _context.Set<StudentTeacherLink>()
                .AnyAsync(l =>
                    l.StudentUserId == studentUserId &&
                    l.TeacherId == teacherId &&
                    l.LinkStatus == LinkStatus.Active);
        }

        /// <inheritdoc />
        public async Task AddStudentTeacherLinkAsync(StudentTeacherLink link)
        {
            await _context.Set<StudentTeacherLink>().AddAsync(link);
        }

        /// <inheritdoc />
        // FIX BUG-2: Entry().State is synchronous — await Task.CompletedTask for async contract
        public async Task UpdateStudentTeacherLinkAsync(StudentTeacherLink link)
        {
            _context.Entry(link).State = EntityState.Modified;
            await Task.CompletedTask;
        }

        // ══════════════════════════════════════════════
        // TEACHER STUDENT (TEACHER-SCOPED RECORD) QUERIES
        // ══════════════════════════════════════════════

        /// <inheritdoc />
        public async Task<TeacherStudent?> GetTeacherStudentByLinkingCredentialsAsync(
            long teacherId, string studentCode, string hashedToken)
        {
            return await _context.Set<TeacherStudent>()
                .FirstOrDefaultAsync(ts =>
                    ts.TeacherId == teacherId &&
                    ts.StudentCode == studentCode &&
                    ts.HashedToken == hashedToken &&
                    !ts.IsDeleted);
        }

        // ══════════════════════════════════════════════
        // PARENT USER ENTITY QUERIES
        // ══════════════════════════════════════════════

        /// <inheritdoc />
        public async Task<ParentUser?> GetActiveParentUserByIdAsync(long parentUserId)
        {
            return await _context.Set<ParentUser>()
                .FirstOrDefaultAsync(p => p.Id == parentUserId && p.DeletedAt == null);
        }

        /// <inheritdoc />
        public async Task<bool> ParentUserExistsByUserIdAsync(long userId)
        {
            return await _context.Set<ParentUser>()
                .AnyAsync(p => p.UserId == userId);
        }

        /// <inheritdoc />
        public async Task AddParentUserAsync(ParentUser parentUser)
        {
            await _context.Set<ParentUser>().AddAsync(parentUser);
        }

        /// <inheritdoc />
        // FIX BUG-2: Entry().State is synchronous — await Task.CompletedTask for async contract
        public async Task UpdateParentUserAsync(ParentUser parentUser)
        {
            _context.Entry(parentUser).State = EntityState.Modified;
            await Task.CompletedTask;
        }

        // ══════════════════════════════════════════════
        // PARENT CHILD QUERIES
        // ══════════════════════════════════════════════

        /// <inheritdoc />
        public async Task<int> CountActiveChildrenAsync(long parentUserId)
        {
            return await _context.Set<ParentChild>()
                .CountAsync(c => c.ParentUserId == parentUserId && c.IsActive);
        }

        /// <inheritdoc />
        public async Task<IReadOnlyList<ParentChild>> GetActiveChildrenAsync(long parentUserId)
        {
            return await _context.Set<ParentChild>()
                .AsNoTracking()
                .Where(c => c.ParentUserId == parentUserId && c.IsActive)
                .ToListAsync();
        }

        /// <inheritdoc />
        public async Task<ParentChild?> GetActiveChildAsync(long parentUserId, long childId)
        {
            return await _context.Set<ParentChild>()
                .FirstOrDefaultAsync(c =>
                    c.Id == childId &&
                    c.ParentUserId == parentUserId &&
                    c.IsActive);
        }

        /// <inheritdoc />
        public async Task<bool> ChildAlreadyLinkedAsync(long parentUserId, long studentUserId)
        {
            return await _context.Set<ParentChild>()
                .AnyAsync(c =>
                    c.ParentUserId == parentUserId &&
                    c.StudentUserId == studentUserId &&
                    c.IsActive);
        }

        /// <inheritdoc />
        public async Task AddParentChildAsync(ParentChild parentChild)
        {
            await _context.Set<ParentChild>().AddAsync(parentChild);
        }

        /// <inheritdoc />
        // FIX BUG-2: Entry().State is synchronous — await Task.CompletedTask for async contract
        public async Task UpdateParentChildAsync(ParentChild parentChild)
        {
            _context.Entry(parentChild).State = EntityState.Modified;
            await Task.CompletedTask;
        }

        // ══════════════════════════════════════════════
        // PARENT CHILD TEACHER LINK QUERIES
        // ══════════════════════════════════════════════

        /// <inheritdoc />
        public async Task<IReadOnlyList<ParentChildTeacherLink>> GetActiveParentChildTeacherLinksAsync(long parentChildId)
        {
            return await _context.Set<ParentChildTeacherLink>()
                .AsNoTracking()
                .Where(l => l.ParentChildId == parentChildId && l.LinkStatus == LinkStatus.Active)
                .ToListAsync();
        }

        /// <inheritdoc />
        public async Task<bool> ParentChildTeacherLinkExistsAsync(long parentChildId, long teacherId)
        {
            return await _context.Set<ParentChildTeacherLink>()
                .AnyAsync(l =>
                    l.ParentChildId == parentChildId &&
                    l.TeacherId == teacherId &&
                    l.LinkStatus == LinkStatus.Active);
        }

        /// <inheritdoc />
        public async Task<ParentChildTeacherLink?> GetActiveParentChildTeacherLinkAsync(long parentChildId, long teacherId)
        {
            return await _context.Set<ParentChildTeacherLink>()
                .FirstOrDefaultAsync(l =>
                    l.ParentChildId == parentChildId &&
                    l.TeacherId == teacherId &&
                    l.LinkStatus == LinkStatus.Active);
        }

        /// <inheritdoc />
        public async Task AddParentChildTeacherLinkAsync(ParentChildTeacherLink link)
        {
            await _context.Set<ParentChildTeacherLink>().AddAsync(link);
        }

        /// <inheritdoc />
        // FIX BUG-2: Entry().State is synchronous — await Task.CompletedTask for async contract
        public async Task UpdateParentChildTeacherLinkAsync(ParentChildTeacherLink link)
        {
            _context.Entry(link).State = EntityState.Modified;
            await Task.CompletedTask;
        }

        // ══════════════════════════════════════════════
        // BATCH LOADING METHODS (FIX DB-1: N+1 elimination)
        // ══════════════════════════════════════════════

        /// <inheritdoc />
        /// <summary>
        /// FIX DB-1: Loads all data needed for student dashboard teacher rendering in bulk.
        /// Previously each teacher in the loop triggered 4 separate DB calls (teacher user,
        /// teacher subjects, first subject name, configuration) — totaling 4×N round-trips.
        /// Now loads everything for a set of teacher IDs in 4 queries total regardless of N.
        /// </summary>
        public async Task<TeacherDashboardBatchData> GetTeacherDashboardDataAsync(IReadOnlyList<long> teacherIds)
        {
            if (!teacherIds.Any())
                return new TeacherDashboardBatchData();

            var distinctIds = teacherIds.Distinct().ToList();

            // 1. Bulk load all teachers
            var teachers = await _context.Set<Teacher>()
                .AsNoTracking()
                .Where(t => distinctIds.Contains(t.Id) && t.DeletedAt == null)
                .ToListAsync();

            // 2. Bulk load all user records for these teachers
            var userIds = teachers.Select(t => t.UserId).Distinct().ToList();
            var users = await _context.Users
                .AsNoTracking()
                .Where(u => userIds.Contains(u.Id))
                .ToListAsync();

            // 3. Bulk load all teacher-subject associations and their subjects
            var teacherSubjects = await _context.Set<TeacherSubject>()
                .AsNoTracking()
                .Where(ts => distinctIds.Contains(ts.TeacherId))
                .ToListAsync();

            var subjectIds = teacherSubjects.Select(ts => ts.SubjectId).Distinct().ToList();
            var subjects = await _context.Set<Subject>()
                .AsNoTracking()
                .Where(s => subjectIds.Contains(s.Id))
                .ToListAsync();

            // 4. Bulk load all configurations for these teachers
            var configs = await _context.Set<TeacherConfiguration>()
                .AsNoTracking()
                .Where(c => distinctIds.Contains(c.TeacherId))
                .ToListAsync();

            return new TeacherDashboardBatchData
            {
                Teachers = teachers.ToDictionary(t => t.Id),
                Users = users.ToDictionary(u => u.Id),
                TeacherSubjects = teacherSubjects.GroupBy(ts => ts.TeacherId)
                    .ToDictionary(g => g.Key, g => (IReadOnlyList<TeacherSubject>)g.ToList()),
                Subjects = subjects.ToDictionary(s => s.Id),
                Configurations = configs.ToDictionary(c => c.TeacherId)
            };
        }
    }
}