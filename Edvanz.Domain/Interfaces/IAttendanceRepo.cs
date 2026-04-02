using Edvanz.Domain.Entities;
using Edvanz.Domain.Enums;

namespace Edvanz.Domain.Interfaces;

/// <summary>
/// Extended repository interface for the Attendance Module (Module 3).
/// Centralizes all domain-specific query methods for attendance-related entities:
/// SessionOccurrence, StudentSessionAssignment, AttendanceRecord,
/// AttendanceEditLog, and StudentAbsenceCounter.
///
/// ARCHITECTURAL NOTE (same rationale as IUserRepo and ITeacherStudentRepo):
/// All expression-based queries are encapsulated here in named methods.
/// The Application layer never builds raw predicates. If a query changes,
/// you edit ONE method here — not every service that uses it.
///
/// Inherits from IGenericRepo&lt;AttendanceRecord, long&gt; for basic CRUD on the
/// primary entity. Other entities are accessed via named methods below.
/// </summary>
public interface IAttendanceRepo : IGenericRepo<AttendanceRecord, long>
{
    /// <summary>
    /// Checks if attendance already exists for a student in a specific session on a specific date.
    /// Audit Fix: Used for cross-session remapped-date duplicate detection.
    /// </summary>
    Task<AttendanceRecord?> GetExistingAttendanceByStudentSessionAndDateAsync(
        long teacherStudentId, long sessionId, DateTime occurrenceDate);

    /// <summary>
    /// Gets students who were absent on a specific occurrence date across given sessions.
    /// Audit Fix (REQ-ATT-035): View absence history for a selected past date.
    /// </summary>
    Task<IReadOnlyList<AttendanceRecord>> GetAbsentStudentsByDateAsync(
        long teacherId, IEnumerable<long> sessionIds, DateTime occurrenceDate,
        string? search, int page, int pageSize);

    /// <summary>
    /// Counts students who were absent on a specific date across given sessions.
    /// Audit Fix (REQ-ATT-035): Count variant for date-specific absence overview.
    /// </summary>
    Task<int> CountAbsentStudentsByDateAsync(
        long teacherId, IEnumerable<long> sessionIds, DateTime occurrenceDate,
        string? search);

    // ══════════════════════════════════════════════
    // SESSION OCCURRENCE QUERIES
    // ══════════════════════════════════════════════

    /// <summary>
    /// Adds a new session occurrence to the database.
    /// Called during session creation and date range extension.
    /// </summary>
    Task AddOccurrenceAsync(SessionOccurrence occurrence);

    /// <summary>
    /// Adds multiple session occurrences in bulk.
    /// Used when generating all occurrences for a newly created session.
    /// </summary>
    Task AddOccurrencesRangeAsync(IEnumerable<SessionOccurrence> occurrences);

    /// <summary>
    /// Finds a specific occurrence for a session on a given date.
    /// REQ-ATT-001/002: Determines if today is a valid occurrence for a session.
    /// </summary>
    Task<SessionOccurrence?> GetOccurrenceBySessionAndDateAsync(long sessionId, DateTime date);

    /// <summary>
    /// Retrieves all occurrences for a specific session, ordered by date ascending.
    /// REQ-ATT-037: Browsing past or future attendance dates.
    /// REQ-ATT-065: Calendar view for Edit Attendance.
    /// </summary>
    Task<IReadOnlyList<SessionOccurrence>> GetOccurrencesBySessionAsync(long sessionId);

    /// <summary>
    /// Retrieves all occurrences for a teacher on a specific date.
    /// REQ-ATT-001/003: "Today's sessions" — identify which sessions occur today.
    /// REQ-ATT-049: Dashboard daily summary count.
    /// </summary>
    Task<IReadOnlyList<SessionOccurrence>> GetOccurrencesByTeacherAndDateAsync(long teacherId, DateTime date);

    /// <summary>
    /// Updates an existing session occurrence (e.g., status change).
    /// REQ-ATT-049/051: Occurrence status updated as attendance is taken.
    /// </summary>
    Task UpdateOccurrenceAsync(SessionOccurrence occurrence);

    /// <summary>
    /// Deletes all occurrences for a session.
    /// Called before session hard-delete to clean up occurrences.
    /// AttendanceRecords referencing these occurrences get SessionOccurrenceId set to null (SET NULL FK).
    /// </summary>
    Task DeleteOccurrencesBySessionAsync(long sessionId);

    /// <summary>
    /// Retrieves occurrences for a session within a date range, ordered by date ascending.
    /// REQ-ATT-079: Month-by-month loading for student timeline.
    /// REQ-ATT-040: Report generation for specific date ranges.
    /// </summary>
    Task<IReadOnlyList<SessionOccurrence>> GetOccurrencesBySessionAndDateRangeAsync(
        long sessionId, DateTime startDate, DateTime endDate);

    /// <summary>
    /// Counts occurrences for a session within a date range.
    /// REQ-ATT-077: Monthly summary "total occurrences in that month".
    /// </summary>
    Task<int> CountOccurrencesBySessionAndDateRangeAsync(
        long sessionId, DateTime startDate, DateTime endDate);

    /// <summary>
    /// Gets the most recent occurrence for a session that is before a given date.
    /// REQ-ATT-027: "Immediately preceding session occurrence" for absence check.
    /// </summary>
    Task<SessionOccurrence?> GetPreviousOccurrenceAsync(long sessionId, DateTime beforeDate);

    /// <summary>
    /// Gets the next occurrence for a session on or after a given date.
    /// Step 4.2: Used for cross-session attendance date remapping (REQ-ATT-018).
    /// </summary>
    Task<SessionOccurrence?> GetNextOccurrenceAsync(long sessionId, DateTime onOrAfterDate);

    /// <summary>
    /// Gets the latest (most recent date) occurrence for a session.
    /// </summary>
    Task<SessionOccurrence?> GetLatestOccurrenceBySessionAsync(long sessionId);

    /// <summary>
    /// Gets only the occurrence dates for a session (efficient date-only projection).
    /// </summary>
    Task<HashSet<DateTime>> GetExistingOccurrenceDatesAsync(long sessionId);

    // ══════════════════════════════════════════════
    // BATCH OCCURRENCE COUNTING (FIX 2.1)
    // ══════════════════════════════════════════════

    /// <summary>
    /// Counts attendance records grouped by occurrence Id in a single query.
    /// </summary>
    Task<Dictionary<long, int>> CountRecordsByOccurrenceBatchAsync(IEnumerable<long> occurrenceIds);

    // ══════════════════════════════════════════════
    // STUDENT SESSION ASSIGNMENT QUERIES
    // ══════════════════════════════════════════════

    /// <summary>
    /// Adds a new student session assignment.
    /// REQ-ATT-019/045: Creates a new assignment period.
    /// </summary>
    Task AddAssignmentAsync(StudentSessionAssignment assignment);

    /// <summary>
    /// Updates an existing student session assignment.
    /// REQ-ATT-020: Deactivates when student is reassigned.
    /// </summary>
    Task UpdateAssignmentAsync(StudentSessionAssignment assignment);

    /// <summary>
    /// Gets the active assignment for a student (the one with IsActive = true).
    /// There should be at most one active assignment per student at any time.
    /// </summary>
    Task<StudentSessionAssignment?> GetActiveAssignmentAsync(long teacherStudentId);

    /// <summary>
    /// Gets all assignments (active and inactive) for a student, ordered by AssignedAt.
    /// REQ-ATT-046: Chronological timeline of all session periods.
    /// </summary>
    Task<IReadOnlyList<StudentSessionAssignment>> GetAssignmentsByStudentAsync(long teacherStudentId);

    /// <summary>
    /// Gets all active assignments for a session.
    /// REQ-ATT-050: Count students assigned to a session for progress tracking.
    /// </summary>
    Task<IReadOnlyList<StudentSessionAssignment>> GetActiveAssignmentsBySessionAsync(long sessionId);

    /// <summary>
    /// Step 1.2: Deactivates all assignments for a session using ExecuteUpdateAsync.
    /// Also nullifies SessionId to prevent FK violation on session hard-delete.
    /// Called before session hard-delete.
    /// </summary>
    Task DeactivateAssignmentsBySessionAsync(long sessionId);

    /// <summary>
    /// Deactivates all active assignments for a student.
    /// Called during permanent student purge.
    /// </summary>
    Task DeactivateAssignmentsByStudentAsync(long teacherStudentId);

    // ══════════════════════════════════════════════
    // BATCH ASSIGNMENT QUERIES (FIX 2.2)
    // ══════════════════════════════════════════════

    /// <summary>
    /// Gets active assignments for multiple students in a single query.
    /// Returns a dictionary keyed by TeacherStudentId for O(1) lookup.
    /// </summary>
    Task<Dictionary<long, StudentSessionAssignment>> GetActiveAssignmentsBatchAsync(
        IEnumerable<long> teacherStudentIds);

    // ══════════════════════════════════════════════
    // ATTENDANCE RECORD QUERIES
    // ══════════════════════════════════════════════

    /// <summary>
    /// Adds a new attendance record.
    /// REQ-ATT-006: Unified record regardless of attendance method.
    /// </summary>
    Task AddAttendanceRecordAsync(AttendanceRecord record);

    /// <summary>
    /// Adds multiple attendance records in bulk.
    /// REQ-ATT-055: "Mark All Present" and multi-select operations.
    /// </summary>
    Task AddAttendanceRecordsRangeAsync(IEnumerable<AttendanceRecord> records);

    /// <summary>
    /// Updates an existing attendance record.
    /// REQ-ATT-024: Edit Attendance — change status, update metadata.
    /// </summary>
    Task UpdateAttendanceRecordAsync(AttendanceRecord record);

    /// <summary>
    /// Deletes an attendance record (hard delete).
    /// REQ-ATT-024: "Remove erroneously recorded attendance entries."
    /// </summary>
    Task DeleteAttendanceRecordAsync(AttendanceRecord record);

    /// <summary>
    /// Gets an attendance record by its Id, scoped to the teacher.
    /// </summary>
    Task<AttendanceRecord?> GetAttendanceRecordByIdAsync(long recordId, long teacherId);

    /// <summary>
    /// Checks if attendance already exists for a student on a specific occurrence.
    /// BR-ATT-002: Duplicate prevention.
    /// </summary>
    Task<AttendanceRecord?> GetExistingAttendanceAsync(long teacherStudentId, long sessionOccurrenceId);

    /// <summary>
    /// Checks if attendance already exists for a student on any linked session for the same date.
    /// REQ-ATT-069/070: Cross-session duplicate detection.
    /// </summary>
    Task<AttendanceRecord?> GetExistingAttendanceByStudentAndDateAsync(
        long teacherStudentId, DateTime occurrenceDate, IEnumerable<long> linkedSessionIds);

    /// <summary>
    /// Checks for existing attendance records for multiple students on a specific occurrence.
    /// Returns a set of TeacherStudentIds that already have attendance.
    /// </summary>
    Task<HashSet<long>> GetExistingAttendanceBatchAsync(
        IEnumerable<long> teacherStudentIds, long sessionOccurrenceId);

    /// <summary>
    /// Step 2.1: Batch cross-session duplicate check for multiple students on a date.
    /// Returns a dictionary mapping TeacherStudentId to the existing record (if any)
    /// across all linked sessions for the given date.
    /// Used by BulkMarkAttendanceAsync to detect cross-session duplicates without N+1.
    /// </summary>
    Task<Dictionary<long, AttendanceRecord>> GetExistingAttendanceByStudentsAndDateAsync(
        IEnumerable<long> teacherStudentIds, DateTime occurrenceDate, IEnumerable<long> linkedSessionIds);

    /// <summary>
    /// Step 3.1: Gets a held record for a student on a specific occurrence.
    /// REQ-ATT-061: Find held records to release them.
    /// </summary>
    Task<AttendanceRecord?> GetHeldRecordAsync(long teacherStudentId, long sessionOccurrenceId);

    /// <summary>
    /// Gets all attendance records for a session occurrence.
    /// REQ-ATT-008: Which students are marked vs. unmarked.
    /// </summary>
    Task<IReadOnlyList<AttendanceRecord>> GetRecordsByOccurrenceAsync(long sessionOccurrenceId);

    /// <summary>
    /// Gets all attendance records for a student within a specific assignment period.
    /// </summary>
    Task<IReadOnlyList<AttendanceRecord>> GetRecordsByAssignmentAsync(long studentSessionAssignmentId);

    /// <summary>
    /// Gets all attendance records for a student within a date range, across all sessions.
    /// </summary>
    Task<IReadOnlyList<AttendanceRecord>> GetRecordsByStudentAndDateRangeAsync(
        long teacherStudentId, DateTime startDate, DateTime endDate);

    /// <summary>
    /// Gets the last N attendance records for a student, ordered by OccurrenceDate descending.
    /// REQ-ATT-068: Compact visual indicator of last 5 session occurrences.
    /// </summary>
    Task<IReadOnlyList<AttendanceRecord>> GetRecentRecordsByStudentAsync(
        long teacherStudentId, int count);

    /// <summary>
    /// Counts attendance records for a session occurrence by status.
    /// </summary>
    Task<int> CountRecordsByOccurrenceAndStatusAsync(long sessionOccurrenceId, AttendanceStatus status);

    /// <summary>
    /// Gets attendance records for a session across a date range.
    /// Step 5.2: Includes teacherId guard for defense-in-depth.
    /// </summary>
    Task<IReadOnlyList<AttendanceRecord>> GetRecordsBySessionAndDateRangeAsync(
        long teacherId, long sessionId, DateTime startDate, DateTime endDate);

    /// <summary>
    /// Gets all attendance records for a teacher on a specific date.
    /// </summary>
    Task<IReadOnlyList<AttendanceRecord>> GetRecordsByTeacherAndDateAsync(
        long teacherId, DateTime date);

    /// <summary>
    /// Step 1.2: Nullifies SessionOccurrenceId on all records for a session's occurrences
    /// using ExecuteUpdateAsync — single SQL UPDATE, no in-memory loading.
    /// </summary>
    Task NullifyOccurrenceReferencesForSessionAsync(long sessionId);

    /// <summary>
    /// Step 1.2: Nullifies the denormalized SessionId on all AttendanceRecords for a session
    /// using ExecuteUpdateAsync — single SQL UPDATE, no in-memory loading.
    /// </summary>
    Task NullifySessionIdOnRecordsForSessionAsync(long sessionId);

    /// <summary>
    /// Step 1.1: Nullifies TeacherStudentId and StudentSessionAssignmentId on all
    /// AttendanceRecords for a student using ExecuteUpdateAsync.
    /// Called during permanent student purge. Denormalized StudentName/StudentCode remain intact.
    /// </summary>
    Task NullifyStudentReferencesOnRecordsAsync(long teacherStudentId);

    // ══════════════════════════════════════════════
    // ATTENDANCE EDIT LOG QUERIES
    // ══════════════════════════════════════════════

    /// <summary>
    /// Adds a new edit log entry.
    /// REQ-ATT-025: Logged as modification alongside the original entry.
    /// </summary>
    Task AddEditLogAsync(AttendanceEditLog editLog);

    /// <summary>
    /// Gets all edit logs for a specific attendance record.
    /// </summary>
    Task<IReadOnlyList<AttendanceEditLog>> GetEditLogsByRecordAsync(long attendanceRecordId);

    // ══════════════════════════════════════════════
    // STUDENT ABSENCE COUNTER QUERIES
    // ══════════════════════════════════════════════

    /// <summary>
    /// Adds a new student absence counter.
    /// </summary>
    Task AddAbsenceCounterAsync(StudentAbsenceCounter counter);

    /// <summary>
    /// Updates an existing student absence counter.
    /// </summary>
    Task UpdateAbsenceCounterAsync(StudentAbsenceCounter counter);

    /// <summary>
    /// Step 4.1: Batch updates multiple absence counters in a single SaveChanges.
    /// Used by BulkMarkAttendanceAsync to avoid N individual update calls.
    /// </summary>
    Task UpdateAbsenceCountersRangeAsync(IEnumerable<StudentAbsenceCounter> counters);

    /// <summary>
    /// Gets the absence counter for a specific student under a teacher.
    /// </summary>
    Task<StudentAbsenceCounter?> GetAbsenceCounterAsync(long teacherId, long teacherStudentId);

    /// <summary>
    /// Gets absence counters for multiple students in a single query.
    /// Returns a dictionary keyed by TeacherStudentId.
    /// </summary>
    Task<Dictionary<long, StudentAbsenceCounter>> GetAbsenceCountersBatchAsync(
        long teacherId, IEnumerable<long> teacherStudentIds);

    /// <summary>
    /// Gets absence counters for all students in a session.
    /// </summary>
    Task<IReadOnlyList<StudentAbsenceCounter>> GetAbsenceCountersBySessionAsync(long sessionId);

    /// <summary>
    /// Recalculates ConsecutiveAbsences from actual attendance records.
    /// Step 2.2: Excludes Held status, uses configurable scan depth.
    /// </summary>
    Task<int> RecalculateConsecutiveAbsencesAsync(long teacherStudentId);

    /// <summary>
    /// Deletes absence counter for a student.
    /// </summary>
    Task DeleteAbsenceCounterAsync(StudentAbsenceCounter counter);

    /// <summary>
    /// Deletes all absence counters for a student across all teachers.
    /// </summary>
    Task DeleteAbsenceCountersByStudentAsync(long teacherStudentId);

    // ══════════════════════════════════════════════
    // PAGED ABSENCE OVERVIEW
    // ══════════════════════════════════════════════

    /// <summary>
    /// Counts students in the absence overview query.
    /// </summary>
    Task<int> CountAbsenceOverviewAsync(
        long teacherId,
        long? sessionId = null,
        string? search = null,
        bool? missingStudentPhone = null,
        bool? missingParentPhone = null);

    /// <summary>
    /// Returns a paged list of StudentAbsenceCounter with TeacherStudent included.
    /// REQ-ATT-032/067: Sorted by ConsecutiveAbsences DESC.
    /// </summary>
    Task<IReadOnlyList<StudentAbsenceCounter>> GetPagedAbsenceOverviewAsync(
        long teacherId,
        int page,
        int pageSize,
        long? sessionId = null,
        string? search = null,
        bool? missingStudentPhone = null,
        bool? missingParentPhone = null);

    /// <summary>
    /// Executes an attendance report query.
    /// Step 6.1: Supports all 6 report types including session group and linked sessions.
    /// </summary>
    Task<IReadOnlyList<AttendanceRecord>> ExecuteReportQueryAsync(
        long teacherId,
        long? sessionId = null,
        long? sessionGroupId = null,
        DateTime? startDate = null,
        DateTime? endDate = null,
        AttendanceStatus? status = null,
        long? teacherStudentId = null,
        IEnumerable<long>? sessionIds = null);

    /// <summary>
    /// Returns distinct student Ids from the assignment list query.
    /// REQ-ATT-072: Student Attendance Timeline.
    /// </summary>
    Task<IReadOnlyList<long>> GetDistinctStudentIdsFromAssignmentsAsync(
        long teacherId,
        long? sessionId = null,
        long? sessionGroupId = null,
        string? studentName = null,
        string? studentCode = null);

    // ══════════════════════════════════════════════
    // PAGED ATTENDANCE STUDENT LIST (FIX 1.2)
    // ══════════════════════════════════════════════

    /// <summary>
    /// Retrieves the paginated student list for Take Attendance entirely in the database.
    /// REQ-ATT-008/014/015/036/054.
    /// </summary>
    Task<(IReadOnlyList<PagedAttendanceStudentRow> Items, int TotalCount)> GetPagedAttendanceStudentListAsync(
        long teacherId, long sessionId, DateTime occurrenceDate,
        IEnumerable<long> linkedSessionIds,
        string? search, bool unmarkedOnly,
        int page, int pageSize);
}

/// <summary>
/// Projection model returned by GetPagedAttendanceStudentListAsync.
/// Lives in the Domain layer alongside the repo interface since it's a query projection.
/// </summary>
public class PagedAttendanceStudentRow
{
    public long TeacherStudentId { get; set; }
    public string StudentName { get; set; } = null!;
    public string StudentCode { get; set; } = null!;
    public long? SessionId { get; set; }
    public string? SessionName { get; set; }
    public bool IsFromLinkedSession { get; set; }
    public string? SourceSessionName { get; set; }
    public bool IsMarked { get; set; }
    public AttendanceStatus? CurrentStatus { get; set; }
    public int ConsecutiveAbsences { get; set; }
    public int TotalAbsences { get; set; }
}