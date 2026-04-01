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
    /// FIX 3.1: Gets the next occurrence for cross-session date remapping.
    public async Task<SessionOccurrence?> GetNextOccurrenceAsync(long sessionId, DateTime onOrAfterDate)
    {
        return await _context.SessionOccurrences
            .Where(o => o.SessionId == sessionId && o.OccurrenceDate >= onOrAfterDate.Date)
            .OrderBy(o => o.OccurrenceDate)
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

    /// <inheritdoc />
    /// FIX 6.2: Loads only dates instead of full entities for efficient duplicate checking.
    public async Task<HashSet<DateTime>> GetExistingOccurrenceDatesAsync(long sessionId)
    {
        var dates = await _context.SessionOccurrences
            .Where(o => o.SessionId == sessionId)
            .Select(o => o.OccurrenceDate)
            .ToListAsync();
        return dates.ToHashSet();
    }

    // ══════════════════════════════════════════════
    // BATCH OCCURRENCE COUNTING (FIX 2.1)
    // ══════════════════════════════════════════════

    /// <inheritdoc />
    public async Task<Dictionary<long, int>> CountRecordsByOccurrenceBatchAsync(
        IEnumerable<long> occurrenceIds)
    {
        var idList = occurrenceIds.ToList();
        if (idList.Count == 0)
            return new Dictionary<long, int>();

        return await _context.AttendanceRecords
            .Where(r => r.SessionOccurrenceId.HasValue && idList.Contains(r.SessionOccurrenceId.Value))
            .GroupBy(r => r.SessionOccurrenceId!.Value)
            .Select(g => new { OccurrenceId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.OccurrenceId, x => x.Count);
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
    public async Task DeactivateAssignmentsBySessionAsync(long sessionId)
    {
        var assignments = await _context.StudentSessionAssignments
            .Where(a => a.SessionId == sessionId && a.IsActive)
            .ToListAsync();

        foreach (var assignment in assignments)
        {
            assignment.IsActive = false;
            assignment.UnassignedAt = DateTime.UtcNow;
            assignment.SessionId = null; // Nullify before session hard-delete
        }
    }

    /// <inheritdoc />
    /// FIX 1.1: Deactivates all active assignments for a student during permanent purge.
    public async Task DeactivateAssignmentsByStudentAsync(long teacherStudentId)
    {
        var assignments = await _context.StudentSessionAssignments
            .Where(a => a.TeacherStudentId == teacherStudentId && a.IsActive)
            .ToListAsync();

        foreach (var assignment in assignments)
        {
            assignment.IsActive = false;
            assignment.UnassignedAt = DateTime.UtcNow;
        }
    }

    // ══════════════════════════════════════════════
    // BATCH ASSIGNMENT QUERIES (FIX 2.2)
    // ══════════════════════════════════════════════

    /// <inheritdoc />
    public async Task<Dictionary<long, StudentSessionAssignment>> GetActiveAssignmentsBatchAsync(
        IEnumerable<long> teacherStudentIds)
    {
        var idList = teacherStudentIds.ToList();
        if (idList.Count == 0)
            return new Dictionary<long, StudentSessionAssignment>();

        var assignments = await _context.StudentSessionAssignments
            .Where(a => idList.Contains(a.TeacherStudentId) && a.IsActive)
            .ToListAsync();

        // Use first active assignment per student (should only be one due to business rules)
        return assignments
            .GroupBy(a => a.TeacherStudentId)
            .ToDictionary(g => g.Key, g => g.First());
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
        var sessionIds = linkedSessionIds.ToList();
        return await _context.AttendanceRecords
            .FirstOrDefaultAsync(r => r.TeacherStudentId == teacherStudentId
                && r.OccurrenceDate == occurrenceDate.Date
                && r.SessionId.HasValue
                && sessionIds.Contains(r.SessionId.Value));
    }

    /// <inheritdoc />
    /// FIX 2.2: Batch duplicate check for multiple students on one occurrence.
    public async Task<HashSet<long>> GetExistingAttendanceBatchAsync(
        IEnumerable<long> teacherStudentIds, long sessionOccurrenceId)
    {
        var idList = teacherStudentIds.ToList();
        if (idList.Count == 0)
            return new HashSet<long>();

        var existingStudentIds = await _context.AttendanceRecords
            .Where(r => idList.Contains(r.TeacherStudentId)
                && r.SessionOccurrenceId == sessionOccurrenceId)
            .Select(r => r.TeacherStudentId)
            .Distinct()
            .ToListAsync();

        return existingStudentIds.ToHashSet();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<AttendanceRecord>> GetRecordsByOccurrenceAsync(
        long sessionOccurrenceId)
    {
        return await _context.AttendanceRecords
            .Where(r => r.SessionOccurrenceId == sessionOccurrenceId)
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
            .AsNoTracking()
            .ToListAsync();
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

    /// <inheritdoc />
    /// FIX 1.4: Nullifies the denormalized SessionId on AttendanceRecords before session hard-delete.
    public async Task NullifySessionIdOnRecordsForSessionAsync(long sessionId)
    {
        var records = await _context.AttendanceRecords
            .Where(r => r.SessionId == sessionId)
            .ToListAsync();

        foreach (var record in records)
        {
            record.SessionId = null;
            // SessionName and OccurrenceDate remain intact for historical display (BR-ATT-005)
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
    /// FIX 2.2: Batch counter loading for multiple students.
    public async Task<Dictionary<long, StudentAbsenceCounter>> GetAbsenceCountersBatchAsync(
        long teacherId, IEnumerable<long> teacherStudentIds)
    {
        var idList = teacherStudentIds.ToList();
        if (idList.Count == 0)
            return new Dictionary<long, StudentAbsenceCounter>();

        var counters = await _context.StudentAbsenceCounters
            .Where(c => c.TeacherId == teacherId && idList.Contains(c.TeacherStudentId))
            .ToListAsync();

        return counters.ToDictionary(c => c.TeacherStudentId);
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
    public async Task<int> RecalculateConsecutiveAbsencesAsync(long teacherStudentId)
    {
        // Get recent records in reverse chronological order
        var recentRecords = await _context.AttendanceRecords
            .Where(r => r.TeacherStudentId == teacherStudentId)
            .OrderByDescending(r => r.OccurrenceDate)
            .ThenByDescending(r => r.RecordedAt)
            .Select(r => r.Status)
            .Take(100) // Reasonable upper bound for consecutive absences
            .ToListAsync();

        int consecutive = 0;
        foreach (var status in recentRecords)
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

    /// <inheritdoc />
    /// FIX 1.1: Deletes all absence counters for a student during permanent purge.
    public async Task DeleteAbsenceCountersByStudentAsync(long teacherStudentId)
    {
        var counters = await _context.StudentAbsenceCounters
            .Where(c => c.TeacherStudentId == teacherStudentId)
            .ToListAsync();

        if (counters.Count > 0)
            _context.StudentAbsenceCounters.RemoveRange(counters);
    }

    // ══════════════════════════════════════════════
    // PRIVATE HELPER: ABSENCE OVERVIEW QUERY BUILDER
    // ══════════════════════════════════════════════

    /// <summary>
    /// Builds the filtered queryable for absence overview.
    /// Private — exposed only through CountAbsenceOverviewAsync and GetPagedAbsenceOverviewAsync
    /// to prevent IQueryable leaking to the Application layer (FIX 5.2).
    /// </summary>
    private IQueryable<StudentAbsenceCounter> BuildAbsenceOverviewQuery(
        long teacherId,
        long? sessionId = null,
        string? search = null,
        bool? missingStudentPhone = null,
        bool? missingParentPhone = null)
    {
        var query = _context.StudentAbsenceCounters
            .Where(c => c.TeacherId == teacherId)
            .Include(c => c.TeacherStudent)
            .AsQueryable();

        if (sessionId.HasValue)
        {
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
        {
            query = query.Where(c =>
                c.TeacherStudent.StudentPhoneNumber == null
                || c.TeacherStudent.StudentPhoneNumber == "");
        }

        if (missingParentPhone == true)
        {
            query = query.Where(c =>
                c.TeacherStudent.ParentPhoneNumber == null
                || c.TeacherStudent.ParentPhoneNumber == "");
        }

        return query;
    }

    /// <inheritdoc />
    public async Task<int> CountAbsenceOverviewAsync(
        long teacherId,
        long? sessionId = null,
        string? search = null,
        bool? missingStudentPhone = null,
        bool? missingParentPhone = null)
    {
        return await BuildAbsenceOverviewQuery(teacherId, sessionId, search,
            missingStudentPhone, missingParentPhone)
            .CountAsync();
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
        return await BuildAbsenceOverviewQuery(teacherId, sessionId, search,
            missingStudentPhone, missingParentPhone)
            .OrderByDescending(c => c.ConsecutiveAbsences)
            .ThenBy(c => c.TeacherStudent.StudentName)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .AsNoTracking()
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
        var query = _context.AttendanceRecords
            .Where(r => r.TeacherId == teacherId)
            .Include(r => r.TeacherStudent)
            .AsNoTracking()
            .AsQueryable();

        if (teacherStudentId.HasValue)
            query = query.Where(r => r.TeacherStudentId == teacherStudentId.Value);

        if (sessionIds is not null)
        {
            var idList = sessionIds.ToList();
            query = query.Where(r => r.SessionId.HasValue && idList.Contains(r.SessionId.Value));
        }
        else if (sessionId.HasValue)
        {
            query = query.Where(r => r.SessionId == sessionId.Value);
        }

        if (sessionGroupId.HasValue)
            query = query.Where(r => r.SessionOccurrence != null
                                  && r.SessionOccurrence.Session.SessionGroupId == sessionGroupId.Value);

        if (startDate.HasValue)
            query = query.Where(r => r.OccurrenceDate >= startDate.Value.Date);

        if (endDate.HasValue)
            query = query.Where(r => r.OccurrenceDate <= endDate.Value.Date);

        if (status.HasValue)
            query = query.Where(r => r.Status == status.Value);

        return await query
            .OrderBy(r => r.OccurrenceDate)
            .ThenBy(r => r.TeacherStudentId)
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
        var query = _context.StudentSessionAssignments
            .Where(a => a.TeacherId == teacherId)
            .Include(a => a.TeacherStudent)
            .AsQueryable();

        if (sessionId.HasValue)
            query = query.Where(a => a.SessionId == sessionId.Value);

        if (sessionGroupId.HasValue)
            query = query.Where(a => a.Session != null
                && a.Session.SessionGroupId == sessionGroupId.Value);

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

        return await query
            .Select(a => a.TeacherStudentId)
            .Distinct()
            .ToListAsync();
    }

    // ══════════════════════════════════════════════
    // PAGED ATTENDANCE STUDENT LIST (FIX 1.2)
    // ══════════════════════════════════════════════

    /// <inheritdoc />
    public async Task<(IReadOnlyList<PagedAttendanceStudentRow> Items, int TotalCount)>
        GetPagedAttendanceStudentListAsync(
            long teacherId, long sessionId, DateTime occurrenceDate,
            IEnumerable<long> linkedSessionIds,
            string? search, bool unmarkedOnly,
            int page, int pageSize)
    {
        var linkedIds = linkedSessionIds.ToList();

        // Build base query: active assignments for primary + linked sessions
        var assignmentQuery = _context.StudentSessionAssignments
            .Where(a => a.IsActive && a.TeacherId == teacherId)
            .Where(a => a.SessionId == sessionId
                || (a.SessionId.HasValue && linkedIds.Contains(a.SessionId.Value)))
            .Include(a => a.TeacherStudent);

        // Get the occurrence for this session on this date (for join with attendance records)
        var occurrenceId = await _context.SessionOccurrences
            .Where(o => o.SessionId == sessionId && o.OccurrenceDate == occurrenceDate.Date)
            .Select(o => (long?)o.Id)
            .FirstOrDefaultAsync();

        // Project to row model with LEFT JOIN to attendance records and absence counters
        var rowQuery = assignmentQuery
            .Select(a => new
            {
                a.TeacherStudentId,
                a.TeacherStudent.StudentName,
                a.TeacherStudent.StudentCode,
                AssignedSessionId = a.SessionId,
                AssignedSessionName = a.SessionName,
                IsFromLinkedSession = a.SessionId != sessionId,
                // LEFT JOIN: check if attendance exists for this occurrence
                AttendanceRecord = occurrenceId.HasValue
                    ? _context.AttendanceRecords
                        .Where(r => r.TeacherStudentId == a.TeacherStudentId
                            && r.SessionOccurrenceId == occurrenceId.Value)
                        .Select(r => new { r.Status })
                        .FirstOrDefault()
                    : null,
                // LEFT JOIN: absence counter
                Counter = _context.StudentAbsenceCounters
                    .Where(c => c.TeacherId == teacherId
                        && c.TeacherStudentId == a.TeacherStudentId)
                    .Select(c => new { c.ConsecutiveAbsences, c.TotalAbsences })
                    .FirstOrDefault()
            });

        // Apply search filter
        if (!string.IsNullOrWhiteSpace(search))
        {
            string searchLower = search.Trim().ToLower();
            rowQuery = rowQuery.Where(r =>
                r.StudentName.ToLower().Contains(searchLower)
                || r.StudentCode.ToLower().Contains(searchLower));
        }

        // Apply unmarked-only filter
        if (unmarkedOnly)
        {
            rowQuery = rowQuery.Where(r => r.AttendanceRecord == null);
        }

        // Get total count before pagination
        int totalCount = await rowQuery.CountAsync();

        // Order: unmarked first (REQ-ATT-054), then by name
        var pagedResults = await rowQuery
            .OrderBy(r => r.AttendanceRecord != null ? 1 : 0) // Unmarked first
            .ThenBy(r => r.StudentName)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        // Map to output model
        var items = pagedResults.Select(r => new PagedAttendanceStudentRow
        {
            TeacherStudentId = r.TeacherStudentId,
            StudentName = r.StudentName,
            StudentCode = r.StudentCode,
            SessionId = r.AssignedSessionId,
            SessionName = r.AssignedSessionName,
            IsFromLinkedSession = r.IsFromLinkedSession,
            SourceSessionName = r.IsFromLinkedSession ? r.AssignedSessionName : null,
            IsMarked = r.AttendanceRecord != null,
            CurrentStatus = r.AttendanceRecord != null ? r.AttendanceRecord.Status : null,
            ConsecutiveAbsences = r.Counter?.ConsecutiveAbsences ?? 0,
            TotalAbsences = r.Counter?.TotalAbsences ?? 0
        }).ToList();

        return (items, totalCount);
    }
}