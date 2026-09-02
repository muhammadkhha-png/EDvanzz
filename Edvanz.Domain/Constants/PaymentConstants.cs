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
    // Collection
        public const string PaymentCollectedSuccess = "PaymentCollectedSuccess";
        public const string PaymentBatchCollectedSuccess = "PaymentBatchCollectedSuccess"; // NEW — batch envelope
        public const string PaymentBatchEmpty = "PaymentBatchEmpty";                        // NEW — see flag below
        public const string PaymentStudentNotAssigned = "PaymentStudentNotAssigned";
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
        public const string PaymentAmountExceedsAdvanceLimit = "PaymentAmountExceedsAdvanceLimit";
        public const string PaymentStudentInRecycleBin = "PaymentStudentInRecycleBin";

        // Per-student joining-month proration override (REQ-PAY-021/022, 2026-09-02)
        public const string ProrationUpdatedSuccess = "ProrationUpdatedSuccess";
        public const string ProrationClearedSuccess = "ProrationClearedSuccess";
        /// <summary>No proration anchor (a new enrollment's still-owed first month) exists for this student+session.</summary>
        public const string ProrationNoAnchorMonth = "ProrationNoAnchorMonth";
        /// <summary>The joining month already has cash collected — proration is history and can no longer be set.</summary>
        public const string ProrationLockedAfterPayment = "ProrationLockedAfterPayment";
        /// <summary>The requested joining amount exceeds the full month (that is an advance, not a proration).</summary>
        public const string ProrationAmountExceedsFull = "ProrationAmountExceedsFull";
        /// <summary>The joining amount is negative.</summary>
        public const string ProrationAmountNegative = "ProrationAmountNegative";
        /// <summary>Audit reason stored on the proration-decision PaymentEditLog (suggested vs set).</summary>
        public const string ProrationManualEditReason = "ProrationManualEditReason";
        public const string PaymentBatchRevertSuccess = "PaymentBatchRevertSuccess";   // NEW — batch-revert envelope (D1)
        // Pro-rating
        public const string PaymentProRatedApplied = "PaymentProRatedApplied";

        // Edit/Delete (BR-PAY-002)
        public const string PaymentEditSuccess = "PaymentEditSuccess";
        public const string PaymentDeleteSuccess = "PaymentDeleteSuccess";
        public const string PaymentNotFound = "PaymentNotFound";
        public const string PaymentEditNotAuthorized = "PaymentEditNotAuthorized";
        // Collection
        public const string PaymentBatchCollectedSuccess = "PaymentBatchCollectedSuccess"; // NEW — batch envelope
        public const string PaymentBatchEmpty = "PaymentBatchEmpty";                        // NEW — see flag below
        // Custom Amount
        public const string CustomAmountSetSuccess = "CustomAmountSetSuccess";
        public const string PaymentCustomAmountInvalid = "PaymentCustomAmountInvalid"; // PAY-4 — reject <= 0

        // Amount validation (edit)
        public const string PaymentAmountNegative = "PaymentAmountNegative";           // PAY-3 — reject negative edit
        public const string EditNoteRequired = "EditNoteRequired";                     // Feature B — note required on partial/custom edit
        public const string CollectNoteRequired = "CollectNoteRequired";               // Feature C — note required on partial/custom collect

        // Forgive balance (waive outstanding — teacher-only, reversible)
        public const string ForgiveAmountInvalid = "ForgiveAmountInvalid";                       // amount <= 0
        public const string ForgiveAmountExceedsOutstanding = "ForgiveAmountExceedsOutstanding"; // amount > outstanding through current month
        public const string ForgiveSuccess = "ForgiveSuccess";
        public const string ForgivenessNotFound = "ForgivenessNotFound";
        public const string ForgivenessAlreadyReversed = "ForgivenessAlreadyReversed";
        public const string ForgiveReversedSuccess = "ForgiveReversedSuccess";

        // Offline Sync — payment domain (PAY-7; SyncCompleted/SyncConflictsDetected are attendance-worded)
        public const string PaymentSyncCompleted = "PaymentSyncCompleted";
        public const string PaymentSyncConflictsDetected = "PaymentSyncConflictsDetected";

        // Screen (api/v1) validation envelopes (PAY-5) — localized message + stable code
        public const string PaymentInvalidMonthInteger = "PaymentInvalidMonthInteger";
        public const string PaymentInvalidMonthFormat = "PaymentInvalidMonthFormat";
        public const string PaymentInvalidYear = "PaymentInvalidYear";
        public const string PaymentInvalidCollectFilter = "PaymentInvalidCollectFilter";
        public const string PaymentInvalidStatusFilter = "PaymentInvalidStatusFilter";
        public const string PaymentLookupCriteriaRequired = "PaymentLookupCriteriaRequired";
        public const string PaymentLookupStudentNotFound = "PaymentLookupStudentNotFound";
        public const string PaymentNoStudentsSelected = "PaymentNoStudentsSelected";
        public const string PaymentSubmitBatchEmpty = "PaymentSubmitBatchEmpty";
        public const string PaymentWithdrawAmountInvalid = "PaymentWithdrawAmountInvalid";
        public const string PaymentWalletInsufficientBalance = "PaymentWalletInsufficientBalance";
        public const string PaymentWalletConcurrencyConflict = "PaymentWalletConcurrencyConflict";

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
        /// <summary>422 — the tutor override is negative or exceeds the refundable/paid amount.</summary>
        public const string DepartureOverrideAmountInvalid = "DepartureOverrideAmountInvalid";
        /// <summary>Localized outcome label: "Amount to refund to student: {0} EGP".</summary>
        public const string DepartureOutcomeRefundDueLabel = "DepartureOutcomeRefundDueLabel";
        /// <summary>Localized outcome label: "Amount student still owes: {0} EGP".</summary>
        public const string DepartureOutcomeAmountOwedLabel = "DepartureOutcomeAmountOwedLabel";
        /// <summary>Localized outcome label: "No financial obligation".</summary>
        public const string DepartureOutcomeNoObligationLabel = "DepartureOutcomeNoObligationLabel";
        /// <summary>Audit reason stored on the PaymentEditLog written by the departure refund reversal.</summary>
        public const string DepartureRefundReversalReason = "DepartureRefundReversalReason";

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
        public const string PaymentBatchEditSuccess = "PaymentBatchEditSuccess";   // NEW — batch-edit envelope (D2)

        // Admin one-off: backfill a paid month by moving an advance payment
        public const string BackfillInvalidMonthFormat = "BackfillInvalidMonthFormat";     // 422 — target/from not "YYYY-MM"
        public const string BackfillStudentHasNoPeriods = "BackfillStudentHasNoPeriods";   // 404 — no periods for the student
        public const string BackfillTargetMonthExists = "BackfillTargetMonthExists";       // 422 — a period already exists at target month
        public const string BackfillAdvanceMonthNotFound = "BackfillAdvanceMonthNotFound"; // 422 — no period at the advance month
        public const string BackfillAdvanceMonthNotMonthly = "BackfillAdvanceMonthNotMonthly"; // 422 — advance period isn't Monthly
        public const string BackfillAdvanceMonthNotPaid = "BackfillAdvanceMonthNotPaid";   // 422 — advance month isn't fully cash-paid
        public const string BackfillSuccess = "BackfillSuccess";                           // 200 — applied / previewed

        // Admin one-off: reset-aware recompute of an assistant wallet's CurrentBalance
        public const string RecomputeWalletSuccess = "RecomputeWalletSuccess";             // 200 — applied / previewed
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

    /// <summary>
    /// Confirm Student Departure — allows tutor/assistant to confirm a student's course
    /// withdrawal and process the associated refund or owed-amount settlement
    /// (<c>PaymentController.ConfirmDeparture</c> / <c>PaymentService.ConfirmDepartureAsync</c>).
    /// Replaces the previous <c>roleOnly: ["Teacher","SuperAdmin"]</c> gate on that endpoint —
    /// Teachers/SuperAdmin still pass automatically (module-only / bypass), Assistants now
    /// require this specific grant since it moves money.
    /// </summary>
    public const string PermissionConfirmDeparture = "ConfirmDeparture";

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