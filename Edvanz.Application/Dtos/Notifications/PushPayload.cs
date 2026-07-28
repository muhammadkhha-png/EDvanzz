using Edvanz.Domain.Enums;

namespace Edvanz.Application.Dtos.Notifications;

/// <summary>
/// Structured payload for a single FCM push (choice C / D2-b).
///
/// Carries the two pieces of routing metadata the Flutter client needs, which the
/// Firebase adapter (FirebasePushNotificationSender) emits as separate FCM data keys:
///   - <see cref="Category"/>  → data["type"]     (WHAT kind of notification this is)
///   - <see cref="Screen"/> + <see cref="Args"/> → data["deepLink"] (WHERE to route on tap)
///
/// The adapter serializes Screen + Args into a single canonical JSON object
/// (e.g. {"screen":"chat","conversationId":"123"}) so every notification type shares
/// one deep-link format — replacing the previous inconsistency where chat sent JSON
/// and subscription jobs sent plain path strings.
///
/// data["type"] answers the original product question: it lets the client tell a
/// person-to-person chat message (Category = DirectMessage) apart from an
/// app-generated notification, without inspecting the deep-link shape.
/// </summary>
public sealed class PushPayload
{
    /// <summary>
    /// The notification category — the discriminator. Required: every push must
    /// declare what it is. Serialized to data["type"] as its enum name
    /// (JsonStringEnumConverter), e.g. "DirectMessage", "SubscriptionReminder".
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
}