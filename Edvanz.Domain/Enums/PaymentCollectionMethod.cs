using System.Text.Json.Serialization;

namespace Edvanz.Domain.Enums;

/// <summary>
/// Identifies which method was used to collect a payment.
/// REQ-PAY-001: Four supported collection methods.
/// REQ-PAY-002: All methods produce the same unified payment record.
/// REQ-PAY-010: Online payments flagged distinctly in history.
/// Stored as tinyint in the database.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum PaymentCollectionMethod : byte
{
    /// <summary>
    /// Method 1: Collector typed the student's name into a search field.
    /// REQ-PAY-001 Method 1.
    /// </summary>
    ManualName = 1,

    /// <summary>
    /// Method 2: Collector typed the student's unique code.
    /// REQ-PAY-001 Method 2.
    /// </summary>
    ManualCode = 2,

    /// <summary>
    /// Method 3: Collector scanned the student's barcode.
    /// REQ-PAY-001 Method 3.
    /// </summary>
    BarcodeScan = 3,

    /// <summary>
    /// Method 4: Student initiated payment via Phone Cash (Vodafone/Orange/Etisalat Cash).
    /// REQ-PAY-005: Online payment via Phone Cash.
    /// </summary>
    OnlinePhoneCash = 4,

    /// <summary>
    /// Method 4: Student initiated payment via InstaPay.
    /// REQ-PAY-006: Online payment via InstaPay.
    /// </summary>
    OnlineInstaPay = 5
}