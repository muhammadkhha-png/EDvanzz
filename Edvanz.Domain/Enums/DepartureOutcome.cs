using System.Text.Json.Serialization;

namespace Edvanz.Domain.Enums;

/// <summary>
/// Identifies the financial outcome of a student departure.
/// REQ-PAY-068/069/070: Pro-rated departure calculation outcomes.
/// Stored as tinyint in the database.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum DepartureOutcome : byte
{
    /// <summary>
    /// Student has overpaid and a refund is due to the student.
    /// REQ-PAY-069: Refund Amount = Full Period Amount − Pro-Rated Amount.
    /// </summary>
    RefundDue = 1,

    /// <summary>
    /// Student has not paid and still owes for attended occurrences.
    /// REQ-PAY-070: Amount Due = Pro-Rated Amount based on attended occurrences.
    /// </summary>
    AmountOwed = 2,

    /// <summary>
    /// Student attended zero occurrences in current period and has no obligation.
    /// REQ-PAY-071: No payment obligation exists for the current period.
    /// </summary>
    NoObligation = 3
}