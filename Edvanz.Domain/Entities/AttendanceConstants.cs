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
    }

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