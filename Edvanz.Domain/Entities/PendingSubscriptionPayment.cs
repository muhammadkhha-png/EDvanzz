using Edvanz.Domain.Entities.ShareProp;
using Edvanz.Domain.Enums;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Edvanz.Domain.Entities;

/// <summary>
/// Represents a subscription-renewal payment attempt that has not yet been confirmed.
/// A pending payment NEVER grants the tutor feature access (BR-SUB-008). Only after
/// Status transitions to Confirmed does the system insert a TeacherSubscription row
/// and restore access.
///
/// Why a separate table from TeacherSubscription:
///   - Failed, rejected, and in-flight payments must not create TeacherSubscription rows.
///   - Paymob iframe sessions can live 20+ minutes before webhook arrives.
///   - Manual submissions await super admin review (could be hours).
///   - Cleanly separates "authorized for access" (TeacherSubscription) from
///     "attempted to pay" (PendingSubscriptionPayment).
///
/// Lifecycle:
///   Initiated → AwaitingSuperAdminApproval (manual submit) → Confirmed | Rejected
///   Initiated → Confirmed (Paymob webhook success)
///   Initiated → Rejected (Paymob webhook failure)
///   Initiated → Expired (24h timeout by PendingPaymentExpiryJob)
/// </summary>
public class PendingSubscriptionPayment : BaseEntity
{
    // ──────────────────────────────────────────────────────────────
    // OWNERSHIP
    // ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Foreign key to the Teacher initiating this payment.
    /// </summary>
    [ForeignKey(nameof(Teacher))]
    public long TeacherId { get; set; }

    public Teacher Teacher { get; set; } = null!;

    // ──────────────────────────────────────────────────────────────
    // PAYMENT ROUTING
    // ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Chosen payment method: VodafoneCash or InstaPay.
    /// SuperAdminManual is not valid for pending payments — those bypass this table.
    /// </summary>
    public PaymentMethod PaymentMethod { get; set; }

    /// <summary>
    /// The channel the tutor selected: Paymob (auto-confirm) or Manual (admin approval).
    /// In v1 with the Paymob stub (D-04), all payments route to Manual.
    /// </summary>
    public PaymentChannel PaymentChannel { get; set; }

    /// <summary>
    /// Current lifecycle state of this pending payment.
    /// </summary>
    public PendingPaymentStatus Status { get; set; } = PendingPaymentStatus.Initiated;

    // ──────────────────────────────────────────────────────────────
    // AMOUNT (captured at initiation per BR-SUB-009)
    // ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Amount to be paid, in EGP. Captured from the teacher's StudentCapacityPackage.MonthlyPriceEGP
    /// at the moment of initiation (BR-SUB-009: in-flight payments honor the initiation-time price,
    /// not subsequent price changes).
    /// </summary>
    [Column(TypeName = "decimal(10,2)")]
    public decimal AmountEGP { get; set; }

    // ──────────────────────────────────────────────────────────────
    // PAYMOB-SPECIFIC
    // ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Paymob session/order identifier returned when the iframe was created.
    /// Used by PaymobWebhookController to look up the pending row on callback.
    /// Null for Manual-channel payments.
    /// </summary>
    [MaxLength(200)]
    public string? PaymobSessionId { get; set; }

    // ──────────────────────────────────────────────────────────────
    // MANUAL-SUBMISSION DETAILS
    // ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Transaction reference the tutor entered during manual submission
    /// (Vodafone Cash transaction ID or InstaPay reference). Super admin verifies
    /// this against the provider's dashboard before approving.
    /// Null until manual-submit is called.
    /// </summary>
    [MaxLength(100)]
    public string? SubmittedTransactionReference { get; set; }

    /// <summary>
    /// Encrypted JSON containing tutor-submitted details (payment phone, any other
    /// manually entered fields). Encrypted via IEncryptionService (REQ-SUB-NFR-004).
    /// Null until manual-submit is called.
    /// </summary>
    public string? EncryptedSubmittedDetails { get; set; }

    // ──────────────────────────────────────────────────────────────
    // RESOLUTION
    // ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Reason given when Status = Rejected.
    /// Shown to the teacher in PaymentRejected notification (REQ-SUB-007 pattern).
    /// </summary>
    [MaxLength(500)]
    public string? RejectionReason { get; set; }

    /// <summary>
    /// Timestamp when the tutor initiated the renewal flow (UTC).
    /// Distinct from BaseEntity.CreateAt for explicit semantic clarity.
    /// </summary>
    public DateTime InitiatedAt { get; set; }

    /// <summary>
    /// Timestamp when Status reached a terminal state (Confirmed, Rejected, Expired).
    /// Null while in Initiated or AwaitingSuperAdminApproval.
    /// </summary>
    public DateTime? ResolvedAt { get; set; }

    /// <summary>
    /// The user who resolved this pending payment. For super admin manual approve/reject,
    /// this is the super admin's User Id. Null for Paymob auto-confirmed payments.
    /// </summary>
    [ForeignKey(nameof(ResolvedByUser))]
    public long? ResolvedByUserId { get; set; }

    public User? ResolvedByUser { get; set; }
}
