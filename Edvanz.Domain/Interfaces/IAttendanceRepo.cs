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
    /// FIX H4: Added phone filter parameters for REQ-ATT-034 compliance.
    /// </summary>
    Task<IReadOnlyList<AttendanceRecord>> GetAbsentStudentsByDateAsync(
        long teacherId, IEnumerable<long> sessionIds, DateTime occurrenceDate,
        string? search, int page, int pageSize,
        bool? missingStudentPhone = null, bool? missingParentPhone = null);

    /// <summary>
    /// Counts students who were absent on a specific date across given sessions.
    /// Audit Fix (REQ-ATT-035): Count variant for date-specific absence overview.
    /// FIX H4: Added phone filter parameters for REQ-ATT-034 compliance.
    /// </summary>
    Task<int> CountAbsentStudentsByDateAsync(
        long teacherId, IEnumerable<long> sessionIds, DateTime occurrenceDate,
        string? search,
        bool? missingStudentPhone = null, bool? missingParentPhone = null);

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

    // ── Cross-session equivalence ("weekly-slot position") ──────────────────────────────
    // Two occurrences of membership-linked sessions are the SAME logical slot iff they share
    // (WeekStartDate, DayPositionIndex). These helpers resolve equivalent occurrences so a
    // present mark on one linked session surfaces on every linked session's equivalent slot.

    /// <summary>
    /// The occurrence of <paramref name="sessionId"/> for a given equivalence slot
    /// (WeekStartDate + DayPositionIndex), or null when that session has no occurrence in the slot
    /// (e.g. a late-created session whose first partial week is missing early days).
    /// Used by the write path to remap a cross-session mark onto the student's home-session occurrence.
    /// </summary>
    Task<SessionOccurrence?> GetOccurrenceBySessionAndSlotAsync(
        long sessionId, DateTime weekStartDate, int dayPositionIndex);

    /// <summary>
    /// Resolves the equivalence slot of <paramref name="selectedSessionId"/>'s occurrence on
    /// <paramref name="date"/>, then returns the ids of every occurrence across the selected session and
    /// <paramref name="linkedSessionIds"/> that shares that slot. Empty when the selected session has no
    /// occurrence on the date. Drives the equivalence-aware read (take-list / edit-occurrence) and dedup.
    /// </summary>
    Task<IReadOnlyList<long>> GetEquivalentOccurrenceIdsAsync(
        long selectedSessionId, DateTime date, IEnumerable<long> linkedSessionIds);

    /// <summary>
    /// The student's first non-absent (Present/CrossSessionPresent) record on ANY occurrence in
    /// <paramref name="equivalentOccurrenceIds"/>, or null. Cross-session duplicate guard: a student
    /// already marked present on an equivalent slot of any linked session cannot be marked again.
    /// </summary>
    Task<AttendanceRecord?> GetExistingAttendanceOnEquivalentOccurrenceAsync(
        long teacherStudentId, IEnumerable<long> equivalentOccurrenceIds);

    /// <summary>
    /// Batch equivalence duplicate check for bulk marking: each student's existing record (if any) on
    /// an equivalent-slot occurrence in <paramref name="equivalentOccurrenceIds"/>, keyed by student id.
    /// </summary>
    Task<Dictionary<long, AttendanceRecord>> GetExistingAttendanceOnEquivalentOccurrenceBatchAsync(
        IEnumerable<long> teacherStudentIds, IEnumerable<long> equivalentOccurrenceIds);

    /// <summary>
    /// Records for the Edit-Attendance occurrence view: this occurrence's own marks PLUS cross-session
    /// visitors who physically attended THIS session on this date (their record lives on their home
    /// occurrence but is tagged with CrossSessionId = this session). Excludes a linked session's own
    /// roster (they attended their own class, not this one).
    /// </summary>
    Task<IReadOnlyList<AttendanceRecord>> GetRecordsForOccurrenceEditViewAsync(
        long sessionId, long occurrenceId, DateTime occurrenceDate);

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

    // ── Auto-absent nightly sweep (AttendanceAutoAbsentService) ──────────────────────────
    // Fan-out selection + candidate enumeration for the job that materializes Absent records
    // for students never marked once an occurrence's whole equivalence-slot window has passed.

    /// <summary>
    /// Distinct teacher ids that have at least one <see cref="SessionOccurrence"/> whose date is in
    /// [<paramref name="fromInclusive"/>, <paramref name="toExclusive"/>). Coarse fan-out selector for
    /// the auto-absent dispatcher, so it enqueues a worker only for teachers who actually held classes
    /// in the window (the worker re-gates precisely against each teacher's local date). Both bounds are
    /// compared on the date component.
    /// </summary>
    Task<IReadOnlyList<long>> GetTeacherIdsWithOccurrencesBetweenAsync(
        DateTime fromInclusive, DateTime toExclusive);

    /// <summary>
    /// A teacher's occurrences whose date is in [<paramref name="fromInclusive"/>,
    /// <paramref name="toExclusive"/>), ordered by date ascending, with <c>Session</c> included.
    /// Tracked (NOT AsNoTracking): the auto-absent worker refreshes each processed occurrence's
    /// <see cref="SessionOccurrence.Status"/>. This is the candidate set the worker sweeps.
    /// </summary>
    Task<IReadOnlyList<SessionOccurrence>> GetOccurrencesByTeacherAndDateRangeAsync(
        long teacherId, DateTime fromInclusive, DateTime toExclusive);

    /// <summary>
    /// The maximum occurrence date among <paramref name="sessionIds"/> for the equivalence slot
    /// (<paramref name="weekStartDate"/> + <paramref name="dayPositionIndex"/>), or null when none
    /// exists. The auto-absent worker passes the home session plus its linked sessions so it can hold
    /// off marking a student absent until the ENTIRE slot window has passed — linked sessions meeting
    /// on a later weekday (Sun≡Mon) still give the student their equivalent-attendance chance first.
    /// </summary>
    Task<DateTime?> GetMaxOccurrenceDateForSlotAsync(
        IEnumerable<long> sessionIds, DateTime weekStartDate, int dayPositionIndex);

    /// <summary>
    /// The tracked attendance records that <paramref name="teacherStudentIds"/> already have on
    /// <paramref name="sessionOccurrenceId"/> (any status). Tracked so the auto-absent worker can roll
    /// an unresolved <see cref="Edvanz.Domain.Enums.AttendanceStatus.Held"/> record forward to Absent
    /// in place. Empty list when none.
    /// </summary>
    Task<IReadOnlyList<AttendanceRecord>> GetRecordsByOccurrenceForStudentsAsync(
        long sessionOccurrenceId, IEnumerable<long> teacherStudentIds);

    /// <summary>
    /// TRACKED get-by-id (unlike <see cref="GetOccurrenceByIdAndTeacherAsync"/> which is AsNoTracking).
    /// Returns the already-tracked instance via identity resolution when one exists, else loads and
    /// tracks it. Used by the reconciliation flip/overwrite to refresh an occurrence's status WITHOUT
    /// risking a second, conflicting instance for an occurrence the mark pipeline already tracked (the
    /// home occurrence a cross-session mark was remapped onto). Null when not found for the teacher.
    /// </summary>
    Task<SessionOccurrence?> GetOccurrenceByIdTrackedAsync(long sessionOccurrenceId, long teacherId);

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
    /// Hard-deletes the specified occurrences by id (set-based DELETE, runs on the caller's transaction).
    /// Used when a session's recurrence is edited and its schedule is rebuilt (SES-1). ONLY
    /// pure-placeholder occurrences must be passed — any referencing row (attendance / exam anchor /
    /// per-session payment) would have its FK SET NULL, so history-bearing occurrences must be excluded
    /// by the caller. No-op for an empty set.
    /// </summary>
    Task DeleteOccurrencesByIdsAsync(IEnumerable<long> occurrenceIds);

    /// <summary>
    /// Retrieves occurrences for a session within a date range, ordered by date ascending.
    /// REQ-ATT-079: Month-by-month loading for student timeline.
    /// REQ-ATT-040: Report generation for specific date ranges.
    /// </summary>
    Task<IReadOnlyList<SessionOccurrence>> GetOccurrencesBySessionAndDateRangeAsync(
        long sessionId, DateTime startDate, DateTime endDate);

    /// <summary>
    /// Loads a single session occurrence by id, scoped to the teacher (tenant guard).
    /// Used by the Exams module to validate a chosen "during session" exam date and read its
    /// OccurrenceDate. Returns null if it does not exist or belongs to another teacher.
    /// </summary>
    Task<SessionOccurrence?> GetOccurrenceByIdAndTeacherAsync(long sessionOccurrenceId, long teacherId);

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
    /// Checks for existing attendance records for multiple students on a specific occurrence.
    /// Returns a set of TeacherStudentIds that already have attendance.
    /// </summary>
    Task<HashSet<long>> GetExistingAttendanceBatchAsync(
        IEnumerable<long> teacherStudentIds, long sessionOccurrenceId);

    /// <summary>
    /// Flushes the tracked changes but tolerates a CONCURRENT-INSERT race on the AttendanceRecords
    /// unique index — a second scanner (teacher + assistant scanning the same class at once) recording
    /// the same student on the same occurrence between our pre-check snapshot and this flush. Instead
    /// of letting that one unique-violation roll the WHOLE batch back (which left every scanned student
    /// unmarked → the night job then marked them Absent), it detaches only the Added
    /// <see cref="AttendanceRecord"/>(s) that now collide with an already-persisted
    /// (SessionOccurrenceId, TeacherStudentId) row and retries the flush with the rest, up to
    /// <paramref name="maxAttempts"/> times. Returns the TeacherStudentIds whose record was dropped as
    /// an already-recorded duplicate, so the caller reports them skipped and recomputes their absence
    /// counters from records. Any non-unique-violation DB error is rethrown unchanged.
    /// </summary>
    Task<IReadOnlyList<long>> FlushSkippingDuplicateRecordsAsync(int maxAttempts = 5);

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
    /// Computes the student's absence-counter totals directly from their attendance records
    /// (the single source of truth) in one query, excluding Held: TotalOccurrences = all non-Held
    /// records, TotalPresent = Present + CrossSessionPresent, TotalAbsences = Absent. Used to
    /// recompute the counter on edit/delete so the totals can never drift from the records (the old
    /// ±1 transition maintenance skewed them for cases it didn't enumerate). Returns (0,0,0) when the
    /// student has no records; by construction TotalOccurrences == TotalPresent + TotalAbsences.
    /// </summary>
    Task<(int TotalOccurrences, int TotalPresent, int TotalAbsences)> GetCounterAggregatesAsync(
        long teacherStudentId);

    /// <summary>Latest Absent record (date + denormalized session) and latest Present/
    /// CrossSessionPresent date for the student — recomputes the counter's Last* fields on
    /// an edit/delete so they don't point at a now-changed/deleted occurrence.</summary>
    Task<(DateTime? LastAbsenceDate, string? LastAbsenceSessionName, long? LastAbsenceSessionId, DateTime? LastAttendanceDate)>
        GetLastAbsenceAndAttendanceAsync(long teacherStudentId);

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
        bool? missingParentPhone = null,
        int minConsecutiveAbsences = 0);

    /// <summary>
    /// Returns a paged list of StudentAbsenceCounter with TeacherStudent included.
    /// REQ-ATT-032/067: Sorted by ConsecutiveAbsences DESC.
    /// <paramref name="minConsecutiveAbsences"/> (default 0 = no filter) keeps only students on an
    /// active absence streak &gt;= the threshold — the "absent students" violations view.
    /// </summary>
    Task<IReadOnlyList<StudentAbsenceCounter>> GetPagedAbsenceOverviewAsync(
        long teacherId,
        int page,
        int pageSize,
        long? sessionId = null,
        string? search = null,
        bool? missingStudentPhone = null,
        bool? missingParentPhone = null,
        int minConsecutiveAbsences = 0);

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
    /// REQ-ATT-008/014/015/036/054. Also returns AssignedCount (students in THIS session),
    /// NotAssignedCount (students shown from linked sessions) — the two split the result set and
    /// sum to TotalCount — and HoldCount (students whose current status on this occurrence is Held,
    /// across the whole filtered set, not just the page). Soft-deleted students are excluded
    /// (no more "Unknown" rows). Each row also carries the student's absence-counter snapshot
    /// (ConsecutiveAbsences / TotalAbsences / LastAbsenceDate / LastAbsenceSessionName).
    /// </summary>
    Task<(IReadOnlyList<PagedAttendanceStudentRow> Items, int TotalCount, int AssignedCount, int NotAssignedCount, int HoldCount)> GetPagedAttendanceStudentListAsync(
        long teacherId, long sessionId, DateTime occurrenceDate,
        IEnumerable<long> linkedSessionIds,
        string? search, bool unmarkedOnly,
        int page, int pageSize);

    // ══════════════════════════════════════════════
    // V2 AUDIT FIX — NEW BATCH METHODS
    // ══════════════════════════════════════════════

    /// <summary>
    /// FIX C1 (REQ-ATT-050): Counts active student assignments per session in a single query.
    /// Returns dictionary keyed by SessionId → count of active assignments.
    /// Used by GetDashboardAsync to populate TotalStudents on each session card.
    /// </summary>
    Task<Dictionary<long, int>> CountActiveAssignmentsBySessionBatchAsync(IEnumerable<long> sessionIds);

    /// <summary>
    /// FIX M3 (REQ-ATT-068): Batch-loads recent attendance statuses for multiple students.
    /// Returns dictionary keyed by TeacherStudentId → list of last N statuses.
    /// Replaces N+1 GetRecentRecordsByStudentAsync calls in absence overview loop.
    /// </summary>
    Task<Dictionary<long, IReadOnlyList<AttendanceStatus>>> GetRecentRecordsByStudentsBatchAsync(
        IEnumerable<long> teacherStudentIds, int count);

    /// <summary>
    /// FIX M4 (REQ-ATT-072): Returns paginated student IDs from assignments with DB-level Skip/Take.
    /// Previously all distinct IDs were loaded into memory and paginated with LINQ.
    /// Returns (pagedIds, totalCount) for efficient timeline pagination.
    /// </summary>
    Task<(IReadOnlyList<long> PagedIds, int TotalCount)> GetPagedTimelineStudentIdsAsync(
        long teacherId,
        int page,
        int pageSize,
        long? sessionId = null,
        long? sessionGroupId = null,
        string? studentName = null,
        string? studentCode = null);

    // ══════════════════════════════════════════════
    // V3 PERFORMANCE & TENANT FIX — NEW METHODS
    // ══════════════════════════════════════════════

    /// <summary>
    /// FIX R2: Fetches the absence counter bypassing the EF Core change tracker.
    /// Used in the concurrency retry loop so each attempt gets a fresh RowVersion
    /// instead of the stale tracked entity from the previous failed attempt.
    /// </summary>
    Task<StudentAbsenceCounter?> GetAbsenceCounterFreshAsync(long teacherId, long teacherStudentId);

    /// <summary>
    /// FIX P5: Batch-loads all assignments for multiple students in a single query.
    /// Returns dictionary keyed by TeacherStudentId → list of assignments.
    /// Replaces N+1 GetAssignmentsByStudentAsync calls in timeline student list.
    /// </summary>
    Task<Dictionary<long, IReadOnlyList<StudentSessionAssignment>>> GetAssignmentsByStudentsBatchAsync(
        IEnumerable<long> teacherStudentIds);

    // ══════════════════════════════════════════════
    // SESSION MONTH MATRIX (month-view screen)
    // ══════════════════════════════════════════════

    /// <summary>
    /// Pages a session's materialized occurrences within [monthStart, monthEndExclusive),
    /// ordered by date ascending. Feeds the month-matrix screen's occurrence columns.
    /// </summary>
    Task<(IReadOnlyList<SessionMonthOccurrenceRow> Items, int TotalCount)>
        GetPagedSessionMonthOccurrencesAsync(
            long sessionId, DateTime monthStart, DateTime monthEndExclusive,
            int page, int pageSize);

    /// <summary>
    /// Pages the students whose assignment to the session OVERLAPS the month
    /// (assigned before month end and not unassigned before month start) — so historical
    /// months still show students who have since left. Excludes soft-deleted and purged
    /// students (live-TeacherStudent join), distinct per student across re-assignment
    /// periods, ordered by name. Optional name/code search.
    /// </summary>
    Task<(IReadOnlyList<SessionMonthRosterRow> Items, int TotalCount)>
        GetPagedSessionMonthRosterAsync(
            long teacherId, long sessionId, DateTime monthStart, DateTime monthEndExclusive,
            string? search, int page, int pageSize);

    /// <summary>
    /// Statuses for the (student × occurrence) matrix page: every attendance record whose
    /// occurrence id AND student id fall in the given sets. Missing pairs mean unmarked.
    /// </summary>
    Task<IReadOnlyList<SessionMonthStatusCell>> GetAttendanceStatusMatrixAsync(
        IReadOnlyCollection<long> occurrenceIds, IReadOnlyCollection<long> teacherStudentIds);

    /// <summary>
    /// Whole-month present/absent totals per student for the session — across ALL its
    /// occurrences in the month, independent of the occurrence page the client is viewing.
    /// </summary>
    Task<IReadOnlyList<SessionMonthStudentCounts>> GetSessionMonthAttendanceCountsAsync(
        long sessionId, DateTime monthStart, DateTime monthEndExclusive,
        IReadOnlyCollection<long> teacherStudentIds);

}

/// <summary>Occurrence column for the session month matrix (query projection).</summary>
public class SessionMonthOccurrenceRow
{
    public long OccurrenceId { get; set; }
    public DateTime Date { get; set; }
}

/// <summary>Student row for the session month matrix (query projection).</summary>
public class SessionMonthRosterRow
{
    public long TeacherStudentId { get; set; }
    public string StudentName { get; set; } = null!;
    public string StudentCode { get; set; } = null!;
}

/// <summary>One recorded (student, occurrence) attendance cell (query projection).</summary>
public class SessionMonthStatusCell
{
    public long TeacherStudentId { get; set; }
    public long OccurrenceId { get; set; }
    public AttendanceStatus Status { get; set; }
}

/// <summary>Whole-month present/absent totals for one student (query projection).</summary>
public class SessionMonthStudentCounts
{
    public long TeacherStudentId { get; set; }
    public int PresentCount { get; set; }
    public int AbsentCount { get; set; }
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
    public DateTime? LastAbsenceDate { get; set; }
    public string? LastAbsenceSessionName { get; set; }
}

