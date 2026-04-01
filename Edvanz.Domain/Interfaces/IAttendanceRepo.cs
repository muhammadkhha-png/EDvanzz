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
    /// FIX 3.1: Used for cross-session attendance date remapping (REQ-ATT-018).
    /// When a student from Session B attends Session A, the attendance date must be
    /// remapped to Session B's next occurrence date.
    /// </summary>
    Task<SessionOccurrence?> GetNextOccurrenceAsync(long sessionId, DateTime onOrAfterDate);

    /// <summary>
    /// Gets the latest (most recent date) occurrence for a session.
    /// Used to determine if new occurrences need to be generated.
    /// </summary>
    Task<SessionOccurrence?> GetLatestOccurrenceBySessionAsync(long sessionId);

    /// <summary>
    /// FIX 6.2: Gets only the occurrence dates (not full entities) for a session.
    /// More efficient than GetOccurrencesBySessionAsync when only dates are needed
    /// (e.g., during GenerateOccurrencesAsync duplicate checking).
    /// </summary>
    Task<HashSet<DateTime>> GetExistingOccurrenceDatesAsync(long sessionId);

    // ══════════════════════════════════════════════
    // BATCH OCCURRENCE COUNTING (FIX 2.1)
    // ══════════════════════════════════════════════

    /// <summary>
    /// FIX 2.1: Counts attendance records grouped by occurrence Id in a single query.
    /// Replaces the N+1 pattern in GetOccurrenceCalendarAsync where each occurrence
    /// was individually queried for its record count.
    /// </summary>
    Task<Dictionary<long, int>> CountRecordsByOccurrenceBatchAsync(IEnumerable<long> occurrenceIds);

    // ══════════════════════════════════════════════
    // STUDENT SESSION ASSIGNMENT QUERIES
    // ══════════════════════════════════════════════

    /// <summary>
    /// Adds a new student session assignment.
    /// Created when a student is assigned to a session.
    /// REQ-ATT-019/045: New attendance timeline starts from AssignedAt.
    /// </summary>
    Task AddAssignmentAsync(StudentSessionAssignment assignment);

    /// <summary>
    /// Updates an existing student session assignment.
    /// Used when setting UnassignedAt during reassignment.
    /// </summary>
    Task UpdateAssignmentAsync(StudentSessionAssignment assignment);

    /// <summary>
    /// Gets the currently active assignment for a student (where IsActive = true).
    /// Returns null if the student is not currently assigned to any session.
    /// </summary>
    Task<StudentSessionAssignment?> GetActiveAssignmentAsync(long teacherStudentId);

    /// <summary>
    /// Gets all assignment records for a student across all sessions, ordered by AssignedAt ascending.
    /// REQ-ATT-022/046: Unified student attendance profile showing all session periods.
    /// REQ-ATT-074/076: Full attendance timeline across all session changes.
    /// </summary>
    Task<IReadOnlyList<StudentSessionAssignment>> GetAssignmentsByStudentAsync(long teacherStudentId);

    /// <summary>
    /// Gets all currently active assignments for a specific session.
    /// Used to build the student list for Take Attendance screen.
    /// REQ-ATT-008: Which students are assigned to this session.
    /// </summary>
    Task<IReadOnlyList<StudentSessionAssignment>> GetActiveAssignmentsBySessionAsync(long sessionId);

    /// <summary>
    /// Deactivates all active assignments for a session.
    /// Called before session hard-delete.
    /// Sets IsActive = false and UnassignedAt = now, also nullifies SessionId on each record.
    /// </summary>
    Task DeactivateAssignmentsBySessionAsync(long sessionId);

    /// <summary>
    /// FIX 1.1: Deactivates all active assignments for a student across all sessions.
    /// Called during permanent student purge to close any open assignment periods.
    /// Sets IsActive = false and UnassignedAt = now.
    /// </summary>
    Task DeactivateAssignmentsByStudentAsync(long teacherStudentId);

    // ══════════════════════════════════════════════
    // BATCH ASSIGNMENT QUERIES (FIX 2.2)
    // ══════════════════════════════════════════════

    /// <summary>
    /// FIX 2.2: Gets active assignments for multiple students in a single query.
    /// Used by BulkMarkAttendanceAsync to eliminate the N+1 pattern where each student's
    /// assignment was individually looked up inside the loop.
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
    /// Used for edit and delete operations.
    /// </summary>
    Task<AttendanceRecord?> GetAttendanceRecordByIdAsync(long recordId, long teacherId);

    /// <summary>
    /// Checks if attendance already exists for a student on a specific occurrence.
    /// BR-ATT-002: Duplicate prevention — one attendance per student per occurrence per day.
    /// REQ-ATT-069: Checks both the current session and all membership-linked sessions.
    /// </summary>
    Task<AttendanceRecord?> GetExistingAttendanceAsync(long teacherStudentId, long sessionOccurrenceId);

    /// <summary>
    /// Checks if attendance already exists for a student on any linked session for the same date.
    /// REQ-ATT-069/070: Cross-session duplicate detection across membership-linked sessions.
    /// </summary>
    Task<AttendanceRecord?> GetExistingAttendanceByStudentAndDateAsync(
        long teacherStudentId, DateTime occurrenceDate, IEnumerable<long> linkedSessionIds);

    /// <summary>
    /// FIX 2.2: Checks for existing attendance records for multiple students on a specific occurrence.
    /// Returns a set of TeacherStudentIds that already have attendance for this occurrence.
    /// Eliminates the N+1 pattern in BulkMarkAttendanceAsync.
    /// </summary>
    Task<HashSet<long>> GetExistingAttendanceBatchAsync(
        IEnumerable<long> teacherStudentIds, long sessionOccurrenceId);

    /// <summary>
    /// Gets all attendance records for a session occurrence.
    /// REQ-ATT-008: Take Attendance screen — which students are marked vs. unmarked.
    /// REQ-ATT-050: Live counter "18 / 34" on session cards.
    /// </summary>
    Task<IReadOnlyList<AttendanceRecord>> GetRecordsByOccurrenceAsync(long sessionOccurrenceId);

    /// <summary>
    /// Gets all attendance records for a student within a specific assignment period.
    /// REQ-ATT-020: Separate histories per assignment period.
    /// REQ-ATT-075: Timeline organized by month per assignment period.
    /// </summary>
    Task<IReadOnlyList<AttendanceRecord>> GetRecordsByAssignmentAsync(long studentSessionAssignmentId);

    /// <summary>
    /// Gets all attendance records for a student within a date range, across all sessions.
    /// REQ-ATT-074: Full attendance timeline for a student.
    /// REQ-ATT-079: Month-by-month loading (pass month start/end as date range).
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
    /// REQ-ATT-050: Live present/absent/unmarked count on session cards.
    /// REQ-ATT-053: Sticky header with live counts.
    /// </summary>
    Task<int> CountRecordsByOccurrenceAndStatusAsync(long sessionOccurrenceId, AttendanceStatus status);

    /// <summary>
    /// Gets attendance records for a session across a date range, for reporting.
    /// REQ-ATT-040 Report Type 4: Full attendance history for a session.
    /// </summary>
    Task<IReadOnlyList<AttendanceRecord>> GetRecordsBySessionAndDateRangeAsync(
        long sessionId, DateTime startDate, DateTime endDate);

    /// <summary>
    /// Gets all attendance records for a teacher across all sessions for a specific date.
    /// REQ-ATT-040 Report Type 3: All Sessions Absence Report.
    /// </summary>
    Task<IReadOnlyList<AttendanceRecord>> GetRecordsByTeacherAndDateAsync(
        long teacherId, DateTime date);

    /// <summary>
    /// Nullifies SessionOccurrenceId on all records referencing occurrences of a session.
    /// Called before session deletion to preserve records with denormalized data intact.
    /// BR-ATT-005: Records retained after session hard-delete.
    /// </summary>
    Task NullifyOccurrenceReferencesForSessionAsync(long sessionId);

    /// <summary>
    /// FIX 1.4: Nullifies the denormalized SessionId on all AttendanceRecords for a session.
    /// Called before session hard-delete. The denormalized SessionName and OccurrenceDate
    /// remain intact so records are still self-describing after deletion.
    /// Without this, AttendanceRecord.SessionId would point to a nonexistent session row,
    /// causing silent JOIN failures in reporting queries.
    /// </summary>
    Task NullifySessionIdOnRecordsForSessionAsync(long sessionId);

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
    /// REQ-ATT-025: Audit trail — differentiates original from modified records.
    /// </summary>
    Task<IReadOnlyList<AttendanceEditLog>> GetEditLogsByRecordAsync(long attendanceRecordId);

    // ══════════════════════════════════════════════
    // STUDENT ABSENCE COUNTER QUERIES
    // ══════════════════════════════════════════════

    /// <summary>
    /// Adds a new student absence counter (initialized with all zeros).
    /// Created when a student is first assigned to a session.
    /// </summary>
    Task AddAbsenceCounterAsync(StudentAbsenceCounter counter);

    /// <summary>
    /// Updates an existing student absence counter.
    /// Updated transactionally with each attendance record insert/edit.
    /// </summary>
    Task UpdateAbsenceCounterAsync(StudentAbsenceCounter counter);

    /// <summary>
    /// Gets the absence counter for a specific student under a teacher.
    /// REQ-ATT-028/029: Checked on every attendance mark for alert logic.
    /// O(1) lookup via unique index (TeacherId, TeacherStudentId).
    /// </summary>
    Task<StudentAbsenceCounter?> GetAbsenceCounterAsync(long teacherId, long teacherStudentId);

    /// <summary>
    /// FIX 2.2: Gets absence counters for multiple students in a single query.
    /// Eliminates the N+1 pattern in BulkMarkAttendanceAsync and GetAttendanceStudentListAsync.
    /// Returns a dictionary keyed by TeacherStudentId for O(1) lookup.
    /// </summary>
    Task<Dictionary<long, StudentAbsenceCounter>> GetAbsenceCountersBatchAsync(
        long teacherId, IEnumerable<long> teacherStudentIds);

    /// <summary>
    /// Gets absence counters for all students in a session (via their active assignments).
    /// REQ-ATT-032: Absence Overview — all absent students with consecutive counts.
    /// </summary>
    Task<IReadOnlyList<StudentAbsenceCounter>> GetAbsenceCountersBySessionAsync(long sessionId);

    /// <summary>
    /// Recalculates ConsecutiveAbsences from the actual attendance records.
    /// Used after edit operations where we cannot use simple increment/decrement.
    /// Scans recent records in reverse-chronological order until a Present is found.
    /// </summary>
    Task<int> RecalculateConsecutiveAbsencesAsync(long teacherStudentId);

    /// <summary>
    /// Deletes absence counter for a student.
    /// Called during permanent student purge.
    /// </summary>
    Task DeleteAbsenceCounterAsync(StudentAbsenceCounter counter);

    /// <summary>
    /// FIX 1.1: Deletes all absence counters for a student across all teachers.
    /// Called during permanent student purge when the teacherId is not known
    /// or when cleaning up counters across all teacher contexts.
    /// </summary>
    Task DeleteAbsenceCountersByStudentAsync(long teacherStudentId);

    // ══════════════════════════════════════════════
    // PAGED ABSENCE OVERVIEW (avoids EF Core in Application layer)
    // ══════════════════════════════════════════════

    /// <summary>
    /// Counts students in the absence overview query.
    /// Executes the filtered query and returns the distinct student count.
    /// Used by GetAbsenceOverviewAsync for pagination total.
    /// </summary>
    Task<int> CountAbsenceOverviewAsync(
        long teacherId,
        long? sessionId = null,
        string? search = null,
        bool? missingStudentPhone = null,
        bool? missingParentPhone = null);

    /// <summary>
    /// Returns a paged list of StudentAbsenceCounter with TeacherStudent included.
    /// Executes the filtered, sorted query with Skip/Take in the Infrastructure layer
    /// so the Application layer never calls EF Core methods directly.
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
    /// Executes an attendance report query and returns records with TeacherStudent included.
    /// Moves the .Include() + .ToListAsync() call to Infrastructure layer
    /// so the Application layer does not depend on Microsoft.EntityFrameworkCore.
    /// REQ-ATT-040: Report generation.
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
    /// Executes .Select().Distinct().ToListAsync() in Infrastructure layer
    /// so the Application layer does not depend on Microsoft.EntityFrameworkCore.
    /// REQ-ATT-072: Student Attendance Timeline — all students regardless of session.
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
    /// FIX 1.2: Retrieves the paginated student list for Take Attendance / Edit Attendance
    /// entirely in the database (no in-memory loading of 50K students).
    /// Combines primary session students and linked session students, applies search/filter,
    /// orders by marked status (unmarked first per REQ-ATT-054), and applies SKIP/TAKE.
    ///
    /// REQ-ATT-008: Marked vs. unmarked display.
    /// REQ-ATT-014/015: Includes linked session students.
    /// REQ-ATT-036: Supports up to 50K students per session.
    /// REQ-ATT-054: Unmarked students displayed first.
    /// </summary>
    /// <param name="teacherId">Teacher Id for tenant scoping.</param>
    /// <param name="sessionId">The primary session Id.</param>
    /// <param name="occurrenceDate">The occurrence date to check attendance for.</param>
    /// <param name="linkedSessionIds">Ids of sessions linked to the primary session via membership.</param>
    /// <param name="search">Optional search term for student name/code.</param>
    /// <param name="unmarkedOnly">If true, only return students without attendance for this occurrence.</param>
    /// <param name="page">Page number (1-based).</param>
    /// <param name="pageSize">Number of records per page.</param>
    /// <returns>Tuple of (paged student rows with attendance info, total count).</returns>
    Task<(IReadOnlyList<PagedAttendanceStudentRow> Items, int TotalCount)> GetPagedAttendanceStudentListAsync(
        long teacherId, long sessionId, DateTime occurrenceDate,
        IEnumerable<long> linkedSessionIds,
        string? search, bool unmarkedOnly,
        int page, int pageSize);
}

/// <summary>
/// FIX 1.2: Projection model returned by GetPagedAttendanceStudentListAsync.
/// Contains all fields needed to build AttendanceStudentRowDto without additional queries.
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