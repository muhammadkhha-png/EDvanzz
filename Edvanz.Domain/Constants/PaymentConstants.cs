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

        // Assistants
        public const string AssistantNotFound = "AssistantNotFound";
    }
}