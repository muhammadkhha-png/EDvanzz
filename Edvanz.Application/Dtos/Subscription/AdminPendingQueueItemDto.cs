using Edvanz.Domain.Enums;

namespace Edvanz.Application.Dtos.Subscription;

/// <summary>
/// One row of the super-admin pending-payment review queue (FR-SUB-063).
/// Phone number and transaction reference are DECRYPTED for review only — the
/// admin sees plaintext here, not in the tutor-facing payment history (BR-SUB-011).
/// </summary>
public class AdminPendingQueueItemDto
{
    public long Id { get; set; }

    public long TeacherId { get; set; }

    public string TeacherName { get; set; } = null!;

    public string TeacherCode { get; set; } = null!;

    public PaymentMethod PaymentMethod { get; set; }

    public PaymentChannel PaymentChannel { get; set; }

    /// <summary>
    /// Amount the tutor was charged at initiation time (BR-SUB-009).
    /// </summary>
    public decimal AmountEGP { get; set; }

    /// <summary>
    /// Decrypted payment phone number — for super admin review only.
    /// </summary>
    public string PaymentPhoneNumber { get; set; } = null!;

    /// <summary>
    /// Decrypted transaction reference — for super admin verification against the provider dashboard.
    /// </summary>
    public string TransactionReference { get; set; } = null!;

    public DateTime InitiatedAt { get; set; }
}