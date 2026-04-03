using Edvanz.Domain.Entities.ShareProp;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Edvanz.Domain.Entities;

/// <summary>
/// Tracks the cumulative cash collected by an assistant that has not been handed to the tutor.
/// REQ-PAY-034: Cumulative total of all payments the assistant has collected.
/// BR-PAY-004: Represents unhandled cash — a tracking mechanism, not a digital balance.
/// REQ-PAY-036: Reset to zero when tutor confirms cash handover.
///
/// One wallet per assistant per teacher (unique constraint enforced in DbContext).
/// </summary>
public class AssistantWallet : BaseEntity
{
    /// <summary>
    /// Foreign key to the owning Teacher.
    /// REQ-PAY-NFR-001: All payment data scoped to individual tutor account.
    /// </summary>
    [ForeignKey(nameof(Teacher))]
    public long TeacherId { get; set; }
    public Teacher Teacher { get; set; } = null!;

    /// <summary>
    /// Foreign key to the assistant whose wallet this tracks.
    /// NO ACTION: Wallet preserved for historical reset log integrity.
    /// App logic handles cleanup when assistant is deactivated.
    /// </summary>
    [ForeignKey(nameof(Assistant))]
    public long AssistantId { get; set; }
    public Assistant Assistant { get; set; } = null!;

    /// <summary>
    /// The assistant's User ID for direct lookup.
    /// Stored for performance: avoids join through Assistant table.
    /// </summary>
    public long AssistantUserId { get; set; }

    /// <summary>
    /// Current unhandled balance since last reset.
    /// REQ-PAY-034: Accumulates with each collection.
    /// REQ-PAY-036: Set to zero on wallet reset.
    /// REQ-PAY-038: After reset, fresh accumulation begins from zero.
    /// </summary>
    [Column(TypeName = "decimal(12,2)")]
    public decimal CurrentBalance { get; set; } = 0;

    /// <summary>
    /// Lifetime total collected by this assistant (never resets).
    /// Used for collector performance reports (REQ-PAY-058).
    /// </summary>
    [Column(TypeName = "decimal(14,2)")]
    public decimal TotalCollected { get; set; } = 0;

    /// <summary>
    /// Total number of payment transactions processed by this assistant.
    /// REQ-PAY-058: Total number of transactions for collector performance.
    /// </summary>
    public int TransactionCount { get; set; } = 0;

    /// <summary>
    /// Timestamp of the most recent payment collection by this assistant.
    /// Used for wallet history display.
    /// </summary>
    public DateTime? LastCollectionAt { get; set; }

    /// <summary>
    /// Optimistic concurrency token. Prevents lost updates when the assistant
    /// collects on two devices simultaneously.
    /// </summary>
    [Timestamp]
    public byte[] RowVersion { get; set; } = null!;
}