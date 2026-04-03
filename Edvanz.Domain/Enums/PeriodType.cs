using System.Text.Json.Serialization;

namespace Edvanz.Domain.Enums;

/// <summary>
/// Defines the type of a payment period record.
/// Mirrors PaymentType but used specifically for PaymentPeriod records.
/// Stored as tinyint in the database.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum PeriodType : byte
{
    /// <summary>
    /// Period represents a full calendar month obligation.
    /// REQ-PAY-018: Payment assigned to earliest unpaid month.
    /// </summary>
    Monthly = 1,

    /// <summary>
    /// Period represents a single session occurrence obligation.
    /// REQ-PAY-019: Payment assigned to earliest unpaid occurrence.
    /// </summary>
    PerSession = 2
}