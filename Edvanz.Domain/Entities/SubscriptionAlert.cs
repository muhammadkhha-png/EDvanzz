using Edvanz.Domain.Entities.ShareProp;
using Edvanz.Domain.Enums;
using System.ComponentModel.DataAnnotations.Schema;

namespace Edvanz.Domain.Entities;

/// <summary>
/// Idempotency log for the subscription-reminder scheduler (C-7).
/// Guarantees each alert (D-5 through D-0 for a specific subscription) is sent at most once.
///
/// Uniqueness is enforced at the database level via
///     UNIQUE INDEX IX_SubscriptionAlerts_Key ON (TeacherId, SubscriptionEndDate, AlertDay)
/// so that if the dispatcher enqueues two jobs for the same alert (retry, manual trigger,
/// or concurrent runs), only the first INSERT succeeds and the second exits early.
///
/// The key INCLUDES SubscriptionEndDate so that early renewals (which change the EndDate)
/// produce a fresh set of alert keys — the D-3 alert for the old EndDate is a different row
/// from the D-3 alert for the new EndDate. No cross-renewal collision.
///
/// REQ-SUB-005: Six alerts per subscription (D-5, D-4, D-3, D-2, D-1, D-0).
/// REQ-SUB-006: Delivered via Push + WhatsApp (no SMS per D-02).
/// </summary>
public class SubscriptionAlert : BaseEntity
{
    /// <summary>
    /// Foreign key to the Teacher for whom this alert was sent.
    /// </summary>
    [ForeignKey(nameof(Teacher))]
    public long TeacherId { get; set; }

    public Teacher Teacher { get; set; } = null!;

    /// <summary>
    /// The EndDate of the subscription this alert refers to (date part only).
    /// Part of the idempotency key so that renewals which change EndDate produce
    /// fresh alert slots without colliding with historical alerts.
    /// Stored as date (no time component).
    /// </summary>
    [Column(TypeName = "date")]
    public DateTime SubscriptionEndDate { get; set; }

    /// <summary>
    /// Days-until-expiry bucket at the time of alert. One of {5, 4, 3, 2, 1, 0}.
    /// 0 = day of expiry (REQ-SUB-005 sixth alert).
    /// </summary>
    public int AlertDay { get; set; }

    /// <summary>
    /// UTC timestamp when the alert was sent (or attempted).
    /// </summary>
    public DateTime SentAt { get; set; }

    /// <summary>
    /// Flags of channels that successfully delivered. Records ONLY successes.
    /// A row with ChannelsSent = None means both Push and WhatsApp failed —
    /// still recorded to prevent retries that would create duplicate DB churn.
    /// Ops monitors these rows for investigation (EC-14).
    /// </summary>
    public SubscriptionAlertChannel ChannelsSent { get; set; } = SubscriptionAlertChannel.None;
}
