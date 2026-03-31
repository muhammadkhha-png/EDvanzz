using System.Text.Json.Serialization;

namespace Edvanz.Domain.Enums;

/// <summary>
/// Defines how students are charged for a session.
/// REQ-SES-010: Monthly (fixed per calendar month) or PerSession (per individual occurrence).
/// Stored as tinyint in the database.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum PaymentType
{
    /// <summary>
    /// Students are charged a fixed amount per calendar month.
    /// REQ-SES-010: Monthly payment type.
    /// </summary>
    Monthly = 1,

    /// <summary>
    /// Students are charged a fixed amount for each individual session occurrence they attend.
    /// REQ-SES-010: Per Session payment type.
    /// </summary>
    PerSession = 2
}