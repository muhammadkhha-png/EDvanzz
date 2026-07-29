using Edvanz.Domain.Enums;

namespace Edvanz.Application.Dtos.Notifications;

/// <summary>
/// Structured payload for a single FCM push (choice C / D2-b).
///
/// Carries the routing metadata the Flutter client needs, which the Firebase
/// adapter (FirebasePushNotificationSender) emits as separate FCM data keys:
///   - <see cref="Category"/>  → data["type"]     (WHAT kind of notification this is)
///   - <see cref="Screen"/> + <see cref="Args"/> → data["deepLink"] (WHERE to route on tap)
///   - <see cref="Badge"/>     → aps.badge (iOS) / AndroidNotification.NotificationCount
///                                (Android) / data["badge"] (client-side fallback)
///
/// NotificationCategory has two values (msg / notification). The wire value emitted
/// for data["type"] and the Android notification channel id are mapped explicitly in
/// the adapter — NOT via Category.ToString() — so a future rename of the enum member
/// never silently changes what ships to a device.
/// </summary>
public sealed class PushPayload
{
    /// <summary>
    /// The notification category — the discriminator. Required: every push must
    /// declare what it is. Serialized to data["type"] via an explicit wire-value
    /// map in the adapter (not the raw enum name).
    /// </summary>
    public required NotificationCategory Category { get; init; }

    /// <summary>
    /// Logical destination screen the client routes to when the notification is
    /// tapped, e.g. "chat", "subscription-renew", "capacity-requests".
    /// Null when the notification has no specific route.
    /// </summary>
    public string? Screen { get; init; }

    /// <summary>
    /// Optional route arguments merged into the deep-link JSON alongside "screen"
    /// (e.g. { "conversationId": "123" }). All values are strings — FCM data
    /// payloads are string-to-string at the protocol level. Null when no args apply.
    /// </summary>
    public IReadOnlyDictionary<string, string>? Args { get; init; }

    /// <summary>
    /// Per-recipient unread count for THIS category only (msg → unread chat messages
    /// across all conversations; notification → unread bell-inbox rows). Null = no
    /// badge is set on this push. The caller (job) computes this via the relevant
    /// repo method BEFORE calling the sender — the sender never touches a repo.
    /// </summary>
    public int? Badge { get; init; }
}