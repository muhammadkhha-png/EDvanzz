using System;
using System.Text.Json.Serialization;

namespace Edvanz.Domain.Enums;

/// <summary>
/// Flags indicating which channels successfully delivered a subscription alert.
/// REQ-SUB-006: Alerts delivered via Push and WhatsApp (not SMS in this scope — D-02).
/// SubscriptionAlert.ChannelsSent records ONLY the channels that succeeded —
/// failed channels are logged elsewhere (push errors → UserDeviceToken deactivation;
/// WhatsApp errors → Messaging module's MessageLog).
/// </summary>
[Flags]
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum SubscriptionAlertChannel : byte
{
    /// <summary>
    /// No channel delivered successfully. Alert row is still inserted to prevent retries
    /// (idempotency). Ops inspects rows with ChannelsSent = None for investigation.
    /// </summary>
    None = 0,

    /// <summary>
    /// Firebase Push delivered successfully to at least one device.
    /// </summary>
    Push = 1,

    /// <summary>
    /// WhatsApp message accepted by the Messaging module for delivery.
    /// </summary>
    WhatsApp = 2
}
