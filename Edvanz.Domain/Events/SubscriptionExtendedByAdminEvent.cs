namespace Edvanz.Domain.Events;

/// <summary>
/// Emitted when a super admin extends a teacher's subscription or overrides its end date
/// (REQ-ADM-015, REQ-ADM-016).
///
/// Dispatched AFTER the commit. Handlers:
///   - InvalidateSubscriptionCacheHandler  → cache removal (end-date change affects middleware)
///   - SendAdminExtensionPushHandler        → informs teacher of the extension (optional)
///
/// Emitted by:
///   - POST /api/admin/subscriptions/extend   (days added)
///   - PUT  /api/admin/subscriptions/{id}/end-date  (custom end date set)
/// </summary>
public sealed class SubscriptionExtendedByAdminEvent : IDomainEvent
{
    /// <summary>
    /// Constructs the event. Caller passes the UTC timestamp aligned with the
    /// extension transaction's commit moment.
    /// </summary>
    public SubscriptionExtendedByAdminEvent(
        long teacherId,
        long subscriptionId,
        DateTime newEndDate,
        long adminUserId,
        DateTime occurredAtUtc)
    {
        TeacherId = teacherId;
        SubscriptionId = subscriptionId;
        NewEndDate = newEndDate;
        AdminUserId = adminUserId;
        OccurredAtUtc = occurredAtUtc;
    }

    /// <summary>
    /// The teacher whose subscription was extended.
    /// </summary>
    public long TeacherId { get; }

    /// <summary>
    /// The TeacherSubscription.Id that was modified.
    /// </summary>
    public long SubscriptionId { get; }

    /// <summary>
    /// New end date (UTC) after the extension / override.
    /// </summary>
    public DateTime NewEndDate { get; }

    /// <summary>
    /// The super admin User.Id who performed the action (for audit correlation).
    /// </summary>
    public long AdminUserId { get; }

    /// <inheritdoc />
    public DateTime OccurredAtUtc { get; }
}
