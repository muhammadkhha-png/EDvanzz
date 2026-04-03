using System.Text.Json.Serialization;

namespace Edvanz.Domain.Enums;

/// <summary>
/// Tracks the synchronization status of offline-collected payment records.
/// REQ-PAY-079/080/081/082: Offline sync lifecycle.
/// Stored as tinyint in the database.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum PaymentSyncStatus : byte
{
    /// <summary>
    /// Record was created online — no sync needed.
    /// </summary>
    NotApplicable = 0,

    /// <summary>
    /// Record was created offline and is awaiting sync.
    /// REQ-PAY-079: Stored locally with full metadata.
    /// </summary>
    Pending = 1,

    /// <summary>
    /// Record has been successfully synced to the server.
    /// REQ-PAY-080: Sync completed.
    /// </summary>
    Synced = 2,

    /// <summary>
    /// Record conflicts with another record on the server.
    /// REQ-PAY-082: Conflict detected, pending tutor resolution.
    /// </summary>
    Conflict = 3
}