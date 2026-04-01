using Edvanz.Domain.Entities;
using Edvanz.Domain.Enums;
using Edvanz.Domain.Interfaces;
using Edvanz.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Edvanz.Infrastructure.Repositories;

/// <summary>
/// Extended repository for the Attendance Module (Module 3).
/// Centralizes all domain-specific query logic for attendance-related entities.
///
/// ARCHITECTURAL NOTE (same rationale as UserRepo, TeacherStudentRepo, SessionRepo):
/// All expression-based queries are encapsulated here so the Application layer
/// never builds raw predicates. If a query changes, you edit it HERE —
/// not in every service that uses it.
///
/// Inherits from GenericRepo&lt;AttendanceRecord, long&gt; for basic CRUD on the
/// primary entity (AttendanceRecord).
///
/// FIX BUG-2 PATTERN: All synchronous EF Core operations use 'await Task.CompletedTask'
/// to maintain the project's all-async convention and suppress CS1998 warnings.
/// </summary>
public class AttendanceRepo : GenericRepo<AttendanceRecord, long>, IAttendanceRepo
{
    public AttendanceRepo(EdvanzDbContext context) : base(context)
    {
    }

    // ══════════════════════════════════════════════
    // SESSION OCCURRENCE QUERIES
    // ══════════════════════════════════════════════

    /// <inheritdoc />
    public async Task AddOccurrenceAsync(SessionOccurrence occurrence)
    {
        await _context.SessionOccurrences.AddAsync(occurrence);
    }

    /// <inheritdoc />
    public async Task AddOccurrencesRangeAsync(IEnumerable<SessionOccurrence> occurrences)
    {
        await _context.SessionOccurrences.AddRangeAsync(occurrences);
    }

    /// <inheritdoc />
    public async Task<SessionOccurrence?> GetOccurrenceBySessionAndDateAsync(long sessionId, DateTime date)
    {
        return await _context.SessionOccurrences
            .FirstOrDefaultAsync(o => o.SessionId == sessionId && o.OccurrenceDate == date.Date);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<SessionOccurrence>> GetOccurrencesBySessionAsync(long sessionId)
    {
        return await _context.SessionOccurrences
            .Where(o => o.SessionId == sessionId)
            .OrderBy(o => o.OccurrenceDate)
            .AsNoTracking()
            .ToListAsync();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<SessionOccurrence>> GetOccurrencesByTeacherAndDateAsync(
        long teacherId, DateTime date)
    {
        return await _context.SessionOccurrences
            .Where(o => o.TeacherId == teacherId && o.OccurrenceDate == date.Date)
            .AsNoTracking()
            .ToListAsync();
    }

    /// <inheritdoc />
    public async Task UpdateOccurrenceAsync(SessionOccurrence occurrence)
    {
        _context.Entry(occurrence).State = EntityState.Modified;
        await Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task DeleteOccurrencesBySessionAsync(long sessionId)
    {
        var occurrences = await _context.SessionOccurrences
            .Where(o => o.SessionId == sessionId)
            .ToListAsync();
        _context.SessionOccurrences.RemoveRange(occurrences);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<SessionOccurrence>> GetOccurrencesBySessionAndDateRangeAsync(
        long sessionId, DateTime startDate, DateTime endDate)
    {
        return await _context.SessionOccurrences
            .Where(o => o.SessionId == sessionId
                     && o.OccurrenceDate >= startDate.Date
                     && o.OccurrenceDate <= endDate.Date)
            .OrderBy(o => o.OccurrenceDate)
            .AsNoTracking()
            .ToListAsync();
    }

    /// <inheritdoc />
    public async Task<int> CountOccurrencesBySessionAndDateRangeAsync(
        long sessionId, DateTime startDate, DateTime endDate)
    {
        return await _context.SessionOccurrences
            .CountAsync(o => o.SessionId == sessionId
                          && o.OccurrenceDate >= startDate.Date
                          && o.OccurrenceDate <= endDate.Date);
    }

    /// <inheritdoc />
    public async Task<SessionOccurrence?> GetPreviousOccurrenceAsync(long sessionId, DateTime beforeDate)
    {
        return await _context.SessionOccurrences
            .Where(o => o.SessionId == sessionId && o.OccurrenceDate < beforeDate.Date)
            .OrderByDescending(o => o.OccurrenceDate)
            .FirstOrDefaultAsync();
    }

    /// <inheritdoc />
    public async Task<SessionOccurrence?> GetLatestOccurrenceBySessionAsync(long sessionId)
    {
        return await _context.SessionOccurrences
            .Where(o => o.SessionId == sessionId)
            .OrderByDescending(o => o.OccurrenceDate)
            .FirstOrDefaultAsync();
    }

    // ══════════════════════════════════════════════
    // STUDENT SESSION ASSIGNMENT QUERIES
    // ══════════════════════════════════════════════

    /// <inheritdoc />
    public async Task AddAssignmentAsync(StudentSessionAssignment assignment)
    {
        await _context.StudentSessionAssignments.AddAsync(assignment);
    }

    /// <inheritdoc />
    public async Task UpdateAssignmentAsync(StudentSessionAssignment assignment)
    {
        _context.Entry(assignment).State = EntityState.Modified;
        await Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task<StudentSessionAssignment?> GetActiveAssignmentAsync(long teacherStudentId)
    {
        return await _context.StudentSessionAssignments
            .FirstOrDefaultAsync(a => a.TeacherStudentId == teacherStudentId && a.IsActive);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<StudentSessionAssignment>> GetAssignmentsByStudentAsync(
        long teacherStudentId)
    {
        return await _context.StudentSessionAssignments
            .Where(a => a.TeacherStudentId == teacherStudentId)
            .OrderBy(a => a.AssignedAt)
            .AsNoTracking()
            .ToListAsync();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<StudentSessionAssignment>> GetActiveAssignmentsBySessionAsync(
        long sessionId)
    {
        return await _context.StudentSessionAssignments
            .Where(a => a.SessionId == sessionId && a.IsActive)
            .Include(a => a.TeacherStudent)
            .AsNoTracking()
            .ToListAsync();
    }

    /// <inheritdoc />
    public IQueryable<StudentSessionAssignment> BuildAssignmentListQuery(
        long teacherId,
        long? sessionId = null,
        long? sessionGroupId = null,
        string? studentName = null,
        string? studentCode = null)
    {
        var query = _context.StudentSessionAssignments
            .Where(a => a.TeacherId == teacherId && a.IsActive)
            .Include(a => a.TeacherStudent)
            .AsNoTracking()
            .AsQueryable();

        if (sessionId.HasValue)
            query = query.Where(a => a.SessionId == sessionId.Value);

        if (sessionGroupId.HasValue)
            query = query.Where(a => a.Session != null && a.Session.SessionGroupId == sessionGroupId.Value);

        if (!string.IsNullOrWhiteSpace(studentName))
        {
            string searchLower = studentName.Trim().ToLower();
            query = query.Where(a => a.TeacherStudent.StudentName.ToLower().Contains(searchLower));
        }

        if (!string.IsNullOrWhiteSpace(studentCode))
        {
            string codeLower = studentCode.Trim().ToLower();
            query = query.Where(a => a.TeacherStudent.StudentCode.ToLower().Contains(codeLower));
        }

        return query.OrderBy(a => a.TeacherStudent.StudentName);
    }

    /// <inheritdoc />
    public async Task DeactivateAssignmentsBySessionAsync(long sessionId)
    {
        var activeAssignments = await _context.StudentSessionAssignments
            .Where(a => a.SessionId == sessionId && a.IsActive)
            .ToListAsync();

        var now = DateTime.UtcNow;
        foreach (var assignment in activeAssignments)
        {
            assignment.IsActive = false;
            assignment.UnassignedAt = now;
            // Nullify SessionId so it won't fail on NO ACTION FK when session is deleted
            assignment.SessionId = null;
        }
    }

    // ══════════════════════════════════════════════
    // ATTENDANCE RECORD QUERIES
    // ══════════════════════════════════════════════

    /// <inheritdoc />
    public async Task AddAttendanceRecordAsync(AttendanceRecord record)
    {
        await _context.AttendanceRecords.AddAsync(record);
    }

    /// <inheritdoc />
    public async Task AddAttendanceRecordsRangeAsync(IEnumerable<AttendanceRecord> records)
    {
        await _context.AttendanceRecords.AddRangeAsync(records);
    }

    /// <inheritdoc />
    public async Task UpdateAttendanceRecordAsync(AttendanceRecord record)
    {
        _context.Entry(record).State = EntityState.Modified;
        await Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task DeleteAttendanceRecordAsync(AttendanceRecord record)
    {
        _context.AttendanceRecords.Remove(record);
        await Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task<AttendanceRecord?> GetAttendanceRecordByIdAsync(long recordId, long teacherId)
    {
        return await _context.AttendanceRecords
            .FirstOrDefaultAsync(r => r.Id == recordId && r.TeacherId == teacherId);
    }

    /// <inheritdoc />
    public async Task<AttendanceRecord?> GetExistingAttendanceAsync(
        long teacherStudentId, long sessionOccurrenceId)
    {
        return await _context.AttendanceRecords
            .FirstOrDefaultAsync(r => r.TeacherStudentId == teacherStudentId
                                   && r.SessionOccurrenceId == sessionOccurrenceId);
    }

    /// <inheritdoc />
    public async Task<AttendanceRecord?> GetExistingAttendanceByStudentAndDateAsync(
        long teacherStudentId, DateTime occurrenceDate, IEnumerable<long> linkedSessionIds)
    {
        var sessionIdList = linkedSessionIds.ToList();
        return await _context.AttendanceRecords
            .FirstOrDefaultAsync(r => r.TeacherStudentId == teacherStudentId
                                   && r.OccurrenceDate == occurrenceDate.Date
                                   && r.SessionId.HasValue
                                   && sessionIdList.Contains(r.SessionId.Value)
                                   && (r.Status == AttendanceStatus.Present
                                       || r.Status == AttendanceStatus.CrossSessionPresent));
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<AttendanceRecord>> GetRecordsByOccurrenceAsync(
        long sessionOccurrenceId)
    {
        return await _context.AttendanceRecords
            .Where(r => r.SessionOccurrenceId == sessionOccurrenceId)
            .Include(r => r.TeacherStudent)
            .AsNoTracking()
            .ToListAsync();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<AttendanceRecord>> GetRecordsByAssignmentAsync(
        long studentSessionAssignmentId)
    {
        return await _context.AttendanceRecords
            .Where(r => r.StudentSessionAssignmentId == studentSessionAssignmentId)
            .OrderBy(r => r.OccurrenceDate)
            .AsNoTracking()
            .ToListAsync();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<AttendanceRecord>> GetRecordsByStudentAndDateRangeAsync(
        long teacherStudentId, DateTime startDate, DateTime endDate)
    {
        return await _context.AttendanceRecords
            .Where(r => r.TeacherStudentId == teacherStudentId
                     && r.OccurrenceDate >= startDate.Date
                     && r.OccurrenceDate <= endDate.Date)
            .OrderBy(r => r.OccurrenceDate)
            .AsNoTracking()
            .ToListAsync();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<AttendanceRecord>> GetRecentRecordsByStudentAsync(
        long teacherStudentId, int count)
    {
        return await _context.AttendanceRecords
            .Where(r => r.TeacherStudentId == teacherStudentId)
            .OrderByDescending(r => r.OccurrenceDate)
            .Take(count)
            .AsNoTracking()
            .ToListAsync();
    }

    /// <inheritdoc />
    public async Task<int> CountRecordsByOccurrenceAndStatusAsync(
        long sessionOccurrenceId, AttendanceStatus status)
    {
        return await _context.AttendanceRecords
            .CountAsync(r => r.SessionOccurrenceId == sessionOccurrenceId && r.Status == status);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<AttendanceRecord>> GetRecordsBySessionAndDateRangeAsync(
        long sessionId, DateTime startDate, DateTime endDate)
    {
        return await _context.AttendanceRecords
            .Where(r => r.SessionId == sessionId
                     && r.OccurrenceDate >= startDate.Date
                     && r.OccurrenceDate <= endDate.Date)
            .OrderBy(r => r.OccurrenceDate)
            .AsNoTracking()
            .ToListAsync();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<AttendanceRecord>> GetRecordsByTeacherAndDateAsync(
        long teacherId, DateTime date)
    {
        return await _context.AttendanceRecords
            .Where(r => r.TeacherId == teacherId && r.OccurrenceDate == date.Date)
            .Include(r => r.TeacherStudent)
            .AsNoTracking()
            .ToListAsync();
    }

    /// <inheritdoc />
    public IQueryable<AttendanceRecord> BuildAttendanceReportQuery(
        long teacherId,
        long? sessionId = null,
        long? sessionGroupId = null,
        DateTime? startDate = null,
        DateTime? endDate = null,
        AttendanceStatus? status = null)
    {
        var query = _context.AttendanceRecords
            .Where(r => r.TeacherId == teacherId)
            .AsNoTracking()
            .AsQueryable();

        if (sessionId.HasValue)
            query = query.Where(r => r.SessionId == sessionId.Value);

        if (sessionGroupId.HasValue)
            query = query.Where(r => r.SessionOccurrence != null
                                  && r.SessionOccurrence.Session.SessionGroupId == sessionGroupId.Value);

        if (startDate.HasValue)
            query = query.Where(r => r.OccurrenceDate >= startDate.Value.Date);

        if (endDate.HasValue)
            query = query.Where(r => r.OccurrenceDate <= endDate.Value.Date);

        if (status.HasValue)
            query = query.Where(r => r.Status == status.Value);

        return query.OrderBy(r => r.OccurrenceDate).ThenBy(r => r.TeacherStudentId);
    }

    /// <inheritdoc />
    public async Task NullifyOccurrenceReferencesForSessionAsync(long sessionId)
    {
        // Get all occurrence Ids for this session
        var occurrenceIds = await _context.SessionOccurrences
            .Where(o => o.SessionId == sessionId)
            .Select(o => o.Id)
            .ToListAsync();

        if (occurrenceIds.Count == 0)
            return;

        // Nullify SessionOccurrenceId on all related attendance records
        var records = await _context.AttendanceRecords
            .Where(r => r.SessionOccurrenceId.HasValue && occurrenceIds.Contains(r.SessionOccurrenceId.Value))
            .ToListAsync();

        foreach (var record in records)
        {
            record.SessionOccurrenceId = null;
        }
    }

    // ══════════════════════════════════════════════
    // ATTENDANCE EDIT LOG QUERIES
    // ══════════════════════════════════════════════

    /// <inheritdoc />
    public async Task AddEditLogAsync(AttendanceEditLog editLog)
    {
        await _context.AttendanceEditLogs.AddAsync(editLog);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<AttendanceEditLog>> GetEditLogsByRecordAsync(
        long attendanceRecordId)
    {
        return await _context.AttendanceEditLogs
            .Where(el => el.AttendanceRecordId == attendanceRecordId)
            .OrderBy(el => el.EditedAt)
            .AsNoTracking()
            .ToListAsync();
    }

    // ══════════════════════════════════════════════
    // STUDENT ABSENCE COUNTER QUERIES
    // ══════════════════════════════════════════════

    /// <inheritdoc />
    public async Task AddAbsenceCounterAsync(StudentAbsenceCounter counter)
    {
        await _context.StudentAbsenceCounters.AddAsync(counter);
    }

    /// <inheritdoc />
    public async Task UpdateAbsenceCounterAsync(StudentAbsenceCounter counter)
    {
        _context.Entry(counter).State = EntityState.Modified;
        await Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task<StudentAbsenceCounter?> GetAbsenceCounterAsync(
        long teacherId, long teacherStudentId)
    {
        return await _context.StudentAbsenceCounters
            .FirstOrDefaultAsync(c => c.TeacherId == teacherId
                                   && c.TeacherStudentId == teacherStudentId);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<StudentAbsenceCounter>> GetAbsenceCountersBySessionAsync(
        long sessionId)
    {
        // Get student Ids from active assignments for this session
        var studentIds = await _context.StudentSessionAssignments
            .Where(a => a.SessionId == sessionId && a.IsActive)
            .Select(a => a.TeacherStudentId)
            .ToListAsync();

        return await _context.StudentAbsenceCounters
            .Where(c => studentIds.Contains(c.TeacherStudentId))
            .Include(c => c.TeacherStudent)
            .OrderByDescending(c => c.ConsecutiveAbsences)
            .AsNoTracking()
            .ToListAsync();
    }

    /// <inheritdoc />
    public IQueryable<StudentAbsenceCounter> BuildAbsenceOverviewQuery(
        long teacherId,
        long? sessionId = null,
        string? search = null,
        bool? missingStudentPhone = null,
        bool? missingParentPhone = null)
    {
        var query = _context.StudentAbsenceCounters
            .Where(c => c.TeacherId == teacherId)
            .Include(c => c.TeacherStudent)
            .AsNoTracking()
            .AsQueryable();

        if (sessionId.HasValue)
        {
            // Filter to students currently assigned to this session
            var studentIdsInSession = _context.StudentSessionAssignments
                .Where(a => a.SessionId == sessionId.Value && a.IsActive)
                .Select(a => a.TeacherStudentId);

            query = query.Where(c => studentIdsInSession.Contains(c.TeacherStudentId));
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            string searchLower = search.Trim().ToLower();
            query = query.Where(c =>
                c.TeacherStudent.StudentName.ToLower().Contains(searchLower)
                || c.TeacherStudent.StudentCode.ToLower().Contains(searchLower));
        }

        if (missingStudentPhone == true)
            query = query.Where(c => c.TeacherStudent.StudentPhoneNumber == null
                                  || c.TeacherStudent.StudentPhoneNumber == "");

        if (missingParentPhone == true)
            query = query.Where(c => c.TeacherStudent.ParentPhoneNumber == null
                                  || c.TeacherStudent.ParentPhoneNumber == "");

        // REQ-ATT-067: Default sort by consecutive absences descending
        return query.OrderByDescending(c => c.ConsecutiveAbsences)
                    .ThenBy(c => c.TeacherStudent.StudentName);
    }

    /// <inheritdoc />
    public async Task<int> RecalculateConsecutiveAbsencesAsync(long teacherStudentId)
    {
        // Scan recent records in reverse-chronological order until a Present is found
        var records = await _context.AttendanceRecords
            .Where(r => r.TeacherStudentId == teacherStudentId)
            .OrderByDescending(r => r.OccurrenceDate)
            .ThenByDescending(r => r.RecordedAt)
            .Select(r => r.Status)
            .ToListAsync();

        int consecutive = 0;
        foreach (var status in records)
        {
            if (status == AttendanceStatus.Absent)
                consecutive++;
            else
                break; // Any non-absent status breaks the streak
        }

        return consecutive;
    }

    /// <inheritdoc />
    public async Task DeleteAbsenceCounterAsync(StudentAbsenceCounter counter)
    {
        _context.StudentAbsenceCounters.Remove(counter);
        await Task.CompletedTask;
    }

    // ══════════════════════════════════════════════
    // PAGED ABSENCE OVERVIEW
    // ══════════════════════════════════════════════

    /// <inheritdoc />
    public async Task<int> CountAbsenceOverviewAsync(
        long teacherId,
        long? sessionId = null,
        string? search = null,
        bool? missingStudentPhone = null,
        bool? missingParentPhone = null)
    {
        var query = BuildAbsenceOverviewQuery(teacherId, sessionId, search,
            missingStudentPhone, missingParentPhone);
        return await query.CountAsync();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<StudentAbsenceCounter>> GetPagedAbsenceOverviewAsync(
        long teacherId,
        int page,
        int pageSize,
        long? sessionId = null,
        string? search = null,
        bool? missingStudentPhone = null,
        bool? missingParentPhone = null)
    {
        var query = BuildAbsenceOverviewQuery(teacherId, sessionId, search,
            missingStudentPhone, missingParentPhone);

        return await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<AttendanceRecord>> ExecuteReportQueryAsync(
        long teacherId,
        long? sessionId = null,
        long? sessionGroupId = null,
        DateTime? startDate = null,
        DateTime? endDate = null,
        AttendanceStatus? status = null,
        long? teacherStudentId = null,
        IEnumerable<long>? sessionIds = null)
    {
        var query = BuildAttendanceReportQuery(teacherId, sessionId, sessionGroupId,
            startDate, endDate, status);

        if (teacherStudentId.HasValue)
            query = query.Where(r => r.TeacherStudentId == teacherStudentId.Value);

        if (sessionIds is not null)
        {
            var idList = sessionIds.ToList();
            query = query.Where(r => r.SessionId.HasValue && idList.Contains(r.SessionId.Value));
        }

        return await query
            .Include(r => r.TeacherStudent)
            .ToListAsync();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<long>> GetDistinctStudentIdsFromAssignmentsAsync(
        long teacherId,
        long? sessionId = null,
        long? sessionGroupId = null,
        string? studentName = null,
        string? studentCode = null)
    {
        var query = BuildAssignmentListQuery(teacherId, sessionId, sessionGroupId,
            studentName, studentCode);

        return await query
            .Select(a => a.TeacherStudentId)
            .Distinct()
            .ToListAsync();
    }
}