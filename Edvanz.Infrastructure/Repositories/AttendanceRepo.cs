using Edvanz.Domain.Entities;
using Edvanz.Domain.Enums;
using Edvanz.Domain.Interfaces;
using Edvanz.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Edvanz.Infrastructure.Repositories;

/// <summary>
/// Extended repository for the Attendance Module (Module 3).
/// Centralizes all domain-specific query logic for SessionOccurrence,
/// StudentSessionAssignment, AttendanceRecord, AttendanceEditLog, and
/// StudentAbsenceCounter records.
/// 
/// ARCHITECTURAL NOTE (same rationale as SessionRepo and TeacherStudentRepo):
/// All expression-based queries are encapsulated here so the Application layer
/// never builds raw predicates. If a query changes, you edit it HERE —
/// not in every service that uses it.
/// 
/// Inherits from GenericRepo&lt;AttendanceRecord, long&gt; for basic CRUD on AttendanceRecord.
/// Other entity types are accessed via their own DbSet through the shared _context.
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
    public async Task<IReadOnlyList<SessionOccurrence>> GetOccurrencesBySessionAsync(long sessionId)
    {
        return await _context.SessionOccurrences
            .AsNoTracking()
            .Where(o => o.SessionId == sessionId)
            .OrderBy(o => o.OccurrenceIndex)
            .ToListAsync();
    }

    /// <inheritdoc />
    public async Task<SessionOccurrence?> GetOccurrenceBySessionAndDateAsync(long sessionId, DateTime date)
    {
        return await _context.SessionOccurrences
            .FirstOrDefaultAsync(o => o.SessionId == sessionId && o.OccurrenceDate == date.Date);
    }

    /// <inheritdoc />
    public async Task<SessionOccurrence?> GetOccurrenceByIdAsync(long occurrenceId)
    {
        return await _context.SessionOccurrences.FindAsync(occurrenceId);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<SessionOccurrence>> GetOccurrencesByDateAndTeacherAsync(
        long teacherId, DateTime date)
    {
        return await _context.SessionOccurrences
            .AsNoTracking()
            .Include(o => o.Session)
            .Where(o => o.OccurrenceDate == date.Date && o.Session.TeacherId == teacherId)
            .ToListAsync();
    }

    /// <inheritdoc />
    public async Task<SessionOccurrence?> GetPreviousOccurrenceAsync(long sessionId, int currentOccurrenceIndex)
    {
        if (currentOccurrenceIndex <= 0) return null;

        return await _context.SessionOccurrences
            .AsNoTracking()
            .FirstOrDefaultAsync(o => o.SessionId == sessionId
                                   && o.OccurrenceIndex == currentOccurrenceIndex - 1);
    }

    /// <inheritdoc />
    public async Task AddOccurrencesAsync(IEnumerable<SessionOccurrence> occurrences)
    {
        await _context.SessionOccurrences.AddRangeAsync(occurrences);
    }

    /// <inheritdoc />
    public async Task<int> DeleteUnusedOccurrencesAsync(long sessionId)
    {
        // Find occurrences that have attendance records (cannot delete these)
        var occurrencesWithAttendance = await _context.AttendanceRecords
            .Where(ar => ar.SessionOccurrence != null && ar.SessionOccurrence.SessionId == sessionId)
            .Select(ar => ar.SessionOccurrenceId)
            .Distinct()
            .ToListAsync();

        // Delete only occurrences without attendance
        var toDelete = await _context.SessionOccurrences
            .Where(o => o.SessionId == sessionId && !occurrencesWithAttendance.Contains(o.Id))
            .ToListAsync();

        _context.SessionOccurrences.RemoveRange(toDelete);
        await Task.CompletedTask;

        return occurrencesWithAttendance.Count; // Return count of protected occurrences
    }

    // ══════════════════════════════════════════════
    // STUDENT SESSION ASSIGNMENT QUERIES
    // ══════════════════════════════════════════════

    /// <inheritdoc />
    public async Task<StudentSessionAssignment?> GetActiveAssignmentAsync(long teacherStudentId)
    {
        return await _context.StudentSessionAssignments
            .FirstOrDefaultAsync(a => a.TeacherStudentId == teacherStudentId && a.IsActive);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<StudentSessionAssignment>> GetAssignmentsByStudentAsync(long teacherStudentId)
    {
        return await _context.StudentSessionAssignments
            .AsNoTracking()
            .Where(a => a.TeacherStudentId == teacherStudentId)
            .OrderBy(a => a.AssignedAt)
            .ToListAsync();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<StudentSessionAssignment>> GetActiveAssignmentsBySessionAsync(long sessionId)
    {
        return await _context.StudentSessionAssignments
            .AsNoTracking()
            .Where(a => a.SessionId == sessionId && a.IsActive)
            .ToListAsync();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<StudentSessionAssignment>> GetActiveAssignmentsBySessionsAsync(
        IEnumerable<long> sessionIds)
    {
        var idList = sessionIds.ToList();
        return await _context.StudentSessionAssignments
            .AsNoTracking()
            .Where(a => a.SessionId.HasValue && idList.Contains(a.SessionId.Value) && a.IsActive)
            .ToListAsync();
    }

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

    // ══════════════════════════════════════════════
    // ATTENDANCE RECORD QUERIES
    // ══════════════════════════════════════════════

    /// <inheritdoc />
    public async Task<AttendanceRecord?> GetRecordByOccurrenceAndStudentAsync(
        long sessionOccurrenceId, long teacherStudentId)
    {
        return await _context.AttendanceRecords
            .FirstOrDefaultAsync(ar => ar.SessionOccurrenceId == sessionOccurrenceId
                                    && ar.TeacherStudentId == teacherStudentId);
    }

    /// <inheritdoc />
    public async Task<AttendanceRecord?> GetRecordByDateAndStudentAcrossLinkedSessionsAsync(
        long teacherStudentId, DateTime occurrenceDate, IEnumerable<long> linkedSessionIds)
    {
        var sessionIdList = linkedSessionIds.ToList();

        return await _context.AttendanceRecords
            .Include(ar => ar.SessionOccurrence)
            .FirstOrDefaultAsync(ar =>
                ar.TeacherStudentId == teacherStudentId
                && ar.OccurrenceDate == occurrenceDate.Date
                && ar.SessionOccurrence != null
                && sessionIdList.Contains(ar.SessionOccurrence.SessionId));
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<AttendanceRecord>> GetRecordsByOccurrenceAsync(long sessionOccurrenceId)
    {
        return await _context.AttendanceRecords
            .AsNoTracking()
            .Where(ar => ar.SessionOccurrenceId == sessionOccurrenceId)
            .ToListAsync();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<AttendanceRecord>> GetRecordsByAssignmentAsync(long studentSessionAssignmentId)
    {
        return await _context.AttendanceRecords
            .AsNoTracking()
            .Where(ar => ar.StudentSessionAssignmentId == studentSessionAssignmentId)
            .OrderBy(ar => ar.OccurrenceDate)
            .ToListAsync();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<AttendanceRecord>> GetRecordsByStudentAsync(long teacherStudentId)
    {
        return await _context.AttendanceRecords
            .AsNoTracking()
            .Where(ar => ar.TeacherStudentId == teacherStudentId)
            .OrderBy(ar => ar.OccurrenceDate)
            .ThenBy(ar => ar.RecordedAt)
            .ToListAsync();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<AttendanceRecord>> GetRecordsByStudentAndMonthAsync(
        long teacherStudentId, int year, int month)
    {
        var startDate = new DateTime(year, month, 1);
        var endDate = startDate.AddMonths(1);

        return await _context.AttendanceRecords
            .AsNoTracking()
            .Where(ar => ar.TeacherStudentId == teacherStudentId
                      && ar.OccurrenceDate >= startDate
                      && ar.OccurrenceDate < endDate)
            .OrderBy(ar => ar.OccurrenceDate)
            .ToListAsync();
    }

    /// <inheritdoc />
    public async Task<(int TotalRecords, int TotalAbsences)> GetStudentAllTimeSummaryAsync(long teacherStudentId)
    {
        var totalRecords = await _context.AttendanceRecords
            .CountAsync(ar => ar.TeacherStudentId == teacherStudentId);

        var totalAbsences = await _context.AttendanceRecords
            .CountAsync(ar => ar.TeacherStudentId == teacherStudentId
                           && ar.Status == AttendanceStatus.Absent);

        return (totalRecords, totalAbsences);
    }

    /// <inheritdoc />
    public async Task<(int TotalRecords, int TotalAbsences)> GetStudentMonthlySummaryAsync(
        long teacherStudentId, int year, int month)
    {
        var startDate = new DateTime(year, month, 1);
        var endDate = startDate.AddMonths(1);

        var totalRecords = await _context.AttendanceRecords
            .CountAsync(ar => ar.TeacherStudentId == teacherStudentId
                           && ar.OccurrenceDate >= startDate
                           && ar.OccurrenceDate < endDate);

        var totalAbsences = await _context.AttendanceRecords
            .CountAsync(ar => ar.TeacherStudentId == teacherStudentId
                           && ar.OccurrenceDate >= startDate
                           && ar.OccurrenceDate < endDate
                           && ar.Status == AttendanceStatus.Absent);

        return (totalRecords, totalAbsences);
    }

    /// <inheritdoc />
    public IQueryable<AttendanceRecord> BuildSessionAttendanceQuery(long sessionId)
    {
        return _context.AttendanceRecords
            .AsNoTracking()
            .Where(ar => ar.SessionOccurrence != null && ar.SessionOccurrence.SessionId == sessionId)
            .OrderBy(ar => ar.OccurrenceDate)
            .ThenBy(ar => ar.TeacherStudentId);
    }

    /// <inheritdoc />
    public async Task<(int Present, int Absent, int Held)> GetOccurrenceStatusCountsAsync(long sessionOccurrenceId)
    {
        var presentCount = await _context.AttendanceRecords
            .CountAsync(ar => ar.SessionOccurrenceId == sessionOccurrenceId
                           && ar.Status == AttendanceStatus.Present);

        var absentCount = await _context.AttendanceRecords
            .CountAsync(ar => ar.SessionOccurrenceId == sessionOccurrenceId
                           && ar.Status == AttendanceStatus.Absent);

        var heldCount = await _context.AttendanceRecords
            .CountAsync(ar => ar.SessionOccurrenceId == sessionOccurrenceId
                           && ar.Status == AttendanceStatus.Held);

        return (presentCount, absentCount, heldCount);
    }

    // ══════════════════════════════════════════════
    // ATTENDANCE EDIT LOG QUERIES
    // ══════════════════════════════════════════════

    /// <inheritdoc />
    public async Task<IReadOnlyList<AttendanceEditLog>> GetEditLogsByRecordAsync(long attendanceRecordId)
    {
        return await _context.AttendanceEditLogs
            .AsNoTracking()
            .Where(l => l.AttendanceRecordId == attendanceRecordId)
            .OrderByDescending(l => l.EditedAt)
            .ToListAsync();
    }

    /// <inheritdoc />
    public async Task AddEditLogAsync(AttendanceEditLog editLog)
    {
        await _context.AttendanceEditLogs.AddAsync(editLog);
    }

    // ══════════════════════════════════════════════
    // STUDENT ABSENCE COUNTER QUERIES
    // ══════════════════════════════════════════════

    /// <inheritdoc />
    public async Task<StudentAbsenceCounter?> GetAbsenceCounterByStudentAsync(long teacherStudentId)
    {
        return await _context.StudentAbsenceCounters
            .FirstOrDefaultAsync(c => c.TeacherStudentId == teacherStudentId);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<StudentAbsenceCounter>> GetAbsenceCountersBySessionAsync(long sessionId)
    {
        // Join through StudentSessionAssignments to find students in this session
        return await _context.StudentAbsenceCounters
            .AsNoTracking()
            .Where(c => _context.StudentSessionAssignments
                .Any(a => a.TeacherStudentId == c.TeacherStudentId
                       && a.SessionId == sessionId
                       && a.IsActive))
            .OrderByDescending(c => c.ConsecutiveAbsences)
            .ToListAsync();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<StudentAbsenceCounter>> GetAbsenceCountersBySessionsAsync(
        IEnumerable<long> sessionIds)
    {
        var idList = sessionIds.ToList();

        return await _context.StudentAbsenceCounters
            .AsNoTracking()
            .Where(c => _context.StudentSessionAssignments
                .Any(a => a.TeacherStudentId == c.TeacherStudentId
                       && a.SessionId.HasValue
                       && idList.Contains(a.SessionId.Value)
                       && a.IsActive))
            .OrderByDescending(c => c.ConsecutiveAbsences)
            .ToListAsync();
    }

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
    public IQueryable<StudentAbsenceCounter> BuildAbsenceOverviewQuery(
        long sessionId,
        IEnumerable<long>? linkedSessionIds = null,
        string? search = null)
    {
        var allSessionIds = new List<long> { sessionId };
        if (linkedSessionIds is not null)
            allSessionIds.AddRange(linkedSessionIds);

        var query = _context.StudentAbsenceCounters
            .AsNoTracking()
            .Where(c => _context.StudentSessionAssignments
                .Any(a => a.TeacherStudentId == c.TeacherStudentId
                       && a.SessionId.HasValue
                       && allSessionIds.Contains(a.SessionId.Value)
                       && a.IsActive));

        // Apply search filter on student name or code
        if (!string.IsNullOrWhiteSpace(search))
        {
            var searchLower = search.ToLower();
            query = query.Where(c =>
                _context.TeacherStudents.Any(ts =>
                    ts.Id == c.TeacherStudentId
                    && (ts.StudentName.ToLower().Contains(searchLower)
                        || ts.StudentCode.ToLower().Contains(searchLower))));
        }

        // Default sort: consecutive absences descending (REQ-ATT-067)
        return query.OrderByDescending(c => c.ConsecutiveAbsences);
    }
}