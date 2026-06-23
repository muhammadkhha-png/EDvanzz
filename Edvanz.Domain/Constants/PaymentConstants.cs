namespace Edvanz.Domain.Constants;

/// <summary>
/// Constants for the Payment Module (Module 4) and Event Payment Module (Module 5).
/// Centralizes magic numbers, limits, and localization keys
/// so they are compile-time checked and single-sourced.
/// Follows the same pattern as AttendanceConstants.
/// </summary>
public static class PaymentConstants
{
    // ══════════════════════════════════════════════
    // CONFIGURATION LIMITS
    // ══════════════════════════════════════════════

    /// <summary>
    /// Maximum depth when scanning recent periods to recalculate consecutive unpaid count.
    /// Must be high enough to cover any realistic unpaid streak.
    /// </summary>
    public const int MaxConsecutiveUnpaidScanDepth = 1000;

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
    /// Maximum length for event name fields.
    /// </summary>
    public const int EventNameMaxLength = 300;

    /// <summary>
    /// Maximum length for online transaction reference fields.
    /// </summary>
    public const int OnlineTransactionRefMaxLength = 200;

    /// <summary>
    /// Maximum length for offline device ID fields.
    /// </summary>
    public const int OfflineDeviceIdMaxLength = 100;

    /// <summary>
    /// Maximum length for pro-rated tier label fields.
    /// </summary>
    public const int ProRatedTierLabelMaxLength = 200;

    /// <summary>
    /// Maximum length for target scope IDs field (comma-separated).
    /// </summary>
    public const int TargetScopeIdsMaxLength = 4000;

    /// <summary>
    /// Maximum length for event notes field.
    /// REQ-EVT-002: Optional free-text notes.
    /// </summary>
    public const int EventNotesMaxLength = 1000;

    /// <summary>
    /// Default consecutive unpaid threshold for notifications.
    /// REQ-PAY-030: Default is 2 consecutive unpaid periods.
    /// </summary>
    public const int DefaultConsecutiveUnpaidThreshold = 2;

    /// <summary>
    /// Maximum number of retry attempts for concurrency conflicts on counters/wallets.
    /// Same pattern as AttendanceService counter retry.
    /// </summary>
    public const int MaxConcurrencyRetries = 3;

    // ══════════════════════════════════════════════
    // LOCALIZATION KEYS — PAYMENT MODULE
    // ══════════════════════════════════════════════

    public static class Messages
    {
        public const string TeacherNotFound = "TeacherNotFound";
        public const string SessionNotFound = "SessionNotFound";
        public const string StudentNotFound = "StudentNotFound";
        public const string Success = "Success";

        // Collection
        public const string PaymentCollectedSuccess = "PaymentCollectedSuccess";
        public const string PaymentStudentNotAssigned = "PaymentStudentNotAssigned";
        public const string PaymentNoUnpaidPeriod = "PaymentNoUnpaidPeriod";
        public const string PaymentDuplicateDetected = "PaymentDuplicateDetected";
        public const string PaymentAlreadyPaid = "PaymentAlreadyPaid";
        public const string PaymentSameDayWarning = "PaymentSameDayWarning";
        public const string PaymentAmountInvalid = "PaymentAmountInvalid";
        public const string PaymentStudentInRecycleBin = "PaymentStudentInRecycleBin";

        // Pro-rating
        public const string PaymentProRatedApplied = "PaymentProRatedApplied";

        // Edit/Delete (BR-PAY-002)
        public const string PaymentEditSuccess = "PaymentEditSuccess";
        public const string PaymentDeleteSuccess = "PaymentDeleteSuccess";
        public const string PaymentNotFound = "PaymentNotFound";
        public const string PaymentEditNotAuthorized = "PaymentEditNotAuthorized";

        // Custom Amount
        public const string CustomAmountSetSuccess = "CustomAmountSetSuccess";

        // Unpaid Overview
        public const string UnpaidStudentsLoaded = "UnpaidStudentsLoaded";

        // Wallet
        public const string WalletNotFound = "WalletNotFound";
        public const string WalletResetSuccess = "WalletResetSuccess";
        public const string WalletResetNotAuthorized = "WalletResetNotAuthorized";

        // Dashboard
        public const string DashboardLoaded = "DashboardLoaded";

        // Departure
        public const string DepartureSummaryLoaded = "DepartureSummaryLoaded";
        public const string DepartureConfirmedSuccess = "DepartureConfirmedSuccess";
        public const string DepartureStudentNotAssigned = "DepartureStudentNotAssigned";

        // Transfer
        public const string TransferSummaryLoaded = "TransferSummaryLoaded";
        public const string TransferConfirmedSuccess = "TransferConfirmedSuccess";
        public const string TransferSourceSessionNotFound = "TransferSourceSessionNotFound";
        public const string TransferDestinationSessionNotFound = "TransferDestinationSessionNotFound";

        // Offline Sync
        public const string SyncCompleted = "SyncCompleted";
        public const string SyncConflictsDetected = "SyncConflictsDetected";

        // Reporting
        public const string ReportGenerated = "ReportGenerated";
        public const string InvalidExportFormat = "InvalidExportFormat";
        public const string ExportCompleted = "ExportCompleted";
        public const string InvalidReportType = "InvalidReportType";

        // Visibility
        public const string PaymentVisibilityDisabled = "PaymentVisibilityDisabled";

        // ══════════════════════════════════════════════
        // EVENT PAYMENT MODULE KEYS
        // ══════════════════════════════════════════════

        public const string EventCreatedSuccess = "EventCreatedSuccess";
        public const string EventNotFound = "EventNotFound";
        public const string EventUpdatedSuccess = "EventUpdatedSuccess";
        public const string EventDeletedSuccess = "EventDeletedSuccess";
        public const string EventPaymentCollectedSuccess = "EventPaymentCollectedSuccess";
        public const string EventPaymentAlreadyPaid = "EventPaymentAlreadyPaid";
        public const string EventPaymentDuplicateWarning = "EventPaymentDuplicateWarning";
        public const string EventObligationNotFound = "EventObligationNotFound";
        public const string EventReportGenerated = "EventReportGenerated";
        public const string EventNameRequired = "EventNameRequired";
        public const string EventAmountInvalid = "EventAmountInvalid";
        public const string EventTargetScopeEmpty = "EventTargetScopeEmpty";
        public const string EventStudentAlreadyPaid = "EventStudentAlreadyPaid";
        public const string EventStudentCustomAmountSet = "EventStudentCustomAmountSet";

        // Assistants
        public const string AssistantNotFound = "AssistantNotFound";
    }
    // ─────────────────────────────────────────────────────────────────────────────
    // INSERT THE FOLLOWING BLOCK AT THE TOP OF PaymentConstants, BEFORE
    // the "CONFIGURATION LIMITS" region.  Everything else in the file stays.
    // File: Edvanz.Domain/Constants/PaymentConstants.cs
    // ─────────────────────────────────────────────────────────────────────────────

    // ══════════════════════════════════════════════
    // MODULE IDENTITY
    // ══════════════════════════════════════════════

    /// <summary>
    /// Module name as registered in the Modules seed table and emitted in JWT
    /// module claims. Matches <c>DbInitializer</c> seed row exactly.
    /// Used in every <c>[ModulePermission(PaymentConstants.ModuleName, ...)]</c>
    /// attribute and in <c>IModuleTeacherRepo.IsModuleActiveAsync</c> calls.
    /// </summary>
    public const string ModuleName = "Payment";

    /// <summary>
    /// Module name for the Event-Based Payment module (Module 5).
    /// Matches the seeded <c>"Event-Based Payment"</c> row exactly — note the hyphen.
    /// </summary>
    public const string EventModuleName = "Event-Based Payment";

    // ══════════════════════════════════════════════
    // PERMISSION NAMES — PAYMENT MODULE
    // Values MUST match the Names seeded under the "Payment" module in
    // DbInitializer.SeedPermissionsAsync.  Change here → change seeder together.
    // ══════════════════════════════════════════════

    /// <summary>Collect Payment — allows tutor/assistant to collect payments (REQ-USR-018).</summary>
    public const string PermissionCollect = "Collect";

    /// <summary>View Payment History — allows tutor/assistant to view student payment records (REQ-USR-018).</summary>
    public const string PermissionViewHistory = "ViewHistory";

    /// <summary>
    /// Edit Payment History — restricted permission; assistant can modify payment records.
    /// Carries a visible warning in the permission UI (REQ-USR-018).
    /// BR-PAY-002 makes this absolute tutor-only at the API gate; this constant exists
    /// only so the seeder and permission catalogue stay compile-time-checked.
    /// </summary>
    public const string PermissionEditHistory = "EditHistory";

    /// <summary>View Unpaid Students — allows tutor/assistant to view the unpaid overview (REQ-USR-018).</summary>
    public const string PermissionViewUnpaidStudents = "ViewUnpaidStudents";

    /// <summary>
    /// View Collector Summary — allows assistant to view their own collection summary (REQ-USR-018).
    /// REQ-PAY-014: full User Collection View (all collectors) is tutor-only;
    /// the service layer must filter to own records when the caller is an assistant.
    /// </summary>
    public const string PermissionViewCollectorSummary = "ViewCollectorSummary";

    /// <summary>Generate Payment Reports — allows tutor/assistant to generate and export reports (REQ-USR-018).</summary>
    public const string PermissionGenerateReports = "GenerateReports";

    // ══════════════════════════════════════════════
    // PERMISSION NAMES — EVENT-BASED PAYMENT MODULE
    // Values MUST match the Names seeded under the "Event-Based Payment" module.
    // ══════════════════════════════════════════════

    /// <summary>View Events — allows assistant to view event list and tracking (REQ-USR-019).</summary>
    public const string EventPermissionView = "View";

    /// <summary>Create Event — allows assistant to create new payment events (REQ-USR-019).</summary>
    public const string EventPermissionCreate = "Create";

    /// <summary>Edit Event — allows assistant to modify event configuration (REQ-USR-019).
    /// NOTE: BR-EVT-003 additionally restricts student removal to tutor-only;
    /// the service layer must enforce this on UpdateEventDto.StudentIdsToRemove.</summary>
    public const string EventPermissionEdit = "Edit";

    /// <summary>
    /// Delete Event — BR-EVT-003 makes deletion absolute tutor-only.
    /// This constant is kept so the seeder and permission catalogue stay
    /// compile-time-checked, but the <c>DeleteEvent</c> endpoint gate is
    /// <c>roleOnly: ["Teacher","SuperAdmin"]</c>, not this permission.
    /// </summary>
    public const string EventPermissionDelete = "Delete";

    /// <summary>Collect Event Payment — allows assistant to collect payments for events (REQ-USR-019).</summary>
    public const string EventPermissionCollectPayment = "CollectPayment";

    /// <summary>Generate Event Reports — allows assistant to generate and export event reports (REQ-USR-019).</summary>
    public const string EventPermissionGenerateReports = "GenerateReports";
}