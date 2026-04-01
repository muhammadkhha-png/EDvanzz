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
    /// Gets all currently active assignments for a teacher, scoped by teacher.
    /// REQ-ATT-072: Student Attendance Timeline view — all students regardless of session.
    /// </summary>
    IQueryable<StudentSessionAssignment> BuildAssignmentListQuery(
        long teacherId,
        long? sessionId = null,
        long? sessionGroupId = null,
        string? studentName = null,
        string? studentCode = null);

    /// <summary>
    /// Deactivates all active assignments for a session.
    /// Called before session hard-delete.
    /// Sets IsActive = false and UnassignedAt = now, also nullifies SessionId on each record.
    /// </summary>
    Task DeactivateAssignmentsBySessionAsync(long sessionId);

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
    /// Builds a queryable for attendance records filtered by teacher, session, and date range.
    /// Used for paginated report generation (REQ-ATT-040).
    /// </summary>
    IQueryable<AttendanceRecord> BuildAttendanceReportQuery(
        long teacherId,
        long? sessionId = null,
        long? sessionGroupId = null,
        DateTime? startDate = null,
        DateTime? endDate = null,
        AttendanceStatus? status = null);

    /// <summary>
    /// Nullifies SessionOccurrenceId on all records referencing occurrences of a session.
    /// Called before session deletion to preserve records with denormalized data intact.
    /// BR-ATT-005: Records retained after session hard-delete.
    /// </summary>
    Task NullifyOccurrenceReferencesForSessionAsync(long sessionId);

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
    /// Gets absence counters for all students in a session (via their active assignments).
    /// REQ-ATT-032: Absence Overview — all absent students with consecutive counts.
    /// </summary>
    Task<IReadOnlyList<StudentAbsenceCounter>> GetAbsenceCountersBySessionAsync(long sessionId);

    /// <summary>
    /// Builds a queryable for absence counters scoped to a teacher, for paginated absence overview.
    /// REQ-ATT-032/067: Sorted by ConsecutiveAbsences DESC by default.
    /// REQ-ATT-034: Supports search by student name/code and filters.
    /// </summary>
    IQueryable<StudentAbsenceCounter> BuildAbsenceOverviewQuery(
        long teacherId,
        long? sessionId = null,
        string? search = null,
        bool? missingStudentPhone = null,
        bool? missingParentPhone = null);

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

    // ══════════════════════════════════════════════
    // OCCURRENCE GENERATION SUPPORT
    // ══════════════════════════════════════════════

    /// <summary>
    /// Gets the latest (most recent date) occurrence for a session.
    /// Used to determine if new occurrences need to be generated.
    /// </summary>
    Task<SessionOccurrence?> GetLatestOccurrenceBySessionAsync(long sessionId);

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
}