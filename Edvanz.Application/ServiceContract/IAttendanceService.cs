using Edvanz.Application.Dtos;
using Edvanz.Application.Dtos.Attendance;

namespace Edvanz.Application.ServiceContract;

/// <summary>
/// Defines the contract for Attendance Module operations (Module 3).
/// Covers: session occurrence management, attendance taking (all three methods),
/// edit attendance, absence detection, cross-session attendance, absence overview,
/// student attendance timeline, reporting, hold status, export, and offline sync.
///
/// All methods return Result&lt;T&gt; for consistent error handling.
/// All methods are async per system architecture requirements.
/// Lives in the Application layer because it depends on Application DTOs.
///
/// TRANSACTION SAFETY:
/// All write operations use the ownsTransaction pattern:
///   bool ownsTransaction = !_unitOfWork.HasActiveTransaction;
/// This makes them safe for both standalone calls and nested calls.
/// </summary>
public interface IAttendanceService
{
    // ══════════════════════════════════════════════
    // SESSION OCCURRENCE MANAGEMENT
    // ══════════════════════════════════════════════

    /// <summary>
    /// Generates session occurrences for a session based on its recurrence rules.
    /// Called after session creation and when session date range is updated.
    /// REQ-ATT-001/002: Populates the occurrence schedule.
    /// </summary>
    /// <param name="teacherId">The teacher's Id.</param>
    /// <param name="sessionId">The session to generate occurrences for.</param>
    Task<Result<int>> GenerateOccurrencesAsync(long teacherId, long sessionId);

    // ══════════════════════════════════════════════
    // ATTENDANCE DASHBOARD (REQ-ATT-049 through 052)
    // ══════════════════════════════════════════════

    /// <summary>
    /// Retrieves the attendance dashboard for a teacher on a specific date.
    /// REQ-ATT-049: Daily summary — total scheduled, completed, pending.
    /// REQ-ATT-050/051/052: Session cards with live counters and color coding.
    /// </summary>
    /// <param name="teacherId">The teacher's Id.</param>
    /// <param name="request">Dashboard request with optional date.</param>
    Task<Result<AttendanceDashboardDto>> GetDashboardAsync(
        long teacherId, AttendanceDashboardRequest request);

    // ══════════════════════════════════════════════
    // TAKE ATTENDANCE (REQ-ATT-006 through 018)
    // ══════════════════════════════════════════════

    /// <summary>
    /// Retrieves the student list for taking attendance for a session occurrence.
    /// REQ-ATT-008: Real-time marked vs. unmarked display.
    /// REQ-ATT-014/015: Includes linked session students in secondary panel.
    /// REQ-ATT-054: Unmarked students displayed first.
    /// REQ-ATT-036: Paginated for up to 50K students.
    /// </summary>
    /// <param name="teacherId">The teacher's Id.</param>
    /// <param name="sessionId">The session to take attendance for.</param>
    /// <param name="occurrenceDate">The occurrence date (defaults to today).</param>
    /// <param name="request">Pagination and search options.</param>
    Task<Result<PaginatedResponse<List<AttendanceStudentRowDto>>>> GetAttendanceStudentListAsync(
        long teacherId, long sessionId, DateTime? occurrenceDate,
        AttendanceStudentListRequest request);

    /// <summary>
    /// Marks a single student's attendance.
    /// REQ-ATT-006: Supports ManualCode, MultiSelect, BarcodeScan methods.
    /// REQ-ATT-012: Barcode resolved to student record by caller.
    /// REQ-ATT-027/028/029: Checks previous absence and returns alert info.
    /// REQ-ATT-069/070: Checks for duplicate attendance across linked sessions.
    /// BR-ATT-002: Duplicate detection and rejection.
    /// </summary>
    /// <param name="dto">Mark attendance input data.</param>
    Task<Result<MarkAttendanceResultDto>> MarkAttendanceAsync(MarkAttendanceDto dto);

    /// <summary>
    /// Marks multiple students' attendance in one action.
    /// REQ-ATT-006 Method 2: Multi-select from student list.
    /// REQ-ATT-055: "Mark All Present" — processes all students in one call.
    /// REQ-ATT-056: Returns completion summary with absence flags.
    /// </summary>
    /// <param name="dto">Bulk mark attendance input data.</param>
    Task<Result<BulkMarkAttendanceResultDto>> BulkMarkAttendanceAsync(BulkMarkAttendanceDto dto);

    // ══════════════════════════════════════════════
    // HOLD STATUS (FIX 4.1 — REQ-ATT-061)
    // ══════════════════════════════════════════════

    /// <summary>
    /// FIX 4.1: Places a student on "hold" during attendance taking.
    /// REQ-ATT-061: When the tutor selects "Hold" after an absence alert,
    /// the student's row displays a visible held status indicator,
    /// distinguishing them from both marked and fully unmarked students.
    /// REQ-ATT-058: "Hold" cancels the action without recording attendance.
    /// </summary>
    /// <param name="dto">Hold student input data.</param>
    Task<Result<MarkAttendanceResultDto>> HoldStudentAsync(HoldStudentDto dto);

    /// <summary>
    /// FIX 4.1: Releases a held student — either marks them as Present or discards the hold.
    /// REQ-ATT-061: Held students can be returned to and processed later in the same session.
    /// </summary>
    /// <param name="dto">Release hold input data.</param>
    Task<Result<MarkAttendanceResultDto>> ReleaseHoldAsync(ReleaseHoldDto dto);

    // ══════════════════════════════════════════════
    // EDIT ATTENDANCE (REQ-ATT-023 through 026)
    // ══════════════════════════════════════════════

    /// <summary>
    /// Gets the calendar view of session occurrences with color-coded indicators.
    /// REQ-ATT-065: Green = fully recorded, amber = partial, grey = unrecorded.
    /// </summary>
    /// <param name="teacherId">The teacher's Id.</param>
    /// <param name="sessionId">The session to show occurrences for.</param>
    Task<Result<List<OccurrenceCalendarItemDto>>> GetOccurrenceCalendarAsync(
        long teacherId, long sessionId);

    /// <summary>
    /// Edits an existing attendance record.
    /// REQ-ATT-024: Change status, update edit timestamp.
    /// REQ-ATT-025: Edit logged in AttendanceEditLogs — original RecordedAt preserved.
    /// BR-ATT-006: Edit timestamp stored; original entry unmodified.
    /// </summary>
    /// <param name="dto">Edit attendance input data.</param>
    Task<Result<AttendanceRecordDto>> EditAttendanceAsync(EditAttendanceDto dto);

    /// <summary>
    /// Adds a new attendance record via Edit Attendance (not Take Attendance).
    /// REQ-ATT-024: Add records for students missed during original attendance-taking.
    /// REQ-ATT-026: Pre-record future attendance.
    /// </summary>
    /// <param name="dto">Add record input data.</param>
    Task<Result<AttendanceRecordDto>> AddAttendanceRecordAsync(AddAttendanceRecordDto dto);

    /// <summary>
    /// Deletes an attendance record (removes erroneously recorded entry).
    /// REQ-ATT-024: Remove erroneously recorded attendance entries.
    /// </summary>
    /// <param name="dto">Delete record input data.</param>
    Task<Result<bool>> DeleteAttendanceRecordAsync(DeleteAttendanceRecordDto dto);

    /// <summary>
    /// Gets the edit history (audit trail) for an attendance record.
    /// REQ-ATT-025: Differentiates original from modified records.
    /// </summary>
    /// <param name="teacherId">The teacher's Id.</param>
    /// <param name="attendanceRecordId">The record to show history for.</param>
    Task<Result<List<AttendanceEditLogDto>>> GetEditHistoryAsync(
        long teacherId, long attendanceRecordId);

    // ══════════════════════════════════════════════
    // ABSENCE OVERVIEW (REQ-ATT-032 through 035)
    // ══════════════════════════════════════════════

    /// <summary>
    /// Gets the absence overview for a session (and its linked sessions).
    /// REQ-ATT-032: Absent students with consecutive counts.
    /// REQ-ATT-033: Cross-session absence patterns via membership.
    /// REQ-ATT-034: Searchable and filterable.
    /// REQ-ATT-035: Supports selecting past occurrence dates.
    /// REQ-ATT-067: Sorted by consecutive absences descending.
    /// REQ-ATT-068: Last 5 occurrence status indicators.
    /// </summary>
    /// <param name="teacherId">The teacher's Id.</param>
    /// <param name="sessionId">The session to show absence overview for.</param>
    /// <param name="request">Search, filter, and pagination options.</param>
    Task<Result<PaginatedResponse<List<AbsenceOverviewStudentDto>>>> GetAbsenceOverviewAsync(
        long teacherId, long sessionId, AbsenceOverviewRequest request);

    // ══════════════════════════════════════════════
    // STUDENT ATTENDANCE TIMELINE (REQ-ATT-072 through 081)
    // ══════════════════════════════════════════════

    /// <summary>
    /// Gets the paginated student list for the Attendance Timeline view.
    /// REQ-ATT-072: All students regardless of session.
    /// REQ-ATT-073: Filterable by session, group, name, code.
    /// </summary>
    /// <param name="teacherId">The teacher's Id.</param>
    /// <param name="request">Filter and pagination options.</param>
    Task<Result<PaginatedResponse<List<StudentAttendanceSummaryDto>>>> GetTimelineStudentListAsync(
        long teacherId, AttendanceTimelineRequest request);

    /// <summary>
    /// Gets the full attendance summary for a specific student.
    /// REQ-ATT-074: Full record from first assignment to current date.
    /// REQ-ATT-078: All-time summary at top.
    /// REQ-ATT-046: Assignment periods chronologically.
    /// </summary>
    /// <param name="teacherId">The teacher's Id.</param>
    /// <param name="teacherStudentId">The student's Id.</param>
    Task<Result<StudentAttendanceSummaryDto>> GetStudentAttendanceSummaryAsync(
        long teacherId, long teacherStudentId);

    /// <summary>
    /// Gets a single month of attendance data for a student's timeline.
    /// REQ-ATT-075: Month organized with occurrence status per date.
    /// REQ-ATT-077: Monthly summary (total, present, absences, percentage).
    /// REQ-ATT-079: Load one month at a time on scroll.
    /// </summary>
    /// <param name="teacherId">The teacher's Id.</param>
    /// <param name="teacherStudentId">The student's Id.</param>
    /// <param name="request">Year and month to load.</param>
    Task<Result<MonthlyAttendanceSummaryDto>> GetStudentTimelineMonthAsync(
        long teacherId, long teacherStudentId, StudentTimelineMonthRequest request);

    // ══════════════════════════════════════════════
    // REPORTING (REQ-ATT-040 through 042)
    // ══════════════════════════════════════════════

    /// <summary>
    /// Generates an attendance report based on the specified type and parameters.
    /// REQ-ATT-040: Six report types.
    /// REQ-ATT-042: Must complete within 5 seconds for up to 50K students.
    /// </summary>
    /// <param name="teacherId">The teacher's Id.</param>
    /// <param name="request">Report type and parameters.</param>
    Task<Result<List<AttendanceRecordDto>>> GenerateReportAsync(
        long teacherId, AttendanceReportRequest request);

    /// <summary>
    /// FIX 4.2: Exports an attendance report as a downloadable file (PDF or Excel).
    /// REQ-ATT-041: All reports exportable as PDF or Excel (.xlsx).
    /// REQ-ATT-042: Must complete within 5 seconds for up to 50K students.
    /// </summary>
    /// <param name="teacherId">The teacher's Id.</param>
    /// <param name="request">Report type and parameters.</param>
    /// <param name="format">Export format: "xlsx" or "pdf".</param>
    Task<Result<byte[]>> ExportReportAsync(
        long teacherId, AttendanceReportRequest request, string format);

    /// <summary>
    /// FIX 4.2: Exports a student's attendance timeline as a downloadable file (PDF or Excel).
    /// REQ-ATT-081: Preserves month-by-month structure with summary totals.
    /// </summary>
    /// <param name="teacherId">The teacher's Id.</param>
    /// <param name="teacherStudentId">The student's Id.</param>
    /// <param name="startDate">Optional start of export range.</param>
    /// <param name="endDate">Optional end of export range.</param>
    /// <param name="format">Export format: "xlsx" or "pdf".</param>
    Task<Result<byte[]>> ExportTimelineAsync(
        long teacherId, long teacherStudentId,
        DateTime? startDate, DateTime? endDate, string format);

    // ══════════════════════════════════════════════
    // OFFLINE SYNC (FIX 4.3 — REQ-ATT-084/085)
    // ══════════════════════════════════════════════

    /// <summary>
    /// FIX 4.3: Syncs offline-recorded attendance records to the server.
    /// REQ-ATT-084: Automatic background sync when connectivity is restored.
    /// REQ-ATT-085: Detects conflicts and returns both versions for resolution.
    /// </summary>
    /// <param name="dto">Batch of offline attendance records with client timestamps.</param>
    Task<Result<SyncResultDto>> SyncOfflineRecordsAsync(OfflineSyncRequestDto dto);

    // ══════════════════════════════════════════════
    // STUDENT/PARENT VIEW ACCESS
    // ══════════════════════════════════════════════

    /// <summary>
    /// Gets attendance data for a student viewing their own attendance.
    /// Gated by TeacherConfiguration.StudentVisibilityAttendance.
    /// Accessed via StudentTeacherLink → TeacherStudent → AttendanceRecords.
    /// </summary>
    /// <param name="teacherId">The teacher's Id (from the StudentTeacherLink).</param>
    /// <param name="teacherStudentId">The student record Id.</param>
    /// <param name="request">Year and month to load.</param>
    Task<Result<MonthlyAttendanceSummaryDto>> GetStudentViewAttendanceAsync(
        long teacherId, long teacherStudentId, StudentTimelineMonthRequest request);

    /// <summary>
    /// Gets attendance summary for a student from the student/parent perspective.
    /// Gated by TeacherConfiguration.StudentVisibilityAttendance (for students)
    /// or TeacherConfiguration.ParentVisibilityAttendance (for parents).
    /// </summary>
    /// <param name="teacherId">The teacher's Id.</param>
    /// <param name="teacherStudentId">The student record Id.</param>
    Task<Result<StudentAttendanceSummaryDto>> GetStudentViewAttendanceSummaryAsync(
        long teacherId, long teacherStudentId);

    // ══════════════════════════════════════════════
    // INTEGRATION HOOKS (called by other modules)
    // ══════════════════════════════════════════════

    /// <summary>
    /// Called by SessionService when a student is assigned to a session.
    /// Creates the StudentSessionAssignment record and initializes the
    /// StudentAbsenceCounter if it doesn't exist.
    /// REQ-ATT-019: Attendance timeline starts from assignment date.
    /// </summary>
    /// <param name="teacherId">The teacher's Id.</param>
    /// <param name="teacherStudentId">The student being assigned.</param>
    /// <param name="sessionId">The session being assigned to.</param>
    /// <param name="sessionName">The session name (for denormalized storage).</param>
    Task<Result<bool>> OnStudentAssignedToSessionAsync(
        long teacherId, long teacherStudentId, long sessionId, string sessionName);

    /// <summary>
    /// Called by SessionService when a student is unassigned from a session.
    /// Sets UnassignedAt on the active StudentSessionAssignment.
    /// REQ-ATT-020: Preserves complete attendance history of previous session.
    /// </summary>
    /// <param name="teacherId">The teacher's Id.</param>
    /// <param name="teacherStudentId">The student being unassigned.</param>
    Task<Result<bool>> OnStudentUnassignedFromSessionAsync(
        long teacherId, long teacherStudentId);

    /// <summary>
    /// Called by SessionService before a session is hard-deleted.
    /// Nullifies FK references, preserves attendance records with denormalized data.
    /// BR-ATT-005: Records retained after session deletion.
    /// </summary>
    /// <param name="teacherId">The teacher's Id.</param>
    /// <param name="sessionId">The session being deleted.</param>
    Task<Result<bool>> OnSessionDeletingAsync(long teacherId, long sessionId);

    /// <summary>
    /// Called by TeacherStudentService during permanent student purge.
    /// FIX 1.1: Properly cleans up StudentAbsenceCounter and StudentSessionAssignment records.
    /// AttendanceRecords are preserved for historical integrity (BR-ATT-005).
    /// </summary>
    /// <param name="teacherStudentId">The student being permanently deleted.</param>
    Task<Result<bool>> OnStudentPermanentlyDeletedAsync(long teacherStudentId);
}