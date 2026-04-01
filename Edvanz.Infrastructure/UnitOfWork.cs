using Edvanz.Domain.Interfaces;
using Edvanz.Infrastructure.Persistence;
using Edvanz.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Edvanz.Infrastructure
{
    public class UnitOfWork : IUnitOfWork
    {

        private IDbContextTransaction? _transaction;

        private readonly ConcurrentDictionary<string, object> _Repositories = new ConcurrentDictionary<string, object>();
        private readonly EdvanzDbContext _Context;
        private IUserRepo? _userRepo;
        // Student Module repo (Module 1: teacher-scoped student records)
        private ITeacherStudentRepo? _teacherStudentRepo;
        // Session Module repo (Module 2: sessions, groups, links)
        private ISessionRepo? _sessionRepo;
        // Attendance Module repo (Module 3: attendance records, occurrences, counters)
        private IAttendanceRepo? _attendanceRepo;
        private IUserPermissionRepo? _userPermissionRepo;
        private IRefreshTokenRepo? _refreshTokenRepo;
        private  IgoogleUserRepo? _googleUserRepo;

        public UnitOfWork(EdvanzDbContext _context)
        {
            _Context = _context;
        }
        //--------------------------------------------------------------------------------------
        public IGenericRepo<T, Tkey> GetRepository<T, Tkey>()
            where T : class
            where Tkey : IEquatable<Tkey>
        {
            // Check If The Repository Already Exists In The Dictionary Or Add New Repository
            return (IGenericRepo<T, Tkey>)_Repositories.GetOrAdd(typeof(T).Name, new GenericRepo<T, Tkey>(_Context));
        }
        //--------------------------------------------------------------------------------------
        public async Task<int> SaveChangesAsync() => await _Context.SaveChangesAsync();
        public async ValueTask DisposeAsync() => await _Context.DisposeAsync();
        //--------------------------------------------------------------------------------------

        /// <inheritdoc />
        public bool HasActiveTransaction => _transaction is not null;

        //--------------------------------------------------------------------------------------

        public async Task BeginTransactionAsync()
        {
            _transaction = await _Context.Database.BeginTransactionAsync();
        }
        public async Task<IDbContextTransaction> BeginTransactionAsyncM()
        {
            return await _Context.Database.BeginTransactionAsync();
        }

        /// <summary>
        /// FIX BUG-1: Commits the current transaction and clears the reference.
        /// Previously _transaction was NOT set to null after commit, causing
        /// HasActiveTransaction to stay true for the rest of the HTTP request scope.
        /// This caused subsequent service calls to skip their own transaction management,
        /// potentially leaving writes without transactional safety.
        /// </summary>
        public async Task CommitAsync()
        {
            if (_transaction != null)
            {
                await _transaction.CommitAsync();
                // FIX BUG-1: Clear reference so HasActiveTransaction returns false
                await _transaction.DisposeAsync();
                _transaction = null;
            }
        }

        /// <summary>
        /// FIX BUG-1: Rolls back the current transaction and clears the reference.
        /// Same root cause as CommitAsync — stale _transaction reference prevented
        /// proper transaction lifecycle management for subsequent operations.
        /// </summary>
        public async Task RollbackAsync()
        {
            if (_transaction != null)
            {
                await _transaction.RollbackAsync();
                // FIX BUG-1: Clear reference so HasActiveTransaction returns false
                await _transaction.DisposeAsync();
                _transaction = null;
            }
        }

        //public async Task LogError(Exception ex)
        //{
        //    string logDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs");
        //    string logFile = Path.Combine(logDir, "errors.log");
        //    string zipFile = Path.Combine(logDir, "errors.zip");
        //    string password = "123456789";

        //    if (!Directory.Exists(logDir))
        //        Directory.CreateDirectory(logDir);

        //    using (var writer = File.AppendText(logFile))
        //    {
        //        await writer.WriteLineAsync("----- ERROR -----");
        //        await writer.WriteLineAsync(DateTime.Now.ToString());
        //        await writer.WriteLineAsync(ex.Message);
        //        await writer.WriteLineAsync(ex.StackTrace ?? "");
        //        await writer.WriteLineAsync("-----------------");
        //        await writer.WriteLineAsync();
        //    }

        //    using (var zipStream = new ZipOutputStream(File.Create(zipFile)))
        //    {
        //        zipStream.SetLevel(9);
        //        zipStream.Password = password;

        //        ZipEntry entry = new ZipEntry(Path.GetFileName(logFile));
        //        entry.DateTime = DateTime.Now;

        //        zipStream.PutNextEntry(entry);

        //        byte[] buffer = File.ReadAllBytes(logFile);
        //        zipStream.Write(buffer, 0, buffer.Length);

        //        zipStream.Finish();
        //        zipStream.Close();
        //    }
        //}

        /// <summary>
        /// User module ecosystem repo (User, Teacher, StudentUser, ParentUser, linking).
        /// </summary>
        public IUserRepo Users
     => _userRepo ??= new UserRepo(_Context);

        /// <summary>
        /// Student Module repo (Module 1: teacher-scoped student CRUD, search, filter, recycle bin).
        /// </summary>
        public ITeacherStudentRepo Students
     => _teacherStudentRepo ??= new TeacherStudentRepo(_Context);

        /// <summary>
        /// Session Module repo (Module 2: sessions, groups, membership links).
        /// </summary>
        public ISessionRepo SessionsRepo
     => _sessionRepo ??= new SessionRepo(_Context);

        /// <summary>
        /// Attendance Module repo (Module 3: attendance records, occurrences, assignments, counters, edit logs).
        /// </summary>
        public IAttendanceRepo AttendanceRepo
     => _attendanceRepo ??= new AttendanceRepo(_Context);

        public IUserPermissionRepo UsersPermissions => _userPermissionRepo ??= new UserPermissionRepo(_Context);

        public IRefreshTokenRepo RefreshTokenRepo => _refreshTokenRepo ??= new RefreshTokenRepo(_Context);

        public IgoogleUserRepo googleUserRepo => _googleUserRepo ??= new GoogleUserRepo(_Context);
    }
}