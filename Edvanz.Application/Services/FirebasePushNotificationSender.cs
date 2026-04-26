using Edvanz.Application.Dtos.Subscription;
using Edvanz.Application.IservicesContract;
using Edvanz.Application.Options;
using FirebaseAdmin;
using FirebaseAdmin.Messaging;
using Google.Apis.Auth.OAuth2;
using Microsoft.AspNetCore.Builder.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

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
    private const string DeepLinkDataKey = "deepLink";

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
    public async Task<PushNotificationSendResult> SendAsync(
        string fcmToken, string title, string body, string? deepLinkPayload)
    {
        EnsureFirebaseInitialized();

        var message = new Message
        {
            Token = fcmToken,
            Notification = new Notification
            {
                Title = title,
                Body = body
            },
            Data = BuildDataPayload(deepLinkPayload)
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

    private static Dictionary<string, string>? BuildDataPayload(string? deepLinkPayload)
    {
        if (string.IsNullOrWhiteSpace(deepLinkPayload))
            return null;

        return new Dictionary<string, string>
        {
            [DeepLinkDataKey] = deepLinkPayload
        };
    }

    /// <summary>
    /// Returns the last 6 chars of a token for log correlation without exposing the full token.
    /// </summary>
    private static string Suffix(string token) =>
        string.IsNullOrEmpty(token) || token.Length < 6
            ? token
            : token[^6..];
}