using Edvanz.Domain.Entities.ShareProp;
using Edvanz.Domain.Enums;
using System.ComponentModel.DataAnnotations.Schema;

namespace Edvanz.Domain.Entities;

/// <summary>
/// Records the financial snapshot of a student session transfer.
/// REQ-PAY-089: Transfer event permanently retained, never deleted.
/// REQ-PAY-085: Full payment history preserved across transfers.
/// REQ-PAY-086: Outstanding balance or credit carried forward.
/// REQ-PAY-088: Pre-transfer financial summary shown to tutor.
/// REQ-PAY-092: Unified timeline across all sessions.
///
/// Denormalized: all fields survive session hard-delete and student purge.
///
/// Multi-tenant isolation: TeacherId stored directly.
/// </summary>
public class SessionTransferEvent : BaseEntity
{
    /// <summary>
    /// Foreign key to the owning Teacher.
    /// </summary>
    [ForeignKey(nameof(Teacher))]
    public long TeacherId { get; set; }
    public Teacher Teacher { get; set; } = null!;

    /// <summary>
    /// Foreign key to the transferred student.
    /// SET NULL on permanent purge.
    /// </summary>
    [ForeignKey(nameof(TeacherStudent))]
    public long? TeacherStudentId { get; set; }
    public TeacherStudent? TeacherStudent { get; set; }

    // ══════════════════════════════════════════════
    // SOURCE SESSION
    // ══════════════════════════════════════════════

    /// <summary>
    /// Source session FK. Nullable: survives session hard-delete.
    /// </summary>
    public long? SourceSessionId { get; set; }

    /// <summary>
    /// Denormalized: source session name at transfer time.
    /// REQ-PAY-089: Permanently retained.
    /// </summary>
    public string SourceSessionName { get; set; } = null!;

    // ══════════════════════════════════════════════
    // DESTINATION SESSION
    // ══════════════════════════════════════════════

    /// <summary>
    /// Destination session FK. Nullable: survives session hard-delete.
    /// </summary>
    public long? DestinationSessionId { get; set; }

    /// <summary>
    /// Denormalized: destination session name at transfer time.
    /// REQ-PAY-089: Permanently retained.
    /// </summary>
    public string DestinationSessionName { get; set; } = null!;

    // ══════════════════════════════════════════════
    // FINANCIAL SNAPSHOT (REQ-PAY-088/089)
    // ══════════════════════════════════════════════

    /// <summary>
    /// Payment status in the source session at transfer time.
    /// REQ-PAY-088: Paid, PartiallyPaid, or Unpaid.
    /// </summary>
    public PaymentStatus PaymentStatusAtTransfer { get; set; }

    /// <summary>
    /// Outstanding balance carried forward to the new session.
    /// REQ-PAY-086/090: Separate line item in new session's payment record.
    /// </summary>
    [Column(TypeName = "decimal(10,2)")]
    public decimal OutstandingBalance { get; set; }

    /// <summary>
    /// Credit/overpayment carried forward to the new session.
    /// REQ-PAY-086/088: Applied to new session's obligations.
    /// </summary>
    [Column(TypeName = "decimal(10,2)")]
    public decimal CreditBalance { get; set; }

    /// <summary>
    /// Source session payment type description (Monthly/PerSession).
    /// REQ-PAY-091: Different payment types trigger pro-rated departure calc.
    /// </summary>
    public string SourcePaymentType { get; set; } = null!;

    /// <summary>
    /// Destination session payment type description.
    /// REQ-PAY-088: New session's payment type shown in pre-transfer summary.
    /// </summary>
    public string DestinationPaymentType { get; set; } = null!;

    // ══════════════════════════════════════════════
    // DENORMALIZED STUDENT CONTEXT
    // ══════════════════════════════════════════════

    /// <summary>
    /// Denormalized: student name at transfer time.
    /// </summary>
    public string? StudentName { get; set; }

    /// <summary>
    /// Denormalized: student code at transfer time.
    /// </summary>
    public string? StudentCode { get; set; }

    // ══════════════════════════════════════════════
    // TRANSFER METADATA
    // ══════════════════════════════════════════════

    /// <summary>
    /// UTC timestamp of the confirmed transfer.
    /// REQ-PAY-089: Date recorded permanently.
    /// </summary>
    public DateTime TransferredAt { get; set; }

    /// <summary>
    /// The user who initiated and confirmed the transfer.
    /// </summary>
    public long? TransferredByUserId { get; set; }
}