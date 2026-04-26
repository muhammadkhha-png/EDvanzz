namespace Edvanz.Application.Options;

/// <summary>
/// Paymob payment-gateway configuration. Bound from appsettings.json section "Paymob".
/// FR-SUB-034 / D-04: when Enabled = false (v1 default), the stub gateway is registered
/// and the renewal flow falls back to manual mode.
/// </summary>
public class PaymobOptions
{
    public const string Section = "Paymob";

    /// <summary>
    /// Master switch for the Paymob integration. False in v1 until credentials are provisioned.
    /// </summary>
    public bool Enabled { get; set; } = false;

    /// <summary>
    /// Paymob API key. Stored in user-secrets / Azure Key Vault — never in source control.
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// Paymob integration id for the iframe.
    /// </summary>
    public string IntegrationId { get; set; } = string.Empty;

    /// <summary>
    /// Paymob iframe id (the hosted-checkout template).
    /// </summary>
    public string IframeId { get; set; } = string.Empty;

    /// <summary>
    /// HMAC secret used to verify webhook signatures (REQ-SUB-NFR-008).
    /// </summary>
    public string WebhookHmacSecret { get; set; } = string.Empty;

    /// <summary>
    /// Public base URL Paymob redirects to after iframe completion.
    /// </summary>
    public string ReturnUrl { get; set; } = string.Empty;
}

/// <summary>
/// Firebase Cloud Messaging configuration. Bound from appsettings.json section "Firebase".
/// REQ-SUB-006: in-app push channel.
/// </summary>
public class FirebaseOptions
{
    public const string Section = "Firebase";

    /// <summary>
    /// Path to the Firebase service-account JSON file (production deployment) OR
    /// a serialized JSON string (containerized deployment). FirebasePushNotificationSender
    /// detects which based on whether the value parses as a path.
    /// </summary>
    public string CredentialsPath { get; set; } = string.Empty;

    /// <summary>
    /// Firebase project id.
    /// </summary>
    public string ProjectId { get; set; } = string.Empty;
}

/// <summary>
/// Subscription-status Redis cache configuration (§8.7 / NFR-SUB-007).
/// Bound from appsettings.json section "SubscriptionCache".
/// </summary>
public class SubscriptionCacheOptions
{
    public const string Section = "SubscriptionCache";

    /// <summary>
    /// Default TTL for a cached subscription-status projection. Default 30 minutes.
    /// </summary>
    public TimeSpan DefaultTtl { get; set; } = TimeSpan.FromMinutes(30);

    /// <summary>
    /// Reduced TTL applied when the projection's EndDate is within
    /// <see cref="NearExpiryThreshold"/> of UtcNow (§8.6). Default 1 minute.
    /// Minimizes the staleness window for subscriptions about to expire.
    /// </summary>
    public TimeSpan NearExpiryTtl { get; set; } = TimeSpan.FromMinutes(1);

    /// <summary>
    /// How close to EndDate triggers the reduced TTL. Default 10 minutes.
    /// </summary>
    public TimeSpan NearExpiryThreshold { get; set; } = TimeSpan.FromMinutes(10);
}

/// <summary>
/// Reminder dispatcher schedule configuration (§7.1).
/// Bound from appsettings.json section "ReminderScheduler".
/// </summary>
public class ReminderSchedulerOptions
{
    public const string Section = "ReminderScheduler";

    /// <summary>
    /// Cron expression for the daily dispatcher run (Hangfire).
    /// Default "0 9 * * *" = 09:00 every day in the configured TimeZone.
    /// </summary>
    public string CronExpression { get; set; } = "0 9 * * *";

    /// <summary>
    /// Time zone the cron expression evaluates in. Default "Africa/Cairo".
    /// </summary>
    public string TimeZoneId { get; set; } = "Africa/Cairo";

    /// <summary>
    /// Number of Hangfire worker threads on the "notifications" queue. Default 5.
    /// </summary>
    public int NotificationsWorkerCount { get; set; } = 5;
}

/// <summary>
/// Subscription-renewal default values. Bound from appsettings.json section "SubscriptionDefaults".
/// </summary>
public class SubscriptionDefaultsOptions
{
    public const string Section = "SubscriptionDefaults";

    /// <summary>
    /// Subscription period length in days. Default 30 (D-01 / BR-SUB-012).
    /// Centralized so the constant is not magic-number-scattered across services.
    /// </summary>
    public int PeriodDays { get; set; } = 30;

    /// <summary>
    /// Threshold (in days) for the ExpiringSoon status. Default 5 (matches REQ-SUB-005 / D-5).
    /// </summary>
    public int ExpiringSoonThresholdDays { get; set; } = 5;

    /// <summary>
    /// Maximum hours a pending payment can sit in Status = Initiated before
    /// PendingPaymentExpiryJob auto-expires it. Default 24 (EC-18).
    /// </summary>
    public int PendingPaymentExpiryHours { get; set; } = 24;

    /// <summary>
    /// External wallet number tutors send Vodafone Cash payments to. Surfaced in ManualInitiatePayload.
    /// Stored in configuration so the operations team can update it without a deploy.
    /// </summary>
    public string ManualPayToVodafoneCash { get; set; } = string.Empty;

    /// <summary>
    /// External InstaPay handle tutors send payments to.
    /// </summary>
    public string ManualPayToInstaPay { get; set; } = string.Empty;
}