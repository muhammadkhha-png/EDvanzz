using Edvanz.Domain.Entities;
using Edvanz.Domain.Enums;

namespace Edvanz.Domain.Interfaces;

/// <summary>
/// Extended repository interface for the Attendance Module (Module 3).
/// Centralizes all domain-specific query methods for SessionOccurrence,
/// StudentSessionAssignment, AttendanceRecord, AttendanceEditLog, and
/// StudentAbsenceCounter records.
/// 
/// WHY THIS EXISTS (same rationale as ITeacherStudentRepo and ISessionRepo):
/// All query logic lives here in named methods. The Application layer never
/// builds raw expression predicates. If a query changes, you edit ONE method
/// in the repo — not every service that uses it.
/// 
/// Inherits from IGenericRepo&lt;AttendanceRecord, long&gt; so basic CRUD is still available
/// for AttendanceRecord entities. Other entity types are accessed via dedicated methods.
/// </summary>
public interface IAttendanceRepo : IGenericRepo<AttendanceRecord, long>
{
    // ══════════════════════════════════════════════
    // SESSION OCCURRENCE QUERIES
    // ══════════════════════════════════════════════

    /// <summary>
    /// Retrieves all occurrences for a session, ordered by OccurrenceIndex ascending.
    /// Used for generating occurrence lists and calendar views (REQ-ATT-065).
    /// </summary>
    Task<IReadOnlyList<SessionOccurrence>> GetOccurrencesBySessionAsync(long sessionId);

    /// <summary>
    /// Finds a specific occurrence by session and date.
    /// REQ-ATT-001/002: Core lookup for "does today match a scheduled occurrence?"
    /// Returns null if no occurrence exists for that date.
    /// </summary>
    Task<SessionOccurrence?> GetOccurrenceBySessionAndDateAsync(long sessionId, DateTime date);

    /// <summary>
    /// Finds a specific occurrence by its Id.
    /// </summary>
    Task<SessionOccurrence?> GetOccurrenceByIdAsync(long occurrenceId);

    /// <summary>
    /// Retrieves all session occurrences that fall on a specific date across all sessions
    /// for a teacher. Used for the daily dashboard (REQ-ATT-049/052).
    /// </summary>
    Task<IReadOnlyList<SessionOccurrence>> GetOccurrencesByDateAndTeacherAsync(long teacherId, DateTime date);

    /// <summary>
    /// Finds the previous occurrence relative to a given occurrence index within the same session.
    /// REQ-ATT-027: Check attendance for the immediately preceding occurrence.
    /// Returns null if the given occurrence is the first one (index 0).
    /// </summary>
    Task<SessionOccurrence?> GetPreviousOccurrenceAsync(long sessionId, int currentOccurrenceIndex);

    /// <summary>
    /// Bulk-adds session occurrences. Used when a session is created or its schedule changes.
    /// </summary>
    Task AddOccurrencesAsync(IEnumerable<SessionOccurrence> occurrences);

    /// <summary>
    /// Deletes all occurrences for a session. Used before regenerating when schedule changes.
    /// Only deletes occurrences that have NO attendance records to prevent data loss.
    /// Returns the count of occurrences that could not be deleted (have attendance).
    /// </summary>
    Task<int> DeleteUnusedOccurrencesAsync(long sessionId);

    // ══════════════════════════════════════════════
    // STUDENT SESSION ASSIGNMENT QUERIES
    // ══════════════════════════════════════════════

    /// <summary>
    /// Retrieves the active (current) session assignment for a student.
    /// Returns null if the student has no active assignment.
    /// REQ-ATT-019: Attendance begins from the assignment date.
    /// </summary>
    Task<StudentSessionAssignment?> GetActiveAssignmentAsync(long teacherStudentId);

    /// <summary>
    /// Retrieves all assignment periods for a student (active and historical), ordered by AssignedAt.
    /// REQ-ATT-046: Chronological timeline of all assignment periods.
    /// REQ-ATT-022: Unified student attendance profile.
    /// </summary>
    Task<IReadOnlyList<StudentSessionAssignment>> GetAssignmentsByStudentAsync(long teacherStudentId);

    /// <summary>
    /// Retrieves all active assignments for a specific session.
    /// Used for the Take Attendance student list (REQ-ATT-053/054).
    /// </summary>
    Task<IReadOnlyList<StudentSessionAssignment>> GetActiveAssignmentsBySessionAsync(long sessionId);

    /// <summary>
    /// Retrieves all active assignments for multiple sessions (batch).
    /// Used for the cross-session secondary panel (REQ-ATT-014/015).
    /// </summary>
    Task<IReadOnlyList<StudentSessionAssignment>> GetActiveAssignmentsBySessionsAsync(IEnumerable<long> sessionIds);

    /// <summary>
    /// Adds a new student session assignment.
    /// </summary>
    Task AddAssignmentAsync(StudentSessionAssignment assignment);

    /// <summary>
    /// Updates an existing assignment (e.g., setting UnassignedAt and IsActive on reassignment).
    /// </summary>
    Task UpdateAssignmentAsync(StudentSessionAssignment assignment);

    // ══════════════════════════════════════════════
    // ATTENDANCE RECORD QUERIES
    // ══════════════════════════════════════════════

    /// <summary>
    /// Checks if an attendance record already exists for a student at a specific occurrence.
    /// BR-ATT-002: Prevents duplicate attendance per student per occurrence.
    /// REQ-ATT-069/070: Duplicate detection including cross-session records.
    /// </summary>
    Task<AttendanceRecord?> GetRecordByOccurrenceAndStudentAsync(long sessionOccurrenceId, long teacherStudentId);

    /// <summary>
    /// Checks if a student has already been marked present for any occurrence on a given date
    /// across all membership-linked sessions. Used for cross-session duplicate detection.
    /// REQ-ATT-069: Checks across all membership-linked sessions for the same occurrence date.
    /// Returns the existing record if found, null otherwise.
    /// </summary>
    Task<AttendanceRecord?> GetRecordByDateAndStudentAcrossLinkedSessionsAsync(
        long teacherStudentId, DateTime occurrenceDate, IEnumerable<long> linkedSessionIds);

    /// <summary>
    /// Retrieves all attendance records for a specific session occurrence.
    /// Used for the Take Attendance screen (REQ-ATT-008/053).
    /// </summary>
    Task<IReadOnlyList<AttendanceRecord>> GetRecordsByOccurrenceAsync(long sessionOccurrenceId);

    /// <summary>
    /// Retrieves all attendance records within a specific assignment period.
    /// REQ-ATT-044: Independently viewable per assignment period.
    /// </summary>
    Task<IReadOnlyList<AttendanceRecord>> GetRecordsByAssignmentAsync(long studentSessionAssignmentId);

    /// <summary>
    /// Retrieves all attendance records for a student across all assignments, ordered by date.
    /// REQ-ATT-074/076: Full attendance timeline from first assignment to current date.
    /// </summary>
    Task<IReadOnlyList<AttendanceRecord>> GetRecordsByStudentAsync(long teacherStudentId);

    /// <summary>
    /// Retrieves attendance records for a student within a specific month.
    /// REQ-ATT-075/079: Timeline organized by month with lazy loading.
    /// </summary>
    Task<IReadOnlyList<AttendanceRecord>> GetRecordsByStudentAndMonthAsync(
        long teacherStudentId, int year, int month);

    /// <summary>
    /// Counts the total attendance records and absences for a student across all time.
    /// REQ-ATT-078: All-time summary (total occurrences, total absences, percentage).
    /// Returns (totalRecords, totalAbsences).
    /// </summary>
    Task<(int TotalRecords, int TotalAbsences)> GetStudentAllTimeSummaryAsync(long teacherStudentId);

    /// <summary>
    /// Counts attendance records and absences for a student within a specific month.
    /// REQ-ATT-077: Monthly summary (occurrences, present, absences, percentage).
    /// Returns (totalRecords, totalAbsences).
    /// </summary>
    Task<(int TotalRecords, int TotalAbsences)> GetStudentMonthlySummaryAsync(
        long teacherStudentId, int year, int month);

    /// <summary>
    /// Builds a filtered, sortable IQueryable for attendance records within a session.
    /// Used for the session attendance report (REQ-ATT-040 Report Type 4).
    /// Returns IQueryable for pagination support.
    /// </summary>
    IQueryable<AttendanceRecord> BuildSessionAttendanceQuery(long sessionId);

    /// <summary>
    /// Retrieves the attendance status counts for a specific occurrence.
    /// Returns (presentCount, absentCount, heldCount) for dashboard display.
    /// REQ-ATT-050: Live counter on session cards (e.g., "18 / 34").
    /// </summary>
    Task<(int Present, int Absent, int Held)> GetOccurrenceStatusCountsAsync(long sessionOccurrenceId);

    // ══════════════════════════════════════════════
    // ATTENDANCE EDIT LOG QUERIES
    // ══════════════════════════════════════════════

    /// <summary>
    /// Retrieves the edit history for a specific attendance record.
    /// REQ-ATT-025: Differentiates original records from modified records.
    /// </summary>
    Task<IReadOnlyList<AttendanceEditLog>> GetEditLogsByRecordAsync(long attendanceRecordId);

    /// <summary>
    /// Adds a new edit log entry.
    /// </summary>
    Task AddEditLogAsync(AttendanceEditLog editLog);

    // ══════════════════════════════════════════════
    // STUDENT ABSENCE COUNTER QUERIES
    // ══════════════════════════════════════════════

    /// <summary>
    /// Retrieves the absence counter for a specific student.
    /// REQ-ATT-027/028: Quick check for absent-student alerts during attendance-taking.
    /// Returns null if no counter exists yet (student has never had attendance taken).
    /// </summary>
    Task<StudentAbsenceCounter?> GetAbsenceCounterByStudentAsync(long teacherStudentId);

    /// <summary>
    /// Retrieves absence counters for all students in a session, sorted by consecutive absences desc.
    /// REQ-ATT-032/067: Absence Overview panel sorted by most at-risk students first.
    /// </summary>
    Task<IReadOnlyList<StudentAbsenceCounter>> GetAbsenceCountersBySessionAsync(long sessionId);

    /// <summary>
    /// Retrieves absence counters for all students across multiple sessions (batch).
    /// REQ-ATT-033: Cross-session absence view for linked sessions.
    /// </summary>
    Task<IReadOnlyList<StudentAbsenceCounter>> GetAbsenceCountersBySessionsAsync(IEnumerable<long> sessionIds);

    /// <summary>
    /// Adds a new absence counter for a student.
    /// Created when the student first has attendance taken.
    /// </summary>
    Task AddAbsenceCounterAsync(StudentAbsenceCounter counter);

    /// <summary>
    /// Updates an existing absence counter.
    /// Called within the same transaction as each attendance write.
    /// </summary>
    Task UpdateAbsenceCounterAsync(StudentAbsenceCounter counter);

    /// <summary>
    /// Builds a filtered, searchable IQueryable for the Absence Overview panel.
    /// REQ-ATT-034: Supports searching by name/code and the same filters as the Student Module.
    /// REQ-ATT-067: Default sort by consecutive absences descending.
    /// </summary>
    /// <param name="sessionId">The session to show absent students for.</param>
    /// <param name="linkedSessionIds">Optional additional linked session IDs for cross-session view.</param>
    /// <param name="search">Optional search term (partial match on student name or code).</param>
    IQueryable<StudentAbsenceCounter> BuildAbsenceOverviewQuery(
        long sessionId,
        IEnumerable<long>? linkedSessionIds = null,
        string? search = null);
}