using System.Text.Json.Serialization;

namespace Edvanz.Domain.Enums;

/// <summary>
/// Identifies the type of modification applied to a payment record.
/// BR-PAY-002: Only the tutor role may perform these actions.
/// Stored as tinyint in the database.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum PaymentEditAction : byte
{
    /// <summary>
    /// The payment amount was modified.
    /// REQ-PAY-027: Tutor edited the amount.
    /// </summary>
    AmountChanged = 1,

    /// <summary>
    /// The payment status was changed (e.g., Paid → Unpaid reversal).
    /// REQ-PAY-027: Tutor reversed or modified status.
    /// </summary>
    StatusChanged = 2,

    /// <summary>
    /// The payment record was soft-deleted.
    /// BR-PAY-002: Only tutor can delete payment records.
    /// </summary>
    Deleted = 3,

    /// <summary>
    /// The payment was reversed (refund or correction).
    /// REQ-PAY-027: Tutor reversed the payment.
    /// </summary>
    Reversed = 4,

    /// <summary>
    /// The payment period assignment was changed.
    /// BR-PAY-001: Tutor overrode automatic period assignment.
    /// </summary>
    PeriodReassigned = 5
}