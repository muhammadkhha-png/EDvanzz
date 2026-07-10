namespace Edvanz.Domain.Constants;

/// <summary>
/// Constants for the Subscription Management Module (Module 11 — v1.2).
/// Centralizes magic numbers, validation limits, and localization keys
/// so they are compile-time checked and single-sourced.
/// </summary>
public static class SubscriptionConstants
{
    // ══════════════════════════════════════════════
    // CONFIGURATION LIMITS
    // ══════════════════════════════════════════════

    /// <summary>
    /// Maximum number of times SubscriptionService.ConfirmPaymentAsync retries on
    /// DbUpdateConcurrencyException or SQL 2601 unique-violation (§6.6).
    /// </summary>
    public const int MaxConcurrencyRetries = 2;

    // Free-tier per-module quotas are configuration-driven — see FreeTierQuotaOptions
    // (appsettings "FreeTierQuotas" section), not hardcoded here.

    /// <summary>
    /// Window size (in days) before EndDate during which the dispatcher fires reminders.
    /// REQ-SUB-005: D-5 through D-0 — six alerts.
    /// </summary>
    public const int AlertWindowDays = 6;

    /// <summary>
    /// EC-24 guard: a pending payment cannot be approved if the teacher's current
    /// subscription was created within this many hours of the pending payment's
    /// InitiatedAt (duplicate-payment heuristic).
    /// </summary>
    public const int DuplicatePaymentGuardHours = 24;

    /// <summary>
    /// Maximum length for the rejection-reason field on PendingSubscriptionPayment.
    /// Mirrors the entity's [MaxLength(500)] mapping.
    /// </summary>
    public const int RejectionReasonMaxLength = 500;

    /// <summary>
    /// Maximum length for FCM tokens. Mirrors the entity's [MaxLength(500)] mapping.
    /// </summary>
    public const int FcmTokenMaxLength = 500;

    // ══════════════════════════════════════════════
    // PHONE MASKING (BR-SUB-011)
    // ══════════════════════════════════════════════

    /// <summary>
    /// Number of leading digits preserved when masking a phone number for tutor display.
    /// "01012341234" → "010****1234" — three leading + four trailing.
    /// </summary>
    public const int PhoneMaskLeadingDigits = 3;

    /// <summary>
    /// Number of trailing digits preserved when masking a phone number for tutor display.
    /// </summary>
    public const int PhoneMaskTrailingDigits = 4;

    // ══════════════════════════════════════════════
    // CACHE KEY FORMAT (§8.7)
    // ══════════════════════════════════════════════

    /// <summary>
    /// Format string for Redis subscription-status cache keys.
    /// Use string.Format(CacheKeyFormat, teacherId).
    /// </summary>
    public const string CacheKeyFormat = "subscription:teacher:{0}";

    // ══════════════════════════════════════════════
    // HANGFIRE QUEUE NAMES (§7.5)
    // ══════════════════════════════════════════════

    /// <summary>
    /// Dedicated queue for subscription notification jobs.
    /// Isolates fan-out from time-critical work on the default queue.
    /// </summary>
    public const string NotificationsQueue = "notifications";

    /// <summary>
    /// Recurring-job id for the daily reminder dispatcher (Hangfire RecurringJob.AddOrUpdate).
    /// </summary>
    public const string ReminderDispatcherJobId = "subscription-reminder-dispatcher";

    /// <summary>
    /// Recurring-job id for the hourly pending-payment expiry job.
    /// </summary>
    public const string PendingPaymentExpiryJobId = "subscription-pending-payment-expiry";

    // ══════════════════════════════════════════════
    // LOCALIZATION KEYS — SUBSCRIPTION MODULE
    // ══════════════════════════════════════════════

    /// <summary>
    /// Localization message keys. Names match entries in Messages.en.resx / Messages.ar.resx.
    /// </summary>
    public static class Messages
    {
        // ── Generic operation outcomes ──
        public const string Success = "Success";
        public const string TeacherNotFound = "TeacherNotFound";
        public const string UserNotFound = "UserNotFound";

        // ── GetCurrent / GetHistory ──
        public const string NoActiveSubscription = "NoActiveSubscription";

        // ── Free-tier quota block (shared across all quota-limited create paths) ──
        public const string SubscriptionRequired = "SubscriptionRequired";

        // ── Renewal initiate ──
        public const string SubscriptionRenewalInitiated = "SubscriptionRenewalInitiated";
        public const string InvalidPaymentMethod = "InvalidPaymentMethod";
        public const string InvalidPaymentChannel = "InvalidPaymentChannel";
        public const string SuperAdminMethodNotAllowed = "SuperAdminMethodNotAllowed";
        public const string PendingPaymentAlreadyInFlight = "PendingPaymentAlreadyInFlight";
        public const string CapacityPackageNotAssigned = "CapacityPackageNotAssigned";
        public const string CapacityPackagePriceNotSet = "CapacityPackagePriceNotSet";

        // ── Manual submit ──
        public const string ManualSubmissionRecorded = "ManualSubmissionRecorded";
        public const string PendingPaymentNotFound = "PendingPaymentNotFound";
        public const string PendingPaymentNotInInitiated = "PendingPaymentNotInInitiated";
        public const string TransactionReferenceRequired = "TransactionReferenceRequired";
        public const string PaymentPhoneRequired = "PaymentPhoneRequired";

        // ── Confirmation flow ──
        public const string PaymentConfirmed = "PaymentConfirmed";
        public const string PaymentAlreadyConfirmed = "PaymentAlreadyConfirmed";
        public const string ConcurrentRenewalDetected = "ConcurrentRenewalDetected";

        // ── Admin pending queue ──
        public const string PendingPaymentApproved = "PendingPaymentApproved";
        public const string PendingPaymentRejected = "PendingPaymentRejected";
        public const string PendingPaymentNotAwaitingApproval = "PendingPaymentNotAwaitingApproval";
        public const string DuplicatePaymentDetected = "DuplicatePaymentDetected";
        public const string RejectionReasonRequired = "RejectionReasonRequired";

        // ── Admin manual overrides ──
        public const string SubscriptionActivated = "SubscriptionActivated";
        public const string SubscriptionExtended = "SubscriptionExtended";
        public const string SubscriptionEndDateUpdated = "SubscriptionEndDateUpdated";
        public const string SubscriptionNotFound = "SubscriptionNotFound";
        public const string ExtensionDaysMustBePositive = "ExtensionDaysMustBePositive";
        public const string EndDateMustBeAfterStart = "EndDateMustBeAfterStart";
        public const string PackagePriceUpdated = "PackagePriceUpdated";
        public const string PackageNotFound = "PackageNotFound";
        public const string PriceMustBeNonNegative = "PriceMustBeNonNegative";

        // ── Notifications inbox ──
        public const string NotificationNotFound = "NotificationNotFound";
        public const string NotificationMarkedRead = "NotificationMarkedRead";
        public const string NotificationsAllMarkedRead = "NotificationsAllMarkedRead";
        public const string FcmTokenRegistered = "FcmTokenRegistered";
        public const string FcmTokenRequired = "FcmTokenRequired";

        // ── Reminder & confirmation templates (FR-SUB-012, §7.6) ──
        public const string SubscriptionReminderTitle = "SubscriptionReminderTitle";
        public const string SubscriptionReminderBodyDaysRemaining = "SubscriptionReminderBodyDaysRemaining";
        public const string SubscriptionReminderBodyExpiresToday = "SubscriptionReminderBodyExpiresToday";
        public const string RenewalConfirmationTitle = "RenewalConfirmationTitle";
        public const string RenewalConfirmationBody = "RenewalConfirmationBody";
        public const string PaymentRejectedTitle = "PaymentRejectedTitle";
        public const string PaymentRejectedBody = "PaymentRejectedBody";

        // ── Manual-pay instructions ──
        public const string ManualPayInstructionsVodafoneCash = "ManualPayInstructionsVodafoneCash";
        public const string ManualPayInstructionsInstaPay = "ManualPayInstructionsInstaPay";

        // ── Webhook ──
        public const string WebhookSignatureInvalid = "WebhookSignatureInvalid";
        public const string PaymobNotConfigured = "PaymobNotConfigured";
    }
}