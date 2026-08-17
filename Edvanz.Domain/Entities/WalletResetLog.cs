using Edvanz.Domain.Entities.ShareProp;
using System.ComponentModel.DataAnnotations.Schema;

namespace Edvanz.Domain.Entities;

/// <summary>
/// Permanent ledger entry for an assistant wallet reset.
/// REQ-PAY-037: Treated as a permanent ledger event — never deleted.
/// REQ-PAY-036: Logged when tutor confirms cash handover from assistant.
/// REQ-PAY-038: Previous wallet history remains fully accessible.
///
/// Multi-tenant isolation: TeacherId stored directly for tenant-scoped indexes.
/// </summary>
public class WalletResetLog : BaseEntity
{
    /// <summary>
    /// Foreign key to the owning Teacher.
    /// REQ-PAY-NFR-001: All payment data scoped to individual tutor account.
    /// </summary>
    [ForeignKey(nameof(Teacher))]
    public long TeacherId { get; set; }
    public Teacher Teacher { get; set; } = null!;

    /// <summary>
    /// Foreign key to the (teacher-owned) assistant whose wallet was reset. NULLABLE: a reset of a
    /// <see cref="Entities.CenterAssistant"/>'s wallet records <see cref="CenterAssistantId"/> instead.
    /// NO ACTION: log survives assistant deletion for ledger permanence.
    /// </summary>
    [ForeignKey(nameof(Assistant))]
    public long? AssistantId { get; set; }
    public Assistant? Assistant { get; set; }

    /// <summary>
    /// Foreign key to the <see cref="Entities.CenterAssistant"/> whose wallet was reset (mutually
    /// exclusive with <see cref="AssistantId"/>). Null for a normal teacher-assistant reset.
    /// Configured in Fluent API (no OnDelete annotation, per the BUG-4 rule).
    /// </summary>
    public long? CenterAssistantId { get; set; }
    public CenterAssistant? CenterAssistant { get; set; }

    /// <summary>
    /// Foreign key to the assistant wallet record.
    /// </summary>
    [ForeignKey(nameof(AssistantWallet))]
    public long AssistantWalletId { get; set; }
    public AssistantWallet AssistantWallet { get; set; } = null!;

    /// <summary>
    /// The total amount that was reset (handed over to the tutor).
    /// REQ-PAY-037: Stored permanently in the payment history.
    /// </summary>
    [Column(TypeName = "decimal(12,2)")]
    public decimal AmountReset { get; set; }

    /// <summary>
    /// The user (tutor) who confirmed and executed the reset.
    /// REQ-PAY-037: Tutor who confirmed it is recorded.
    /// </summary>
    public long ResetByUserId { get; set; }

    /// <summary>
    /// UTC timestamp of the wallet reset.
    /// REQ-PAY-036: Logged with timestamp.
    /// </summary>
    public DateTime ResetAt { get; set; }

    /// <summary>
    /// Denormalized: assistant display name at reset time.
    /// Survives assistant deletion.
    /// </summary>
    public string? AssistantName { get; set; }
}