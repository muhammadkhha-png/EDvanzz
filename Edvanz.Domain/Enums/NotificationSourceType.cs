namespace Edvanz.Domain.Enums;

/// <summary>
/// Discriminates which background job produced a UserNotification row, paired with
/// UserNotification.SourceEntityId to form a per-job idempotency key — the same role
/// SubscriptionAlerts(TeacherId, SubscriptionEndDate, AlertDay) plays for the
/// subscription-reminder job. Internal bookkeeping only; not exposed on NotificationDto.
/// </summary>
public enum NotificationSourceType : byte
{
    Renewal = 1,
    PaymentRejected = 2,
    CapacityResolved = 3
}