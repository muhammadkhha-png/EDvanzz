using System.Text.Json.Serialization;

namespace Edvanz.Domain.Enums;

/// <summary>
/// Identifies the type of modification applied to a "Books &amp; fees" (event payment) record.
/// Stored as tinyint.
///
/// WHY A SEPARATE ENUM FROM <see cref="PaymentEditAction"/>: <c>PaymentRepo
/// .GetCollectorRefundsInRangeAsync</c> DERIVES the fee refund ledger from <c>PaymentEditLogs</c> by
/// matching on <see cref="PaymentEditAction"/> values. A shared enum over a shared table would mean
/// every existing query there has to PROVE it excludes extras rows. A dedicated enum on a dedicated
/// table makes that impossible by construction.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum EventPaymentEditAction : byte
{
    /// <summary>A collected amount was corrected (tutor-only).</summary>
    AmountChanged = 1,

    /// <summary>A collected payment was refunded in full or in part (tutor-only).</summary>
    Refunded = 2,

    /// <summary>A collected payment was soft-deleted (tutor-only).</summary>
    Deleted = 3,

    /// <summary>The student was marked as not taking the item.</summary>
    Exempted = 4,

    /// <summary>An exemption was lifted — by a human, or by a payment arriving for it.</summary>
    ExemptCleared = 5,

    /// <summary>The student's own amount due was overridden by a human.</summary>
    CustomAmountChanged = 6,

    /// <summary>The student was added to the item after it was created.</summary>
    ObligationAdded = 7,

    /// <summary>The student was removed from the item (tutor-only, and only when nothing was paid).</summary>
    ObligationRemoved = 8
}
