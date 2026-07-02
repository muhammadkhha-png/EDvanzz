using System.Text.Json.Serialization;

namespace Edvanz.Domain.Enums;

/// <summary>
/// Outcome classification for a single transaction within a batch payment edit (D2).
/// Transport-only (not persisted): produced by BatchEditPaymentAsync when aggregating
/// per-transaction results. The finer failure reason (validation vs not-found vs
/// unexpected) is carried alongside as an HTTP status code on the item result.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum BatchEditItemStatus : byte
{
    /// <summary>The edit was applied successfully.</summary>
    Succeeded = 1,

    /// <summary>
    /// The edit failed — transaction not found / not owned (404), a business/validation
    /// error (400), or an unexpected error (500). See the item's StatusCode.
    /// </summary>
    Failed = 2
}