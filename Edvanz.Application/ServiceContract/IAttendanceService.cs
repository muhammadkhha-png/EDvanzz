using Edvanz.Application.Dtos;
using Edvanz.Application.Dtos.Attendance;
using Edvanz.Domain.Enums;

namespace Edvanz.Application.ServiceContract;

/// <summary>
/// Defines the contract for Attendance Module operations (Module 3).
/// Covers: session occurrence management, attendance taking (all three methods),
/// edit attendance, absence detection, cross-session attendance, absence overview,
/// student attendance timeline, reporting, hold status, export, and offline sync.
///
/// All methods return Result&lt;T&gt; for consistent error handling.
/// All methods are async per system architecture requirements.
///
/// TRANSACTION SAFETY:
/// All write operations use the ownsTransaction pattern:
///   bool ownsTransaction = !_unitOfWork.HasActiveTransaction;
/// This makes them safe for both standalone calls and nested calls.
/// </summary>
public interface IAttendanceService
{

    /// <summary>
    /// Returns the count of unmarked students for a session occurrence.
    /// Audit Fix (REQ-ATT-055): Used for "Mark All Present" confirmation prompt.
    /// </summary>
    Task<Result<int>> GetUnmarkedCountAsync(long teacherId, long sessionId, DateTime? occurrenceDate);
    // ══════════════════════════════════════════════
    // SESSION OCCURRENCE MANAGEMENT
    // ══════════════════════════════════════════════

    /// <summary>
    /// Generates session occurrences for a session based on its recurrence rules.
    /// REQ-ATT-001/002: Populates the occurrence schedule.
    /// </summary>
    Task<Result<int>> GenerateOccurrencesAsync(long teacherId, long sessionId);

    // ══════════════════════════════════════════════
    // ATTENDANCE DASHBOARD (REQ-ATT-049 through 052)
    // ══════════════════════════════════════════════

    /// <summary>
    /// Retrieves the attendance dashboard for a teacher on a specific date.
    /// REQ-ATT-049/050/051/052.
    /// </summary>
    Task<Result<AttendanceDashboardDto>> GetDashboardAsync(
        long teacherId, AttendanceDashboardRequest request);

    // ══════════════════════════════════════════════
    // TAKE ATTENDANCE (REQ-ATT-006 through 018)
    // ══════════════════════════════════════════════

    /// <summary>
    /// Retrieves the student list for taking attendance for a session occurrence.
    /// REQ-ATT-008/014/015/054/036.
    /// </summary>
    Task<Result<AttendanceStudentListDto>> GetAttendanceStudentListAsync(
        long teacherId, long sessionId, DateTime? occurrenceDate,
        AttendanceStudentListRequest request);

    /// <summary>
    /// Marks a single student's attendance.
    /// REQ-ATT-006/012/027-031/057-061/069-071.
    /// Step 2.3: Validates status is not Held or CrossSessionPresent.
    /// Step 4.2: Returns clear error if cross-session date remapping has no future occurrence.
    /// </summary>
    Task<Result<MarkAttendanceResultDto>> MarkAttendanceAsync(MarkAttendanceDto dto);

    /// <summary>
    /// Marks multiple students' attendance in one action.
    /// REQ-ATT-006 Method 2 / REQ-ATT-055/056.
    /// Step 2.1: Includes cross-session duplicate detection.
    /// Step 4.1: Batch counter updates (no N+1 inside loop).
    /// </summary>
    Task<Result<BulkMarkAttendanceResultDto>> BulkMarkAttendanceAsync(BulkMarkAttendanceDto dto);

    // ══════════════════════════════════════════════
    // HOLD STATUS (REQ-ATT-058/061) — Step 3.1
    // ══════════════════════════════════════════════

    /// <summary>
    /// Places a student on "hold" during attendance taking.
    /// REQ-ATT-061: Held students display a visible held status indicator.
    /// REQ-ATT-058: "Hold" cancels attendance recording, deferring to later.
    /// Step 3.1: Full implementation.
    /// </summary>
    Task<Result<MarkAttendanceResultDto>> HoldStudentAsync(HoldStudentDto dto);

    /// <summary>
    /// Releases a held student — marks as Present or discards the hold.
    /// REQ-ATT-061: Held students can be returned to later.
    /// Step 3.1: Full implementation.
    /// </summary>
    Task<Result<MarkAttendanceResultDto>> ReleaseHoldAsync(ReleaseHoldDto dto);

    // ══════════════════════════════════════════════
    // EDIT ATTENDANCE (REQ-ATT-023 through 026)
    // ══════════════════════════════════════════════

    /// <summary>
    /// Gets the occurrence calendar for Edit Attendance.
    /// REQ-ATT-065: Color-coded calendar.
    /// </summary>
    Task<Result<List<OccurrenceCalendarItemDto>>> GetOccurrenceCalendarAsync(
        long teacherId, long sessionId);

    /// <summary>
    /// Gets student attendance records for a specific occurrence (Edit Attendance view).
    /// </summary>
    Task<Result<List<AttendanceRecordDto>>> GetOccurrenceStudentsAsync(
        long teacherId, long sessionId, DateTime occurrenceDate);

    /// <summary>
    /// Edits an existing attendance record.
    /// REQ-ATT-024/025.
    /// </summary>
    Task<Result<AttendanceRecordDto>> EditAttendanceAsync(EditAttendanceDto dto);

    /// <summary>
    /// Adds a new attendance record for a past/future occurrence via Edit Attendance.
    /// REQ-ATT-024/026.
    /// </summary>
    Task<Result<AttendanceRecordDto>> AddAttendanceRecordAsync(AddAttendanceRecordDto dto);

    /// <summary>
    /// Deletes an erroneously recorded attendance entry.
    /// REQ-ATT-024.
    /// </summary>
    Task<Result<bool>> DeleteAttendanceRecordAsync(DeleteAttendanceRecordDto dto);

    /// <summary>
    /// Gets the edit history (audit trail) for an attendance record.
    /// REQ-ATT-025.
    /// </summary>
    Task<Result<List<AttendanceEditLogDto>>> GetEditHistoryAsync(
        long teacherId, long attendanceRecordId);

    // ══════════════════════════════════════════════
    // ABSENCE OVERVIEW (REQ-ATT-032 through 035)
    // ══════════════════════════════════════════════

    /// <summary>
    /// Gets the absence overview for a session and its linked sessions.
    /// REQ-ATT-032/033/034/035/067/068.
    /// </summary>
    Task<Result<PaginatedResponse<List<AbsenceOverviewStudentDto>>>> GetAbsenceOverviewAsync(
        long teacherId, long sessionId, AbsenceOverviewRequest request);

    // ══════════════════════════════════════════════
    // STUDENT ATTENDANCE TIMELINE (REQ-ATT-072 through 081)
    // ══════════════════════════════════════════════

    /// <summary>
    /// Gets the paginated student list for the Attendance Timeline view.
    /// REQ-ATT-072/073.
    /// </summary>
    Task<Result<PaginatedResponse<List<StudentAttendanceSummaryDto>>>> GetTimelineStudentListAsync(
        long teacherId, AttendanceTimelineRequest request);

    /// <summary>
    /// Gets the full attendance summary for a specific student.
    /// REQ-ATT-074/078/046.
    /// </summary>
    Task<Result<StudentAttendanceSummaryDto>> GetStudentAttendanceSummaryAsync(
        long teacherId, long teacherStudentId);

    /// <summary>
    /// Gets a single month of attendance data for a student's timeline.
    /// REQ-ATT-075/077/079.
    /// </summary>
    Task<Result<MonthlyAttendanceSummaryDto>> GetStudentTimelineMonthAsync(
        long teacherId, long teacherStudentId, StudentTimelineMonthRequest request);

    // ══════════════════════════════════════════════
    // REPORTING (REQ-ATT-040 through 042)
    // ══════════════════════════════════════════════

    /// <summary>
    /// Generates an attendance report based on the specified type and parameters.
    /// REQ-ATT-040: Six report types. Step 6.1: All 6 types fully implemented.
    /// </summary>
    Task<Result<List<AttendanceRecordDto>>> GenerateReportAsync(
        long teacherId, AttendanceReportRequest request);

    /// <summary>
    /// Exports an attendance report as a downloadable file (PDF or Excel).
    /// REQ-ATT-041: Exportable as PDF or Excel.
    /// Step 3.2: Full implementation using IAttendanceReportExportService.
    /// </summary>
    Task<Result<byte[]>> ExportReportAsync(
        long teacherId, AttendanceReportRequest request, string format);

    /// <summary>
    /// Exports a student's attendance timeline as a downloadable file.
    /// REQ-ATT-081: Preserves month-by-month structure with summary totals.
    /// Step 3.2: Full implementation using IAttendanceReportExportService.
    /// </summary>
    Task<Result<byte[]>> ExportTimelineAsync(
        long teacherId, long teacherStudentId,
        DateTime? startDate, DateTime? endDate, string format);

    // ══════════════════════════════════════════════
    // OFFLINE SYNC (REQ-ATT-084/085) — Step 3.3
    // ══════════════════════════════════════════════

    /// <summary>
    /// Syncs offline-recorded attendance records to the server.
    /// REQ-ATT-084: Background sync on reconnection.
    /// REQ-ATT-085: Conflict detection and resolution.
    /// Step 3.3: Full implementation.
    /// </summary>
    Task<Result<SyncResultDto>> SyncOfflineRecordsAsync(OfflineSyncRequestDto dto);

    // ══════════════════════════════════════════════
    // STUDENT/PARENT VIEW ACCESS
    // ══════════════════════════════════════════════

    /// <summary>
    /// Gets attendance data for a student viewing their own attendance.
    /// Gated by TeacherConfiguration visibility settings.
    /// </summary>
    Task<Result<MonthlyAttendanceSummaryDto>> GetStudentViewAttendanceAsync(
        long teacherId, long teacherStudentId, StudentTimelineMonthRequest request, AttendanceViewerType viewer);

    /// <summary>
    /// Gets attendance summary for a student from the student/parent perspective.
    /// </summary>
    Task<Result<StudentAttendanceSummaryDto>> GetStudentViewAttendanceSummaryAsync(
        long teacherId, long teacherStudentId, AttendanceViewerType viewer);

    // ══════════════════════════════════════════════
    // INTEGRATION HOOKS (called by other modules)
    // ══════════════════════════════════════════════

    /// <summary>
    /// Called by SessionService when a student is assigned to a session.
    /// REQ-ATT-019: Attendance timeline starts from assignment date.
    /// </summary>
    Task<Result<bool>> OnStudentAssignedToSessionAsync(
        long teacherId, long teacherStudentId, long sessionId, string sessionName);

    /// <summary>
    /// Called by SessionService when a student is unassigned from a session.
    /// REQ-ATT-020: Preserves complete attendance history.
    /// </summary>
    Task<Result<bool>> OnStudentUnassignedFromSessionAsync(
        long teacherId, long teacherStudentId);

    /// <summary>
    /// Called by SessionService before a session is hard-deleted.
    /// BR-ATT-005: Records retained after session deletion.
    /// Step 1.2: Uses ExecuteUpdateAsync for bulk nullification.
    /// </summary>
    Task<Result<bool>> OnSessionDeletingAsync(long teacherId, long sessionId);

    /// <summary>
    /// Called by TeacherStudentService during permanent student purge.
    /// Step 1.1: Nullifies FK references on AttendanceRecords (denormalized data preserved).
    /// </summary>
    Task<Result<bool>> OnStudentPermanentlyDeletedAsync(long teacherStudentId);
}