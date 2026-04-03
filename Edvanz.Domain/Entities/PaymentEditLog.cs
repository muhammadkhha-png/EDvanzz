using Edvanz.Domain.Entities.ShareProp;
using Edvanz.Domain.Enums;
using System.ComponentModel.DataAnnotations.Schema;

namespace Edvanz.Domain.Entities;

/// <summary>
/// Audit trail entry for a payment transaction modification.
/// BR-PAY-002: Only the tutor role may edit, delete, or reverse payment records.
/// REQ-PAY-027: Assistants have no access to payment history modification.
///
/// PaymentTransactionId is nullable with SET NULL FK behavior.
/// When parent transaction is deleted, the edit logs survive with null FK,
/// preserving the audit trail. Same pattern as AttendanceEditLog (BR-ATT-006).
/// </summary>
public class PaymentEditLog : BaseEntity
{
    /// <summary>
    /// Foreign key to the payment transaction that was edited.
    /// SET NULL when parent transaction is deleted — log survives for audit.
    /// </summary>
    [ForeignKey(nameof(PaymentTransaction))]
    public long? PaymentTransactionId { get; set; }
    public PaymentTransaction? PaymentTransaction { get; set; }

    /// <summary>
    /// The type of edit action performed.
    /// </summary>
    public PaymentEditAction EditAction { get; set; }

    /// <summary>
    /// The payment amount before this edit was applied.
    /// </summary>
    [Column(TypeName = "decimal(10,2)")]
    public decimal PreviousAmount { get; set; }

    /// <summary>
    /// The payment amount after this edit was applied.
    /// </summary>
    [Column(TypeName = "decimal(10,2)")]
    public decimal NewAmount { get; set; }

    /// <summary>
    /// The payment status before this edit.
    /// </summary>
    public PaymentStatus PreviousStatus { get; set; }

    /// <summary>
    /// The payment status after this edit.
    /// </summary>
    public PaymentStatus NewStatus { get; set; }

    /// <summary>
    /// Who made the edit. FK to Users table.
    /// BR-PAY-002: Always the tutor.
    /// </summary>
    public long? EditedByUserId { get; set; }

    /// <summary>
    /// When the edit was made.
    /// Precision to the second (datetime2(0)).
    /// </summary>
    public DateTime EditedAt { get; set; }

    /// <summary>
    /// Optional reason or note for the edit.
    /// REQ-PAY-075: Override logged alongside original calculated amount.
    /// </summary>
    public string? EditReason { get; set; }
}