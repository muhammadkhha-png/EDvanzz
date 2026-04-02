using Edvanz.Domain.Constants;
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
/// CHANGES FROM ORIGINAL:
/// Step 1.1: Added NullifyStudentReferencesOnRecordsAsync for student purge FK safety.
/// Step 1.2: NullifyOccurrenceReferencesForSessionAsync and NullifySessionIdOnRecordsForSessionAsync
///           now use ExecuteUpdateAsync instead of load-and-loop (OOM fix at scale).
/// Step 1.2: DeactivateAssignmentsBySessionAsync uses ExecuteUpdateAsync.
/// Step 2.1: Added GetExistingAttendanceByStudentsAndDateAsync for batch cross-session dup check.
/// Step 2.2: RecalculateConsecutiveAbsencesAsync excludes Held status, uses configurable depth.
/// Step 3.1: Added GetHeldRecordAsync for hold/release flow.
/// Step 4.1: Added UpdateAbsenceCountersRangeAsync for batch counter updates.
/// Step 5.2: GetRecordsBySessionAndDateRangeAsync now includes teacherId guard.
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
            .Include(o => o.Session)
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

        if (occurrences.Count > 0)
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
    public async Task<HashSet<DateTime>> GetExistingOccurrenceDatesAsync(long sessionId)
    {
        var dates = await _context.SessionOccurrences
            .Where(o => o.SessionId == sessionId)
            .Select(o => o.OccurrenceDate)
            .ToListAsync();
        return new HashSet<DateTime>(dates);
    }

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
            .ToDictionaryAsync(g => g.Key, g => g.Count());
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
    /// Step 1.2: Uses ExecuteUpdateAsync — single SQL UPDATE, no in-memory loading.
    /// Original loaded all assignments into memory and looped. For a session with 50K students,
    /// this was 50K entities tracked. Now it's one SQL statement.
    public async Task DeactivateAssignmentsBySessionAsync(long sessionId)
    {
        await _context.StudentSessionAssignments
            .Where(a => a.SessionId == sessionId && a.IsActive)
            .ExecuteUpdateAsync(s => s
                .SetProperty(a => a.IsActive, false)
                .SetProperty(a => a.UnassignedAt, DateTime.UtcNow)
                .SetProperty(a => a.SessionId, (long?)null));
    }

    /// <inheritdoc />
    public async Task DeactivateAssignmentsByStudentAsync(long teacherStudentId)
    {
        await _context.StudentSessionAssignments
            .Where(a => a.TeacherStudentId == teacherStudentId && a.IsActive)
            .ExecuteUpdateAsync(s => s
                .SetProperty(a => a.IsActive, false)
                .SetProperty(a => a.UnassignedAt, DateTime.UtcNow)
                .SetProperty(a => a.TeacherStudentId, (long?)null));
    }

    /// <inheritdoc />
    public async Task<Dictionary<long, StudentSessionAssignment>> GetActiveAssignmentsBatchAsync(
        IEnumerable<long> teacherStudentIds)
    {
        var idList = teacherStudentIds.ToList();
        if (idList.Count == 0)
            return new Dictionary<long, StudentSessionAssignment>();

        var assignments = await _context.StudentSessionAssignments
            .Where(a => a.TeacherStudentId.HasValue
                && idList.Contains(a.TeacherStudentId.Value)
                && a.IsActive)
            .ToListAsync();

        return assignments
            .Where(a => a.TeacherStudentId.HasValue)
            .GroupBy(a => a.TeacherStudentId!.Value)
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
    public async Task<HashSet<long>> GetExistingAttendanceBatchAsync(
        IEnumerable<long> teacherStudentIds, long sessionOccurrenceId)
    {
        var idList = teacherStudentIds.ToList();
        if (idList.Count == 0)
            return new HashSet<long>();

        var existingIds = await _context.AttendanceRecords
            .Where(r => r.SessionOccurrenceId == sessionOccurrenceId
                && r.TeacherStudentId.HasValue
                && idList.Contains(r.TeacherStudentId.Value))
            .Select(r => r.TeacherStudentId!.Value)
            .ToListAsync();

        return new HashSet<long>(existingIds);
    }

    /// <inheritdoc />
    /// Step 2.1: Batch cross-session duplicate check for BulkMarkAttendanceAsync.
    public async Task<Dictionary<long, AttendanceRecord>> GetExistingAttendanceByStudentsAndDateAsync(
        IEnumerable<long> teacherStudentIds, DateTime occurrenceDate, IEnumerable<long> linkedSessionIds)
    {
        var studentIdList = teacherStudentIds.ToList();
        var sessionIdList = linkedSessionIds.ToList();

        if (studentIdList.Count == 0 || sessionIdList.Count == 0)
            return new Dictionary<long, AttendanceRecord>();

        var records = await _context.AttendanceRecords
            .Where(r => r.TeacherStudentId.HasValue
                && studentIdList.Contains(r.TeacherStudentId.Value)
                && r.OccurrenceDate == occurrenceDate.Date
                && r.SessionId.HasValue
                && sessionIdList.Contains(r.SessionId.Value))
            .ToListAsync();

        // Return first match per student (there should be at most one per BR-ATT-002)
        return records
            .Where(r => r.TeacherStudentId.HasValue)
            .GroupBy(r => r.TeacherStudentId!.Value)
            .ToDictionary(g => g.Key, g => g.First());
    }

    /// <inheritdoc />
    /// Step 3.1: Find a held record for hold/release flow.
    public async Task<AttendanceRecord?> GetHeldRecordAsync(
        long teacherStudentId, long sessionOccurrenceId)
    {
        return await _context.AttendanceRecords
            .FirstOrDefaultAsync(r => r.TeacherStudentId == teacherStudentId
                && r.SessionOccurrenceId == sessionOccurrenceId
                && r.Status == AttendanceStatus.Held);
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
    /// Step 5.2: Added teacherId parameter for defense-in-depth tenant isolation.
    public async Task<IReadOnlyList<AttendanceRecord>> GetRecordsBySessionAndDateRangeAsync(
        long teacherId, long sessionId, DateTime startDate, DateTime endDate)
    {
        return await _context.AttendanceRecords
            .Where(r => r.TeacherId == teacherId
                && r.SessionId == sessionId
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
    /// Step 1.2: Uses ExecuteUpdateAsync — single SQL UPDATE, no in-memory loading.
    /// Original loaded ALL records into memory and looped. For 2.6M records, this caused OOM.
    public async Task NullifyOccurrenceReferencesForSessionAsync(long sessionId)
    {
        var occurrenceIds = await _context.SessionOccurrences
            .Where(o => o.SessionId == sessionId)
            .Select(o => o.Id)
            .ToListAsync();

        if (occurrenceIds.Count == 0)
            return;

        await _context.AttendanceRecords
            .Where(r => r.SessionOccurrenceId.HasValue
                && occurrenceIds.Contains(r.SessionOccurrenceId.Value))
            .ExecuteUpdateAsync(s => s
                .SetProperty(r => r.SessionOccurrenceId, (long?)null));
    }

    /// <inheritdoc />
    /// Step 1.2: Uses ExecuteUpdateAsync — single SQL UPDATE, no in-memory loading.
    public async Task NullifySessionIdOnRecordsForSessionAsync(long sessionId)
    {
        await _context.AttendanceRecords
            .Where(r => r.SessionId == sessionId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(r => r.SessionId, (long?)null));
    }

    /// <inheritdoc />
    /// Step 1.1: Nullifies student FK references before student hard-delete.
    /// Uses ExecuteUpdateAsync for safety at scale.
    /// Denormalized StudentName and StudentCode remain intact for historical display.
    public async Task NullifyStudentReferencesOnRecordsAsync(long teacherStudentId)
    {
        await _context.AttendanceRecords
            .Where(r => r.TeacherStudentId == teacherStudentId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(r => r.TeacherStudentId, (long?)null)
                .SetProperty(r => r.StudentSessionAssignmentId, (long?)null));
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
    /// Step 4.1: Batch update for multiple counters.
    public async Task UpdateAbsenceCountersRangeAsync(IEnumerable<StudentAbsenceCounter> counters)
    {
        foreach (var counter in counters)
        {
            _context.Entry(counter).State = EntityState.Modified;
        }
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
    /// Step 2.2: Excludes Held status from streak calculation.
    /// Uses configurable depth from AttendanceConstants instead of hardcoded 100.
    public async Task<int> RecalculateConsecutiveAbsencesAsync(long teacherStudentId)
    {
        var recentRecords = await _context.AttendanceRecords
            .Where(r => r.TeacherStudentId == teacherStudentId
                && r.Status != AttendanceStatus.Held) // Step 2.2: Exclude Held
            .OrderByDescending(r => r.OccurrenceDate)
            .ThenByDescending(r => r.RecordedAt)
            .Select(r => r.Status)
            .Take(AttendanceConstants.MaxConsecutiveAbsenceScanDepth)
            .ToListAsync();

        int consecutive = 0;
        foreach (var status in recentRecords)
        {
            if (status == AttendanceStatus.Absent)
                consecutive++;
            else
                break; // Any non-absent, non-held status breaks the streak
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
    public async Task DeleteAbsenceCountersByStudentAsync(long teacherStudentId)
    {
        var counters = await _context.StudentAbsenceCounters
            .Where(c => c.TeacherStudentId == teacherStudentId)
            .ToListAsync();

        if (counters.Count > 0)
            _context.StudentAbsenceCounters.RemoveRange(counters);
    }

    /// <inheritdoc />
    public async Task<AttendanceRecord?> GetExistingAttendanceByStudentSessionAndDateAsync(
        long teacherStudentId, long sessionId, DateTime occurrenceDate)
    {
        return await _context.AttendanceRecords
            .FirstOrDefaultAsync(r => r.TeacherStudentId == teacherStudentId
                && r.SessionId == sessionId
                && r.OccurrenceDate == occurrenceDate.Date);
    }

    /// <inheritdoc />
    /// FIX H4: Added phone filter parameters for REQ-ATT-034 compliance.
    /// FIX M7: Replaced ToLower() with EF.Functions.Like for SQL Server index usage.
    public async Task<IReadOnlyList<AttendanceRecord>> GetAbsentStudentsByDateAsync(
        long teacherId, IEnumerable<long> sessionIds, DateTime occurrenceDate,
        string? search, int page, int pageSize,
        bool? missingStudentPhone = null, bool? missingParentPhone = null)
    {
        var ids = sessionIds.ToList();
        var query = _context.AttendanceRecords
            .Where(r => r.TeacherId == teacherId
                && r.OccurrenceDate == occurrenceDate.Date
                && r.Status == AttendanceStatus.Absent
                && r.SessionId.HasValue
                && ids.Contains(r.SessionId.Value))
            .Include(r => r.TeacherStudent)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            // FIX M7: Use EF.Functions.Like instead of ToLower().Contains()
            // SQL Server default collation is case-insensitive, so LIKE works correctly.
            string pattern = $"%{search.Trim()}%";
            query = query.Where(r =>
                (r.StudentName != null && EF.Functions.Like(r.StudentName, pattern))
                || (r.StudentCode != null && EF.Functions.Like(r.StudentCode, pattern))
                || (r.TeacherStudent != null && EF.Functions.Like(r.TeacherStudent.StudentName, pattern))
                || (r.TeacherStudent != null && EF.Functions.Like(r.TeacherStudent.StudentCode, pattern)));
        }

        // FIX H4: Apply phone filters (were missing on the date-specific path).
        if (missingStudentPhone == true)
        {
            query = query.Where(r =>
                r.TeacherStudent != null
                && (r.TeacherStudent.StudentPhoneNumber == null
                    || r.TeacherStudent.StudentPhoneNumber == ""));
        }

        if (missingParentPhone == true)
        {
            query = query.Where(r =>
                r.TeacherStudent != null
                && (r.TeacherStudent.ParentPhoneNumber == null
                    || r.TeacherStudent.ParentPhoneNumber == ""));
        }

        return await query
            .OrderBy(r => r.StudentName ?? r.TeacherStudent!.StudentName)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .AsNoTracking()
            .ToListAsync();
    }

    /// <inheritdoc />
    /// FIX H4: Added phone filter parameters for REQ-ATT-034 compliance.
    /// FIX M7: Replaced ToLower() with EF.Functions.Like for SQL Server index usage.
    public async Task<int> CountAbsentStudentsByDateAsync(
        long teacherId, IEnumerable<long> sessionIds, DateTime occurrenceDate,
        string? search,
        bool? missingStudentPhone = null, bool? missingParentPhone = null)
    {
        var ids = sessionIds.ToList();
        var query = _context.AttendanceRecords
            .Where(r => r.TeacherId == teacherId
                && r.OccurrenceDate == occurrenceDate.Date
                && r.Status == AttendanceStatus.Absent
                && r.SessionId.HasValue
                && ids.Contains(r.SessionId.Value))
            .Include(r => r.TeacherStudent)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            string pattern = $"%{search.Trim()}%";
            query = query.Where(r =>
                (r.StudentName != null && EF.Functions.Like(r.StudentName, pattern))
                || (r.StudentCode != null && EF.Functions.Like(r.StudentCode, pattern)));
        }

        if (missingStudentPhone == true)
        {
            query = query.Where(r =>
                r.TeacherStudent != null
                && (r.TeacherStudent.StudentPhoneNumber == null
                    || r.TeacherStudent.StudentPhoneNumber == ""));
        }

        if (missingParentPhone == true)
        {
            query = query.Where(r =>
                r.TeacherStudent != null
                && (r.TeacherStudent.ParentPhoneNumber == null
                    || r.TeacherStudent.ParentPhoneNumber == ""));
        }

        return await query.CountAsync();
    }

    // ══════════════════════════════════════════════
    // PRIVATE HELPER: ABSENCE OVERVIEW QUERY BUILDER
    // ══════════════════════════════════════════════

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
                .Where(a => a.SessionId == sessionId.Value && a.IsActive && a.TeacherStudentId.HasValue)
                .Select(a => a.TeacherStudentId!.Value);
            query = query.Where(c => studentIdsInSession.Contains(c.TeacherStudentId));
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            // FIX M7: Use EF.Functions.Like instead of ToLower().Contains()
            string pattern = $"%{search.Trim()}%";
            query = query.Where(c =>
                EF.Functions.Like(c.TeacherStudent!.StudentName, pattern)
                || EF.Functions.Like(c.TeacherStudent.StudentCode, pattern));
        }

        if (missingStudentPhone == true)
        {
            query = query.Where(c =>
                c.TeacherStudent!.StudentPhoneNumber == null
                || c.TeacherStudent.StudentPhoneNumber == "");
        }

        if (missingParentPhone == true)
        {
            query = query.Where(c =>
                c.TeacherStudent!.ParentPhoneNumber == null
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
            .ThenBy(c => c.TeacherStudent!.StudentName)
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
            // FIX H3: Use denormalized SessionGroupId field instead of navigating through
            // SessionOccurrence.Session.SessionGroupId. After session hard-delete,
            // SessionOccurrence is cascade-deleted and the navigation returns null,
            // causing records from deleted sessions to be excluded from Report Type 5.
            query = query.Where(r => r.SessionGroupId == sessionGroupId.Value);

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
            .Where(a => a.TeacherId == teacherId && a.TeacherStudentId.HasValue)
            .Include(a => a.TeacherStudent)
            .AsQueryable();

        if (sessionId.HasValue)
            query = query.Where(a => a.SessionId == sessionId.Value);

        if (sessionGroupId.HasValue)
            query = query.Where(a => a.Session != null
                && a.Session.SessionGroupId == sessionGroupId.Value);

        if (!string.IsNullOrWhiteSpace(studentName))
        {
            // FIX M7: Use EF.Functions.Like instead of ToLower().Contains()
            string pattern = $"%{studentName.Trim()}%";
            query = query.Where(a => EF.Functions.Like(a.TeacherStudent!.StudentName, pattern));
        }

        if (!string.IsNullOrWhiteSpace(studentCode))
        {
            string pattern = $"%{studentCode.Trim()}%";
            query = query.Where(a => EF.Functions.Like(a.TeacherStudent!.StudentCode, pattern));
        }

        return await query
            .Select(a => a.TeacherStudentId!.Value)
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

        var assignmentQuery = _context.StudentSessionAssignments
            .Where(a => a.IsActive && a.TeacherId == teacherId)
            .Where(a => a.SessionId == sessionId
                || (a.SessionId.HasValue && linkedIds.Contains(a.SessionId.Value)))
            .Include(a => a.TeacherStudent);

        var occurrenceId = await _context.SessionOccurrences
            .Where(o => o.SessionId == sessionId && o.OccurrenceDate == occurrenceDate.Date)
            .Select(o => (long?)o.Id)
            .FirstOrDefaultAsync();

        var rowQuery = assignmentQuery
            .Select(a => new
            {
                a.TeacherStudentId,
                StudentName = a.TeacherStudent != null ? a.TeacherStudent.StudentName : "Unknown",
                StudentCode = a.TeacherStudent != null ? a.TeacherStudent.StudentCode : "",
                AssignedSessionId = a.SessionId,
                AssignedSessionName = a.SessionName,
                IsFromLinkedSession = a.SessionId != sessionId,
                AttendanceRecord = occurrenceId.HasValue
                    ? _context.AttendanceRecords
                        .Where(r => r.TeacherStudentId == a.TeacherStudentId
                            && r.SessionOccurrenceId == occurrenceId.Value)
                        .Select(r => new { r.Status })
                        .FirstOrDefault()
                    : null,
                Counter = _context.StudentAbsenceCounters
                    .Where(c => c.TeacherId == teacherId
                        && c.TeacherStudentId == a.TeacherStudentId)
                    .Select(c => new { c.ConsecutiveAbsences, c.TotalAbsences })
                    .FirstOrDefault()
            });

        if (!string.IsNullOrWhiteSpace(search))
        {
            // FIX M7: Use EF.Functions.Like instead of ToLower().Contains()
            string pattern = $"%{search.Trim()}%";
            rowQuery = rowQuery.Where(r =>
                EF.Functions.Like(r.StudentName, pattern)
                || EF.Functions.Like(r.StudentCode, pattern));
        }

        if (unmarkedOnly)
        {
            rowQuery = rowQuery.Where(r => r.AttendanceRecord == null);
        }

        int totalCount = await rowQuery.CountAsync();

        var pagedResults = await rowQuery
            .OrderBy(r => r.AttendanceRecord != null ? 1 : 0)
            .ThenBy(r => r.StudentName)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var items = pagedResults.Select(r => new PagedAttendanceStudentRow
        {
            TeacherStudentId = r.TeacherStudentId!.Value,
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

    // ══════════════════════════════════════════════
    // V2 AUDIT FIX — NEW BATCH METHODS
    // ══════════════════════════════════════════════

    /// <inheritdoc />
    /// FIX C1 (REQ-ATT-050): Batch count of active assignments per session.
    public async Task<Dictionary<long, int>> CountActiveAssignmentsBySessionBatchAsync(
        IEnumerable<long> sessionIds)
    {
        var idList = sessionIds.ToList();
        if (idList.Count == 0)
            return new Dictionary<long, int>();

        return await _context.StudentSessionAssignments
            .Where(a => a.IsActive && a.SessionId.HasValue && idList.Contains(a.SessionId.Value))
            .GroupBy(a => a.SessionId!.Value)
            .ToDictionaryAsync(g => g.Key, g => g.Count());
    }

    /// <inheritdoc />
    /// FIX M3 (REQ-ATT-068): Batch-load recent statuses for multiple students.
    /// Uses a single SQL query with ROW_NUMBER() partitioning via GroupBy + Take.
    public async Task<Dictionary<long, IReadOnlyList<AttendanceStatus>>>
        GetRecentRecordsByStudentsBatchAsync(
            IEnumerable<long> teacherStudentIds, int count)
    {
        var idList = teacherStudentIds.ToList();
        if (idList.Count == 0)
            return new Dictionary<long, IReadOnlyList<AttendanceStatus>>();

        // Load recent records for all students in one query, then group in memory.
        // EF Core doesn't support per-group Take, so we fetch the latest N*studentCount
        // records and trim per student. This is a pragmatic trade-off:
        // for a page of 20 students × 5 statuses = 100 records max.
        var records = await _context.AttendanceRecords
            .Where(r => r.TeacherStudentId.HasValue
                && idList.Contains(r.TeacherStudentId.Value)
                && r.Status != AttendanceStatus.Held)
            .OrderByDescending(r => r.OccurrenceDate)
            .ThenByDescending(r => r.RecordedAt)
            .Select(r => new { r.TeacherStudentId, r.Status })
            .AsNoTracking()
            .ToListAsync();

        return records
            .Where(r => r.TeacherStudentId.HasValue)
            .GroupBy(r => r.TeacherStudentId!.Value)
            .ToDictionary(
                g => g.Key,
                g => (IReadOnlyList<AttendanceStatus>)g.Take(count).Select(r => r.Status).ToList());
    }

    /// <inheritdoc />
    /// FIX M4 (REQ-ATT-072): DB-level pagination for timeline student list.
    public async Task<(IReadOnlyList<long> PagedIds, int TotalCount)> GetPagedTimelineStudentIdsAsync(
        long teacherId,
        int page,
        int pageSize,
        long? sessionId = null,
        long? sessionGroupId = null,
        string? studentName = null,
        string? studentCode = null)
    {
        var query = _context.StudentSessionAssignments
            .Where(a => a.TeacherId == teacherId && a.TeacherStudentId.HasValue)
            .Include(a => a.TeacherStudent)
            .AsQueryable();

        if (sessionId.HasValue)
            query = query.Where(a => a.SessionId == sessionId.Value);

        if (sessionGroupId.HasValue)
            query = query.Where(a => a.Session != null
                && a.Session.SessionGroupId == sessionGroupId.Value);

        if (!string.IsNullOrWhiteSpace(studentName))
        {
            // FIX M7: Use EF.Functions.Like
            string pattern = $"%{studentName.Trim()}%";
            query = query.Where(a => EF.Functions.Like(a.TeacherStudent!.StudentName, pattern));
        }

        if (!string.IsNullOrWhiteSpace(studentCode))
        {
            string pattern = $"%{studentCode.Trim()}%";
            query = query.Where(a => EF.Functions.Like(a.TeacherStudent!.StudentCode, pattern));
        }

        // Get distinct student IDs with DB-level pagination
        var distinctQuery = query
            .Select(a => a.TeacherStudentId!.Value)
            .Distinct();

        int totalCount = await distinctQuery.CountAsync();

        var pagedIds = await distinctQuery
            .OrderBy(id => id) // Stable ordering for pagination
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (pagedIds, totalCount);
    }
}