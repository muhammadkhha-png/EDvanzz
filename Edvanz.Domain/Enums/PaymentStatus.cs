using System.Text.Json.Serialization;

namespace Edvanz.Domain.Enums;

/// <summary>
/// Represents the payment status of a student for a specific payment period or event.
/// REQ-PAY-NFR-006: Displayed prominently on the Payment Collection screen.
/// REQ-EVT-010: Same statuses apply to event payments.
/// Stored as tinyint in the database.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum PaymentStatus : byte
{
    /// <summary>
    /// No payment has been received for this period.
    /// REQ-PAY-031: Listed in Unpaid Students View.
    /// </summary>
    Unpaid = 0,

    /// <summary>
    /// A payment has been received but is less than the full amount due.
    /// REQ-PAY-017: Partial payments clearly flagged in payment history.
    /// </summary>
    PartiallyPaid = 1,

    /// <summary>
    /// The full amount has been received for this period.
    /// REQ-PAY-026: System warns if payment attempted for already-paid period.
    /// </summary>
    Paid = 2,

    /// <summary>
    /// More than the required amount has been received (overpayment/credit).
    /// REQ-PAY-086: Credit carried forward during session transfers.
    /// </summary>
    Overpaid = 3
}