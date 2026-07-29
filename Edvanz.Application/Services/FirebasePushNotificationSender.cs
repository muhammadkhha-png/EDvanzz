using Edvanz.Application.Dtos.Notifications;
using Edvanz.Application.Dtos.Subscription;
using Edvanz.Application.IservicesContract;
using Edvanz.Application.Options;
using Edvanz.Domain.Enums;
using FirebaseAdmin;
using FirebaseAdmin.Messaging;
using Google.Apis.Auth.OAuth2;
using Microsoft.AspNetCore.Builder.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace Edvanz.Infrastructure.Services;

/// <summary>
/// Firebase Cloud Messaging adapter for IPushNotificationSender (REQ-SUB-006, §7.7).
///
/// Wraps the FirebaseAdmin NuGet package so the Application layer never references
/// the SDK directly. The FirebaseApp instance is initialized once per process
/// (FirebaseAdmin enforces singleton semantics) — this class lazily creates it
/// on first use and caches the singleton via the static FirebaseApp.DefaultInstance.
///
/// EC-11: when Firebase reports an unregistered token, the sender returns a result
/// with ShouldDeactivateToken = true. The calling job (SendSubscriptionReminderJob
/// or IRenewalNotificationJob) flips UserDeviceToken.IsActive = false via
/// IUserDeviceTokenRepo.DeactivateTokenAsync.
///
/// Polly rate-limit and timeout are applied at the call site, not here (§7.4).
/// </summary>
public class FirebasePushNotificationSender : IPushNotificationSender
{
    private const string TypeDataKey = "type";
    private const string DeepLinkDataKey = "deepLink";
    private const string ScreenKey = "screen";
    private const string BadgeDataKey = "badge";

    private readonly FirebaseOptions _options;
    private readonly ILogger<FirebasePushNotificationSender> _logger;

    // Static lock so initialization is thread-safe across the first burst of
    // concurrent send calls when the worker pool warms up.
    private static readonly object FirebaseInitLock = new();
    private static FirebaseApp? _firebaseApp;

    public FirebasePushNotificationSender(
        IOptions<FirebaseOptions> options,
        ILogger<FirebasePushNotificationSender> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    /// <inheritdoc />
    public async Task<PushNotificationSendResult> SendAsync(
          string fcmToken, string title, string body, PushPayload payload)
    {
        try
        {
            EnsureFirebaseInitialized();
        }
        catch (InvalidOperationException ex)
        {
            // D5: fail soft, never throw out of the adapter. A missing/malformed
            // Firebase:CredentialsPath becomes a normal failed-send result instead of
            // an unhandled exception that Hangfire retries against a config that will
            // never fix itself on its own.
            _logger.LogError(ex,
                "Firebase not initialized — push skipped (suffix {TokenSuffix})", Suffix(fcmToken));
            return new PushNotificationSendResult
            {
                Success = false,
                ShouldDeactivateToken = false,
                ErrorCode = "firebase-not-configured"
            };
        }

        var message = new Message
        {
            Token = fcmToken,
            Notification = new Notification
            {
                Title = title,
                Body = body
            },
            Data = BuildDataPayload(payload),
            Android = BuildAndroidConfig(payload),
            Apns = BuildApnsConfig(payload)
        };
        try
        {
            string messageId = await FirebaseMessaging.DefaultInstance.SendAsync(message);
            _logger.LogDebug(
                "Push delivered: token-suffix {TokenSuffix} messageId {MessageId}",
                Suffix(fcmToken), messageId);

            return new PushNotificationSendResult { Success = true };
        }
        catch (FirebaseMessagingException ex) when (
            ex.MessagingErrorCode == MessagingErrorCode.Unregistered ||
            ex.MessagingErrorCode == MessagingErrorCode.InvalidArgument)
        {
            // EC-11: token is no longer valid for delivery — caller deactivates the row.
            _logger.LogInformation(
                "Push token unregistered (suffix {TokenSuffix}, code {Code}) — flagging for deactivation",
                Suffix(fcmToken), ex.MessagingErrorCode);

            return new PushNotificationSendResult
            {
                Success = false,
                ShouldDeactivateToken = true,
                ErrorCode = ex.MessagingErrorCode.ToString()
            };
        }
        catch (FirebaseMessagingException ex)
        {
            // Transient Firebase-side issue — caller's Polly retry policy handles re-attempts.
            _logger.LogWarning(ex,
                "Push send failed (suffix {TokenSuffix}, code {Code})",
                Suffix(fcmToken), ex.MessagingErrorCode);

            return new PushNotificationSendResult
            {
                Success = false,
                ShouldDeactivateToken = false,
                ErrorCode = ex.MessagingErrorCode.ToString()
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Push send unexpected failure (suffix {TokenSuffix})", Suffix(fcmToken));

            return new PushNotificationSendResult
            {
                Success = false,
                ShouldDeactivateToken = false,
                ErrorCode = "unexpected"
            };
        }
    }

    // ════════════════════════════════════════════════
    // PRIVATE HELPERS
    // ════════════════════════════════════════════════

    /// <inheritdoc />
    public async Task<IReadOnlyList<TokenSendResult>> SendMulticastAsync(
        IReadOnlyList<string> fcmTokens, string title, string body, PushPayload payload)
    {
        if (fcmTokens.Count == 0)
            return Array.Empty<TokenSendResult>();

        try
        {
            EnsureFirebaseInitialized();
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex,
                "Firebase not initialized — multicast push skipped ({TokenCount} tokens)",
                fcmTokens.Count);

            var notConfigured = new List<TokenSendResult>(fcmTokens.Count);
            foreach (var token in fcmTokens)
            {
                notConfigured.Add(new TokenSendResult
                {
                    FcmToken = token,
                    Success = false,
                    ShouldDeactivateToken = false,
                    ErrorCode = "firebase-not-configured"
                });
            }
            return notConfigured;
        }

        // NOTE: MulticastMessage.Tokens — verify this property's exact type against the
        // installed FirebaseAdmin 3.5.0 (expected IReadOnlyList<string>). If the build
        // complains, change to `fcmTokens.ToList()`.
        var message = new MulticastMessage
        {
            Tokens = fcmTokens,
            Notification = new Notification { Title = title, Body = body },
            Data = BuildDataPayload(payload),
            Android = BuildAndroidConfig(payload),
            Apns = BuildApnsConfig(payload)
        };

        BatchResponse batch;
        try
        {
            batch = await FirebaseMessaging.DefaultInstance.SendEachForMulticastAsync(message);
        }
        catch (Exception ex)
        {
            // Failure before per-token results existed (network/transport level) —
            // caller's Polly retry handles re-attempts. Mark all as failed-not-stale
            // so nothing is wrongly deactivated off a transport error.
            _logger.LogError(ex,
                "Multicast push send unexpected failure ({TokenCount} tokens)", fcmTokens.Count);

            var failed = new List<TokenSendResult>(fcmTokens.Count);
            foreach (var token in fcmTokens)
            {
                failed.Add(new TokenSendResult
                {
                    FcmToken = token,
                    Success = false,
                    ShouldDeactivateToken = false,
                    ErrorCode = "unexpected"
                });
            }
            return failed;
        }

        var results = new List<TokenSendResult>(fcmTokens.Count);
        for (int i = 0; i < fcmTokens.Count; i++)
        {
            var response = batch.Responses[i];
            if (response.IsSuccess)
            {
                results.Add(new TokenSendResult { FcmToken = fcmTokens[i], Success = true });
                continue;
            }

            var ex = response.Exception;
            bool shouldDeactivate = ex is FirebaseMessagingException fme &&
                (fme.MessagingErrorCode == MessagingErrorCode.Unregistered ||
                 fme.MessagingErrorCode == MessagingErrorCode.InvalidArgument);

            if (shouldDeactivate)
            {
                _logger.LogInformation(
                    "Push token unregistered (suffix {TokenSuffix}) — flagging for deactivation",
                    Suffix(fcmTokens[i]));
            }
            else
            {
                _logger.LogWarning(ex,
                    "Multicast push failed for token suffix {TokenSuffix}", Suffix(fcmTokens[i]));
            }

            results.Add(new TokenSendResult
            {
                FcmToken = fcmTokens[i],
                Success = false,
                ShouldDeactivateToken = shouldDeactivate,
                ErrorCode = (ex as FirebaseMessagingException)?.MessagingErrorCode.ToString() ?? "unexpected"
            });
        }

        return results;
    }

    // ════════════════════════════════════════════════
    // PLATFORM CONFIG (Android / iOS) + WIRE-VALUE MAPPING
    // ════════════════════════════════════════════════

    /// <summary>
    /// Explicit FCM data["type"] wire value per category — deliberately NOT
    /// Category.ToString(), so a C# enum-member rename or typo never changes what
    /// ships to a device without an explicit, reviewed change here.
    /// </summary>
    private static string CategoryWireValue(NotificationCategory category) => category switch
    {
        NotificationCategory.msg => "msg",
        NotificationCategory.notifiction => "notification",
        _ => category.ToString()
    };

    /// <summary>
    /// Android notification channel id. Per product: SEPARATE channels for msg vs
    /// notification — the Flutter app must register NotificationChannel("msg") and
    /// NotificationChannel("notification") with matching ids or the OS silently
    /// falls back to a default channel (sound/importance/grouping all wrong).
    /// </summary>
    private static string ChannelId(NotificationCategory category) => category switch
    {
        NotificationCategory.msg => "msg",
        NotificationCategory.notifiction => "notification",
        _ => "notification"
    };

    /// <summary>
    /// Android delivery config. High priority on both categories — with only two
    /// buckets left after the enum collapse, "notification" now covers every
    /// remaining app-generated alert (payment rejected, capacity resolved, renewal
    /// confirmed, subscription reminder), not just the low-urgency daily reminder,
    /// so normal priority would under-deliver several of them.
    /// NOTE: AndroidNotification.NotificationCount — verify this member exists on
    /// the installed FirebaseAdmin 3.5.0. If not, drop this line; data["badge"] in
    /// BuildDataPayload still carries the value for the Flutter side to apply.
    /// </summary>
    private static AndroidConfig BuildAndroidConfig(PushPayload payload) => new()
    {
        Priority = Priority.High,
        Notification = new AndroidNotification
        {
            ChannelId = ChannelId(payload.Category),
            NotificationCount = payload.Badge
        }
    };

    /// <summary>
    /// iOS delivery config. apns-priority:10 for immediate delivery on both
    /// categories. No interruptionLevel/time-sensitive — the app does NOT hold the
    /// Time-Sensitive entitlement, so setting it would be silently ignored/rejected
    /// by APNs; default "active" interruption level is correct without it.
    /// </summary>
    private static ApnsConfig BuildApnsConfig(PushPayload payload)
    {
        var aps = new Aps();
        if (payload.Badge.HasValue)
            aps.Badge = payload.Badge.Value;

        return new ApnsConfig
        {
            Headers = new Dictionary<string, string> { ["apns-priority"] = "10" },
            Aps = aps
        };
    }

    // ════════════════════════════════════════════════
    // PRIVATE HELPERS
    // ════════════════════════════════════════════════

    private void EnsureFirebaseInitialized()
    {
        if (_firebaseApp is not null) return;

        lock (FirebaseInitLock)
        {
            if (_firebaseApp is not null) return;

            // FirebaseAdmin enforces a single default-instance per process.
            // If something else already created it, reuse it.
            if (FirebaseApp.DefaultInstance is not null)
            {
                _firebaseApp = FirebaseApp.DefaultInstance;
                return;
            }

            GoogleCredential credential = LoadCredential();

            _firebaseApp = FirebaseApp.Create(new AppOptions
            {
                Credential = credential,
                ProjectId = string.IsNullOrEmpty(_options.ProjectId) ? null : _options.ProjectId
            });

            _logger.LogInformation("Firebase initialized (project {ProjectId})", _options.ProjectId);
        }
    }

    private GoogleCredential LoadCredential()
    {
        if (string.IsNullOrWhiteSpace(_options.CredentialsPath))
        {
            throw new InvalidOperationException(
                "Firebase:CredentialsPath is not configured. Set it to a service-account JSON file path or inline JSON.");
        }

        // Two supported deployment patterns:
        //   (a) File path (typical local / VM deployment).
        //   (b) Serialized JSON pasted directly (containerized / Key Vault deployment).
        // Distinguish by inspecting whether the value starts with '{'.
        string trimmed = _options.CredentialsPath.TrimStart();

        return trimmed.StartsWith("{")
            ? GoogleCredential.FromJson(_options.CredentialsPath)
            : GoogleCredential.FromFile(_options.CredentialsPath);
    }

    /// <summary>
    /// Builds the FCM data map from the structured payload. Emits two keys:
    ///   - "type"     : the NotificationCategory name (always present) — the client's
    ///                  discriminator between a person-to-person chat message and an
    ///                  app-generated notification.
    ///   - "deepLink" : a canonical JSON object {"screen":...,<args>} for tap routing
    ///                  (present only when the payload specifies a Screen).
    /// </summary>
    private static Dictionary<string, string> BuildDataPayload(PushPayload payload)
    {
        var data = new Dictionary<string, string>
        {
            [TypeDataKey] = CategoryWireValue(payload.Category)
        };

        string? deepLink = BuildDeepLink(payload);
        if (deepLink is not null)
            data[DeepLinkDataKey] = deepLink;

        // Android has no reliable server-authoritative badge API — some launchers
        // ignore AndroidNotification.NotificationCount. Sending the raw value in data
        // lets the Flutter client set the badge itself via a local badger plugin.
        if (payload.Badge.HasValue)
            data[BadgeDataKey] = payload.Badge.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);

        return data;
    }

    /// <summary>
    /// Serializes Screen + Args into a single canonical deep-link JSON object,
    /// e.g. {"screen":"chat","conversationId":"123"}. Args are written first so an
    /// explicit Screen always wins the "screen" key. Returns null when no Screen is set.
    /// </summary>
    private static string? BuildDeepLink(PushPayload payload)
    {
        if (string.IsNullOrWhiteSpace(payload.Screen))
            return null;

        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        if (payload.Args is not null)
        {
            foreach (var kvp in payload.Args)
                map[kvp.Key] = kvp.Value;
        }
        map[ScreenKey] = payload.Screen;

        return JsonSerializer.Serialize(map);
    }

    /// <summary>
    /// Returns the last 6 chars of a token for log correlation without exposing the full token.
    /// </summary>
    private static string Suffix(string token) =>
        string.IsNullOrEmpty(token) || token.Length < 6
            ? token
            : token[^6..];
}