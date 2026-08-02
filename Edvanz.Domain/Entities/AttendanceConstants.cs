namespace Edvanz.Domain.Constants;

/// <summary>
/// Constants for the Attendance Module (Module 3).
/// Centralizes magic numbers, limits, and localization keys
/// so they are compile-time checked and single-sourced.
/// </summary>
public static class AttendanceConstants
{
    // ══════════════════════════════════════════════
    // CONFIGURATION LIMITS
    // ══════════════════════════════════════════════

    /// <summary>
    /// Maximum depth when scanning recent records to recalculate consecutive absences.
    /// Step 2.2: Replaces the hardcoded Take(100) in RecalculateConsecutiveAbsencesAsync.
    /// Must be high enough to cover any realistic absence streak.
    /// </summary>
    public const int MaxConsecutiveAbsenceScanDepth = 1000;

    /// <summary>
    /// Number of recent attendance records to fetch for the compact visual indicator.
    /// REQ-ATT-068: Last 5 session occurrence statuses.
    /// </summary>
    public const int RecentStatusIndicatorCount = 5;

    /// <summary>
    /// Maximum length for denormalized session and student name fields.
    /// </summary>
    public const int NameMaxLength = 200;

    /// <summary>
    /// Maximum length for student code fields.
    /// </summary>
    public const int StudentCodeMaxLength = 20;

    /// <summary>
    /// Maximum length for edit reason fields.
    /// </summary>
    public const int EditReasonMaxLength = 500;

    /// <summary>
    /// Maximum number of retries for DbUpdateConcurrencyException on counter updates.
    /// FIX H7: Prevents unhandled concurrency exceptions when two assistants mark
    /// attendance for linked sessions simultaneously.
    /// </summary>
    public const int MaxConcurrencyRetries = 3;

    // ══════════════════════════════════════════════
    // LOCALIZATION KEYS — ATTENDANCE MODULE
    // ══════════════════════════════════════════════

    public static class Messages
    {
        public const string TeacherNotFound = "TeacherNotFound";
        public const string SessionNotFound = "SessionNotFound";
        public const string StudentNotFound = "StudentNotFound";
        public const string Success = "Success";

        // Take Attendance
        public const string AttendanceMarkedSuccess = "AttendanceMarkedSuccess";
        public const string AttendanceBulkMarkedSuccess = "AttendanceBulkMarkedSuccess";
        public const string AttendanceNoOccurrenceToday = "AttendanceNoOccurrenceToday";
        public const string AttendanceDuplicateDetected = "AttendanceDuplicateDetected";
        public const string AttendanceAbsenceAlertPending = "AttendanceAbsenceAlertPending";
        public const string AttendanceCrossSessionNotLinked = "AttendanceCrossSessionNotLinked";
        public const string AttendanceStudentNotAssigned = "AttendanceStudentNotAssigned";
        public const string InvalidAttendanceStatus = "InvalidAttendanceStatus";
        public const string CrossSessionNoFutureOccurrence = "CrossSessionNoFutureOccurrence";

        // Edit Attendance
        public const string AttendanceAddedSuccess = "AttendanceAddedSuccess";
        public const string AttendanceEditedSuccess = "AttendanceEditedSuccess";
        public const string AttendanceRecordNotFound = "AttendanceRecordNotFound";
        public const string AttendanceRecordDeletedSuccess = "AttendanceRecordDeletedSuccess";

        // Hold
        public const string AttendanceHeldSuccess = "AttendanceHeldSuccess";
        public const string AttendanceHoldReleasedSuccess = "AttendanceHoldReleasedSuccess";
        public const string AttendanceHoldNotFound = "AttendanceHoldNotFound";
        public const string AttendanceAlreadyMarked = "AttendanceAlreadyMarked";

        // Reporting
        public const string AttendanceReportGenerated = "AttendanceReportGenerated";

        // Export
        public const string InvalidExportFormat = "InvalidExportFormat";
        public const string ExportCompleted = "ExportCompleted";

        // Offline Sync
        public const string SyncCompleted = "SyncCompleted";
        public const string FeatureNotImplemented = "FeatureNotImplemented";

        // Visibility
        public const string AttendanceVisibilityDisabled = "AttendanceVisibilityDisabled";

        // Dashboard
        public const string NoOccurrenceRedirectToEdit = "NoOccurrenceRedirectToEdit";

        // ══════════════════════════════════════════════
        // AUDIT FIX — NEW MESSAGE KEYS
        // ══════════════════════════════════════════════

        /// <summary>BR-ATT-001: No retroactive attendance before student assignment date.</summary>
        public const string AttendanceBeforeAssignmentDate = "AttendanceBeforeAssignmentDate";

        /// <summary>Session was deleted while a student was on hold.</summary>
        public const string AttendanceSessionDeletedWhileHeld = "AttendanceSessionDeletedWhileHeld";

        /// <summary>Student is in recycle bin (soft-deleted) and cannot have attendance recorded.</summary>
        public const string AttendanceStudentInRecycleBin = "AttendanceStudentInRecycleBin";

        // ══════════════════════════════════════════════
        // V2 AUDIT FIX — ADDITIONAL MESSAGE KEYS
        // ══════════════════════════════════════════════

        /// <summary>
        /// FIX H5: Used when report generation fails in the export path.
        /// Previously the export path incorrectly used AttendanceReportGenerated (a success key).
        /// </summary>
        public const string AttendanceReportGenerationFailed = "AttendanceReportGenerationFailed";

        /// <summary>
        /// FIX H7: Concurrency conflict on absence counter update — retry exhausted.
        /// </summary>
        public const string AttendanceConcurrencyConflict = "AttendanceConcurrencyConflict";

        /// <summary>
        /// FIX H1: Duplicate attendance record detected via Edit Attendance add path.
        /// </summary>
        public const string AttendanceDuplicateRecordExists = "AttendanceDuplicateRecordExists";

        // ══════════════════════════════════════════════
        // SECTION-3 FIX — NEW MESSAGE KEYS (ATT-2/3/5/6/7)
        // ══════════════════════════════════════════════

        /// <summary>ATT-2: Required attendance status was omitted (nullable enum ⇒ detectable — CC-6).</summary>
        public const string AttendanceStatusRequired = "AttendanceStatusRequired";

        /// <summary>ATT-6: Required occurrence date was omitted on the add-record path (CC-6).</summary>
        public const string AttendanceOccurrenceDateRequired = "AttendanceOccurrenceDateRequired";

        /// <summary>ATT-5: mark-bulk recorded only some students; others were skipped (see per-student results).</summary>
        public const string AttendanceBulkMarkedPartial = "AttendanceBulkMarkedPartial";

        /// <summary>
        /// mark-bulk received neither the new <c>items</c> list nor the legacy <c>teacherStudentIds</c>
        /// list — nothing to mark. Exactly one of the two shapes must be supplied.
        /// </summary>
        public const string AttendanceBulkTargetsRequired = "AttendanceBulkTargetsRequired";

        /// <summary>ATT-5: per-student skip reason — a cross-session visitor cannot be marked Absent here.</summary>
        public const string AttendanceCrossSessionCannotBeAbsent = "AttendanceCrossSessionCannotBeAbsent";

        /// <summary>ATT-7: report type requires a student id (SingleStudentAbsence).</summary>
        public const string AttendanceReportStudentRequired = "AttendanceReportStudentRequired";

        /// <summary>ATT-7: report type requires a session id (SessionAbsence / SessionAttendanceHistory / LinkedSessionsAttendance).</summary>
        public const string AttendanceReportSessionRequired = "AttendanceReportSessionRequired";

        /// <summary>ATT-7: report type requires a session group id (SessionGroupAttendance).</summary>
        public const string AttendanceReportSessionGroupRequired = "AttendanceReportSessionGroupRequired";

        // ══════════════════════════════════════════════
        // AUTO-ABSENT — edit-log reasons (audit trail on AttendanceEditLog.EditReason).
        // Stored on the log rows the auto-absent job / flip produce, so the edit history reads clearly.
        // ══════════════════════════════════════════════

        /// <summary>Auto-absent job rolled an unresolved Held record forward to Absent (whole slot passed).</summary>
        public const string AutoAbsentHeldRolledToAbsent = "AutoAbsentHeldRolledToAbsent";

        /// <summary>A later same-occurrence mark overwrote a system-written auto-absent record.</summary>
        public const string AutoAbsentOverwrittenByMark = "AutoAbsentOverwrittenByMark";

        /// <summary>A later equivalent-occurrence present scan flipped a prior Absent to CrossSessionPresent.</summary>
        public const string AbsentFlippedToCrossSessionPresent = "AbsentFlippedToCrossSessionPresent";
    }

    // ══════════════════════════════════════════════
    // AUTO-ABSENT BACKGROUND JOB
    // ══════════════════════════════════════════════

    /// <summary>
    /// Hangfire recurring-job id for the nightly auto-absent dispatcher (registered in Program.cs).
    /// One id, single-sourced here so registration and any programmatic trigger stay in sync.
    /// </summary>
    public const string AutoAbsentJobId = "attendance-auto-absent";

    /// <summary>
    /// Hangfire queue the per-teacher auto-absent workers run on. Declared on the worker interface
    /// method via <c>[Queue(...)]</c> (§6.1) — its own queue keeps the nightly sweep off the
    /// latency-sensitive "notifications" queue.
    /// </summary>
    public const string AutoAbsentQueue = "auto-absent";

    // ══════════════════════════════════════════════
    // MODULE IDENTITY
    // ══════════════════════════════════════════════

    /// <summary>
    /// Module name as registered in the <c>Models</c> seed table
    /// (DbInitializer.SeedModulesAsync) and emitted in the per-request
    /// <c>UserAuthSnapshot.Modules</c> set. This exact string is what
    /// PermissionHandler step 5 matches against, so it MUST equal the seeded row.
    /// Used in every [ModulePermission(ModuleName, ...)] on the attendance endpoints.
    /// Same single-sourcing pattern as <see cref="VideoConstants"/> / StudentConstants —
    /// change it in exactly one place: here.
    /// </summary>
    public const string ModuleName = "Attendance";

    // ══════════════════════════════════════════════
    // PERMISSION NAMES (seeded under the "Attendance" module — REQ-USR-017)
    // Values MUST match DbInitializer.SeedPermissionsAsync exactly. The snapshot
    // stores them qualified as "Attendance.{Name}" and the handler re-forms that
    // same key, so any drift here silently locks assistants out.
    // ══════════════════════════════════════════════

    /// <summary>REQ-USR-017 "Take Attendance" — record attendance for session occurrences.</summary>
    public const string PermissionTake = "Take";

    /// <summary>REQ-USR-017 "Edit Attendance" — modify previously recorded attendance records.</summary>
    public const string PermissionEdit = "Edit";

    /// <summary>REQ-USR-017 "View Attendance History" — view student/session attendance history.</summary>
    public const string PermissionViewHistory = "ViewHistory";

    /// <summary>REQ-USR-017 "View Absence Overview" — view the absence overview panel and consecutive-absence data.</summary>
    public const string PermissionViewAbsenceOverview = "ViewAbsenceOverview";

    /// <summary>REQ-USR-017 "Generate Attendance Reports" — generate and export attendance reports.</summary>
    public const string PermissionGenerateReports = "GenerateReports";
}