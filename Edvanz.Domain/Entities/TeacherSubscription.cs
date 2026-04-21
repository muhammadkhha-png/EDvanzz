using Edvanz.Domain.Entities.ShareProp;
using Edvanz.Domain.Enums;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Edvanz.Domain.Entities;

/// <summary>
/// Tracks a teacher's subscription periods. One teacher has many subscription records over time.
/// Exactly one row per teacher has IsCurrent = true (BR-SUB-006), enforced by the filtered
/// unique index IX_TeacherSubscriptions_Current on (TeacherId) WHERE IsCurrent = 1.
///
/// REQ-SUB-001: Active subscription required for all system access.
/// REQ-SUB-002: Monthly subscription model (30 fixed days per D-01).
/// REQ-SUB-003: Exposes start date, end date, days remaining; status is DERIVED, not stored (D-08 / C-6).
/// REQ-SUB-004: Period begins on payment confirmation date (see PaymentConfirmedAt).
/// BR-SUB-003: Early renewal starts from current EndDate (no days lost).
/// BR-SUB-013: PaymentConfirmedAt is the single UtcNow captured at the start of the confirmation
/// transaction and used consistently for StartDate derivation — no drift (C-4).
/// REQ-ADM-012/015/016: Super admin can manually activate, extend, or override subscriptions.
///
/// DESIGN NOTES:
///   - SubscriptionStatus column from v1.0 was REMOVED (C-6 / D-08). Status is now computed
///     at read time from (IsCurrent, StartDate, EndDate). Reports use vw_TeacherSubscriptionStatus.
///   - RowVersion provides optimistic concurrency for the IsCurrent flip (C-2 / NFR-SUB-012).
/// </summary>
public class TeacherSubscription : BaseEntity
{
    // ──────────────────────────────────────────────────────────────
    // IDENTITY & OWNERSHIP
    // ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Foreign key to the Teacher who owns this subscription period.
    /// </summary>
    [ForeignKey(nameof(Teacher))]
    public long TeacherId { get; set; }

    public Teacher Teacher { get; set; } = null!;

    // ──────────────────────────────────────────────────────────────
    // PERIOD
    // ──────────────────────────────────────────────────────────────

    /// <summary>
    /// The date the subscription period begins (UTC).
    /// Equals PaymentConfirmedAt for first-of-sequence renewals.
    /// Equals the previous current subscription's EndDate for early renewals (BR-SUB-003, BR-SUB-013).
    /// </summary>
    public DateTime StartDate { get; set; }

    /// <summary>
    /// The date the subscription period ends (UTC). StartDate + 30 days (D-01).
    /// Super admin can override via SetEndDate endpoint (REQ-ADM-015).
    /// </summary>
    public DateTime EndDate { get; set; }

    /// <summary>
    /// True if this is the teacher's CURRENT subscription (the one middleware evaluates).
    /// Exactly one row per teacher is IsCurrent = true (BR-SUB-006).
    /// Enforced by filtered unique index; violations fail at the DB level.
    /// </summary>
    public bool IsCurrent { get; set; }

    // ──────────────────────────────────────────────────────────────
    // PAYMENT DETAILS
    // ──────────────────────────────────────────────────────────────

    /// <summary>
    /// The payment method used. VodafoneCash or InstaPay for tutor-initiated renewals;
    /// SuperAdminManual for admin overrides (REQ-SUB-017, REQ-ADM-012).
    /// </summary>
    public PaymentMethod PaymentMethod { get; set; }

    /// <summary>
    /// The channel that processed the payment: Paymob (auto-confirmed), Manual (admin-approved),
    /// or SuperAdminOverride (no payment flow).
    /// </summary>
    public PaymentChannel PaymentChannel { get; set; }

    /// <summary>
    /// Amount actually paid in EGP (decimal(10,2)). Zero for SuperAdminManual activations.
    /// Sourced from StudentCapacityPackage.MonthlyPriceEGP at initiation time (BR-SUB-009).
    /// </summary>
    [Column(TypeName = "decimal(10,2)")]
    public decimal AmountPaidEGP { get; set; }

    /// <summary>
    /// External transaction reference. For Paymob: the Paymob transaction ID.
    /// For Manual: the reference string the tutor entered (Vodafone Cash / InstaPay reference).
    /// Null for SuperAdminOverride.
    /// </summary>
    [MaxLength(100)]
    public string? TransactionReference { get; set; }

    /// <summary>
    /// Encrypted JSON blob containing sensitive payment metadata
    /// (payment phone, full transaction reference, provider response, audit fields).
    /// Encrypted via IEncryptionService (REQ-SUB-NFR-004, BR-SUB-010).
    /// Never stored or logged in plain text.
    /// </summary>
    public string? EncryptedPaymentDetails { get; set; }

    /// <summary>
    /// The single UtcNow value captured at the start of the confirmation transaction
    /// (BR-SUB-013 / C-4). Used both:
    ///   (a) as the authoritative timestamp of payment confirmation, and
    ///   (b) for StartDate derivation when there is no active prior subscription.
    /// Guarantees no drift between StartDate, PaymentConfirmedAt, and CreateAt.
    /// </summary>
    public DateTime PaymentConfirmedAt { get; set; }

    // ──────────────────────────────────────────────────────────────
    // AUDIT
    // ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Foreign key to the User who created this subscription record.
    /// Populated for super admin manual activations / overrides (REQ-ADM-016).
    /// Null for tutor-initiated Paymob-confirmed renewals.
    /// </summary>
    [ForeignKey(nameof(CreatedByUser))]
    public long? CreatedByUserId { get; set; }

    public User? CreatedByUser { get; set; }

    // ──────────────────────────────────────────────────────────────
    // CONCURRENCY
    // ──────────────────────────────────────────────────────────────

    /// <summary>
    /// SQL Server rowversion for optimistic concurrency control (C-2 / NFR-SUB-012).
    /// EF Core auto-adds WHERE RowVersion = @original to UPDATE statements.
    /// If two concurrent operations flip IsCurrent, the second fails with
    /// DbUpdateConcurrencyException and the service retries (bounded, max 2 attempts).
    /// </summary>
    [Timestamp]
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
}
