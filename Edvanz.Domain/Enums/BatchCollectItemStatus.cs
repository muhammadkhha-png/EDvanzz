using System.Text.Json.Serialization;

namespace Edvanz.Domain.Enums;

/// <summary>
/// Outcome classification for a single student within a batch payment collection.
/// Transport-only (not persisted): produced by BatchCollectPaymentAsync when
/// aggregating per-student results.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum BatchCollectItemStatus : byte
{
    /// <summary>Payment was recorded successfully.</summary>
    Collected = 1,

    /// <summary>
    /// A same-day duplicate (REQ-PAY-020) or already-paid period (REQ-PAY-026) was
    /// detected; nothing was written. Re-submit this student with the corresponding
    /// ConfirmAll flag to proceed.
    /// </summary>
    NeedsConfirmation = 2,

    /// <summary>Collection failed (e.g., invalid amount, student not assigned).</summary>
    Failed = 3
}