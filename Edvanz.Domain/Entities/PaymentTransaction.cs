using Edvanz.Domain.Entities.ShareProp;
using Edvanz.Domain.Enums;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Edvanz.Domain.Entities;

/// <summary>
/// Core payment fact table. One row per payment collected.
/// REQ-PAY-002: All payment collection methods produce a unified record.
/// REQ-PAY-012: Records collecting user, student, amount, session, method, timestamp.
/// REQ-PAY-010: Online payments flagged distinctly via PaymentMethod and IsOnlinePayment.
/// BR-PAY-002: Only tutor can edit/delete/reverse (soft-delete pattern).
///
/// DENORMALIZATION RATIONALE (SessionName, StudentName, StudentCode):
/// Sessions use hard delete (BR-SES-004). Students use permanent purge after 10-day recycle bin.
/// Payment records MUST survive both session deletion and student purge for financial history.
/// These denormalized fields make every record self-describing after parent entities are destroyed.
/// Same pattern as AttendanceRecord (BR-ATT-005).
///
/// Multi-tenant isolation: TeacherId stored directly for tenant-scoped indexes.
/// </summary>
public class PaymentTransaction : BaseEntity
{
    // ══════════════════════════════════════════════
    // TENANT ISOLATION
    // ══════════════════════════════════════════════

    /// <summary>
    /// Foreign key to the owning Teacher. Stored directly for tenant-scoped index performance.
    /// REQ-PAY-NFR-001: All payment data scoped to individual tutor account.
    /// </summary>
    [ForeignKey(nameof(Teacher))]
    public long TeacherId { get; set; }
    public Teacher Teacher { get; set; } = null!;

    // ══════════════════════════════════════════════
    // STUDENT REFERENCE (Nullable FK for purge safety)
    // ══════════════════════════════════════════════

    /// <summary>
    /// Foreign key to the student record.
    /// SET NULL on student permanent purge. Denormalized fields preserve display data.
    /// </summary>
    [ForeignKey(nameof(TeacherStudent))]
    public long? TeacherStudentId { get; set; }
    public TeacherStudent? TeacherStudent { get; set; }

    // ══════════════════════════════════════════════
    // SESSION REFERENCE (Nullable for hard-delete survival)
    // ══════════════════════════════════════════════

    /// <summary>
    /// Denormalized: the session this payment was applied to.
    /// Nullable: set to null by application logic before session hard-delete.
    /// </summary>
    public long? SessionId { get; set; }

    [ForeignKey(nameof(SessionId))]
    public Session? Session { get; set; }

    /// <summary>
    /// Foreign key to the specific session occurrence (PerSession payment type only).
    /// SET NULL when occurrence is cleaned up. Nullable for Monthly payment type.
    /// </summary>
    public long? SessionOccurrenceId { get; set; }

    [ForeignKey(nameof(SessionOccurrenceId))]
    public SessionOccurrence? SessionOccurrence { get; set; }

    /// <summary>
    /// Foreign key to the payment period this transaction satisfies.
    /// REQ-PAY-018/019: Automatically assigned to earliest unpaid period.
    /// </summary>
    [ForeignKey(nameof(PaymentPeriod))]
    public long? PaymentPeriodId { get; set; }
    public PaymentPeriod? PaymentPeriod { get; set; }

    /// <summary>
    /// Foreign key to the student's session assignment at collection time.
    /// SET NULL on assignment cleanup. Preserves assignment context.
    /// </summary>
    [ForeignKey(nameof(StudentSessionAssignment))]
    public long? StudentSessionAssignmentId { get; set; }
    public StudentSessionAssignment? StudentSessionAssignment { get; set; }

    // ══════════════════════════════════════════════
    // FINANCIAL DATA
    // ══════════════════════════════════════════════

    /// <summary>
    /// The amount that was due for this payment.
    /// REQ-PAY-015: Pre-filled from session amount or custom amount.
    /// </summary>
    [Column(TypeName = "decimal(10,2)")]
    public decimal AmountDue { get; set; }

    /// <summary>
    /// The actual amount collected in this transaction.
    /// REQ-PAY-017: May differ from AmountDue for partial payments.
    /// </summary>
    [Column(TypeName = "decimal(10,2)")]
    public decimal AmountPaid { get; set; }

    /// <summary>
    /// Which collection method was used for this payment.
    /// REQ-PAY-001: ManualName, ManualCode, BarcodeScan, OnlinePhoneCash, OnlineInstaPay.
    /// </summary>
    public PaymentCollectionMethod PaymentMethod { get; set; }

    /// <summary>
    /// Current payment status of this transaction.
    /// REQ-PAY-NFR-006: Paid, PartiallyPaid, Unpaid.
    /// </summary>
    public PaymentStatus PaymentTransactionStatus { get; set; }

    // ══════════════════════════════════════════════
    // COLLECTOR TRACKING (REQ-PAY-011/012/013)
    // ══════════════════════════════════════════════

    /// <summary>
    /// The user who collected this payment (tutor or assistant).
    /// REQ-PAY-011: Automatically associated with logged-in user.
    /// </summary>
    public long? CollectedByUserId { get; set; }

    // ══════════════════════════════════════════════
    // DENORMALIZED CONTEXT (survive hard-delete/purge)
    // ══════════════════════════════════════════════

    /// <summary>
    /// Denormalized: snapshot of student name at collection time.
    /// Survives student permanent purge.
    /// </summary>
    public string? StudentName { get; set; }

    /// <summary>
    /// Denormalized: snapshot of student code at collection time.
    /// Survives student permanent purge.
    /// </summary>
    public string? StudentCode { get; set; }

    /// <summary>
    /// Denormalized: snapshot of session name at collection time.
    /// Survives session hard-deletion.
    /// </summary>
    public string SessionName { get; set; } = null!;

    // ══════════════════════════════════════════════
    // TIMESTAMPS
    // ══════════════════════════════════════════════

    /// <summary>
    /// Server UTC timestamp when the payment was recorded.
    /// REQ-PAY-NFR-002: Precision to the second (datetime2(0)).
    /// </summary>
    public DateTime CollectedAt { get; set; }

    /// <summary>
    /// The local timestamp at collection (teacher's timezone).
    /// Used for display and duplicate detection on same calendar day (REQ-PAY-020).
    /// </summary>
    public DateTime LocalCollectedAt { get; set; }

    // ══════════════════════════════════════════════
    // PAYMENT FLAGS
    // ══════════════════════════════════════════════

    /// <summary>
    /// Whether this was a partial payment (AmountPaid &lt; AmountDue).
    /// REQ-PAY-017: Partial payments clearly flagged in payment history.
    /// </summary>
    public bool IsPartial { get; set; } = false;

    /// <summary>
    /// Whether the amount was pro-rated for this transaction.
    /// REQ-PAY-025: Pro-rated amount indicator.
    /// </summary>
    public bool IsProRated { get; set; } = false;

    /// <summary>
    /// Human-readable label describing the pro-rated tier applied.
    /// REQ-PAY-NFR-007: "Pro-rated: joined in days 11–20 — 2/3 of full amount".
    /// </summary>
    public string? ProRatedTierLabel { get; set; }

    /// <summary>
    /// Whether this payment was initiated online by the student.
    /// REQ-PAY-010: Online payments flagged distinctly in tutor's history.
    /// </summary>
    public bool IsOnlinePayment { get; set; } = false;

    /// <summary>
    /// Reference/confirmation number from the online payment provider.
    /// REQ-PAY-008: Transaction reference for reconciliation.
    /// </summary>
    public string? OnlineTransactionRef { get; set; }

    // ══════════════════════════════════════════════
    // OFFLINE SYNC (REQ-PAY-076 through 084)
    // ══════════════════════════════════════════════

    /// <summary>
    /// Whether this record was created offline and synced later.
    /// REQ-PAY-079: Offline records stored with full metadata.
    /// </summary>
    public bool IsOfflineRecord { get; set; } = false;

    /// <summary>
    /// Device identifier for offline-created records.
    /// REQ-PAY-082: Conflict detection uses device + timestamp.
    /// </summary>
    public string? OfflineDeviceId { get; set; }

    /// <summary>
    /// Sync status for offline records.
    /// REQ-PAY-080/081/082: Lifecycle tracking.
    /// </summary>
    public PaymentSyncStatus SyncStatus { get; set; } = PaymentSyncStatus.NotApplicable;

    // ══════════════════════════════════════════════
    // SOFT DELETE (BR-PAY-002)
    // ══════════════════════════════════════════════

    /// <summary>
    /// Soft-delete flag. BR-PAY-002: Only tutor can delete payment records.
    /// </summary>
    public bool IsDeleted { get; set; } = false;

    /// <summary>
    /// Timestamp of soft deletion. Null if active.
    /// </summary>
    public DateTime? DeletedAt { get; set; }

    // ══════════════════════════════════════════════
    // CONCURRENCY
    // ══════════════════════════════════════════════

    /// <summary>
    /// Optimistic concurrency token. Prevents lost updates when concurrent
    /// requests modify the same transaction (e.g., edit while sync completes).
    /// </summary>
    [Timestamp]
    public byte[] RowVersion { get; set; } = null!;

    // Navigation property
    public ICollection<PaymentEditLog> EditLogs { get; set; } = new List<PaymentEditLog>();
}