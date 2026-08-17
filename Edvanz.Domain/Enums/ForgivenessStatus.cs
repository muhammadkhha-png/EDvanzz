using System.Text.Json.Serialization;

namespace Edvanz.Domain.Enums;

/// <summary>
/// Lifecycle of a <see cref="Entities.PaymentForgiveness"/> record. A forgiveness waives part of a
/// student's outstanding balance (NOT cash) and is fully reversible. Stored as tinyint.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ForgivenessStatus : byte
{
    /// <summary>The forgiveness is in effect — the waived amount is subtracted from the student's outstanding.</summary>
    Active = 1,

    /// <summary>The forgiveness was reversed — the waived amount was restored to the student's outstanding.</summary>
    Reversed = 2
}
