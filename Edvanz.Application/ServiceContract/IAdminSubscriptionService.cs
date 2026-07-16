using Edvanz.Application.Dtos;
using Edvanz.Application.Dtos.Subscription;

namespace Edvanz.Application.ServiceContract;

/// <summary>
/// Super-admin subscription operations (§4.4).
/// Covers manual activation (no payment), extension, end-date override,
/// and the pending-payment approval queue.
///
/// All write operations record CreatedByUserId for audit (FR-SUB-064 / REQ-ADM-016).
/// All methods return Result&lt;T&gt; for consistent error handling.
/// </summary>
public interface IAdminSubscriptionService
{
    // ══════════════════════════════════════════════
    // MANUAL OVERRIDES (REQ-ADM-012 / 015 / 016)
    // ══════════════════════════════════════════════

    /// <summary>
    /// Manual activation that bypasses payment (FR-SUB-060 / REQ-ADM-012).
    /// Inserts a new TeacherSubscription with PaymentChannel = SuperAdminOverride
    /// and AmountPaidEGP = 0. Flips the previous current row IsCurrent = false.
    /// Cache is invalidated synchronously after commit; no payment notification fires.
    /// </summary>
    Task<Result<CurrentSubscriptionDto>> ActivateAsync(
        long adminUserId, AdminActivateRequest request);

    /// <summary>
    /// Extends the teacher's current subscription EndDate by N days (FR-SUB-061 / REQ-ADM-016).
    /// Does NOT create a new row — mutates the current row's EndDate.
    /// Cache is invalidated synchronously after commit.
    /// </summary>
    Task<Result<CurrentSubscriptionDto>> ExtendAsync(
        long adminUserId, AdminExtendRequest request);

    /// <summary>
    /// Overrides the EndDate on a specific TeacherSubscription row (FR-SUB-062 / REQ-ADM-015).
    /// Used when the admin needs to correct an issued subscription.
    /// Cache is invalidated synchronously after commit.
    /// </summary>
    Task<Result<CurrentSubscriptionDto>> SetEndDateAsync(
        long adminUserId, AdminSetEndDateRequest request);

    // ══════════════════════════════════════════════
    // PENDING PAYMENT QUEUE (FR-SUB-063)
    // ══════════════════════════════════════════════

    /// <summary>
    /// Paginated queue of pending payments awaiting super-admin approval.
    /// Encrypted submitted-details blob is decrypted server-side for review only;
    /// the decrypted view is returned in the DTO (BR-SUB-011: admin sees plaintext
    /// in the review endpoint, not in payment history).
    /// </summary>
    Task<Result<PaginatedResponse<List<AdminPendingQueueItemDto>>>> GetPendingQueueAsync(
        int page, int pageSize);

    /// <summary>
    /// Approves a pending payment. Delegates to ISubscriptionService.ConfirmPaymentAsync
    /// passing the admin's user id as resolvedByUserId.
    ///
    /// EC-24 GUARD: If the teacher's current subscription was created within the last
    /// 24 hours (i.e., the renewal was already paid via another path), this method
    /// returns a "duplicate payment detected" error and does NOT confirm — the admin
    /// must verify the situation manually before approving.
    /// </summary>
    Task<Result<ConfirmPaymentResultDto>> ApprovePendingAsync(
        long adminUserId, long pendingPaymentId);

    /// <summary>
    /// Rejects a pending payment. Sets Status = Rejected, persists the rejection reason,
    /// and enqueues IPendingPaymentRejectedNotificationJob to inform the teacher
    /// via push + WhatsApp + UserNotification row.
    /// </summary>
    Task<Result<bool>> RejectPendingAsync(
        long adminUserId, long pendingPaymentId, string rejectionReason);

    // ══════════════════════════════════════════════
    // CAPACITY-INCREASE REQUEST QUEUE
    // ══════════════════════════════════════════════

    /// <summary>
    /// Paginated FIFO queue of Pending capacity-increase requests, each enriched with
    /// live teacher context (current capacity, active student count) and the projected
    /// renewal price at the requested capacity.
    /// </summary>
    Task<Result<PaginatedResponse<List<AdminCapacityRequestQueueItemDto>>>> GetCapacityRequestQueueAsync(
        int page, int pageSize);

    /// <summary>
    /// Approves a capacity request: raises Teacher.StudentCapacity to the requested
    /// value immediately (never decreases — Math.Max guard) inside one transaction with
    /// the request's status flip, then notifies the teacher post-commit. The new price
    /// naturally applies from the NEXT renewal (initiation reads live capacity, BR-SUB-009).
    /// </summary>
    Task<Result<CapacityRequestDto>> ApproveCapacityRequestAsync(
        long adminUserId, long requestId);

    /// <summary>
    /// Rejects a capacity request with a required reason (max 500 chars), then notifies
    /// the teacher post-commit with that reason.
    /// </summary>
    Task<Result<CapacityRequestDto>> RejectCapacityRequestAsync(
        long adminUserId, long requestId, string rejectionReason);

    // ══════════════════════════════════════════════
    // PRICING (per-student rate: renewal = capacity × rate)
    // ══════════════════════════════════════════════

    /// <summary>Returns the current per-student monthly rate with its audit fields.</summary>
    Task<Result<SubscriptionPricingDto>> GetPricingAsync();

    /// <summary>
    /// Updates the per-student monthly rate (must be &gt; 0). BR-SUB-009: in-flight
    /// pending payments retain their initiation-time amount snapshot.
    /// </summary>
    Task<Result<SubscriptionPricingDto>> UpdatePricingAsync(
        long adminUserId, decimal pricePerStudentEGP);

    // ══════════════════════════════════════════════
    // MODULE QUOTAS (free-tier limits table)
    // ══════════════════════════════════════════════

    /// <summary>All free-tier module quota rows, ordered by ModuleKey.</summary>
    Task<Result<List<ModuleQuotaDto>>> GetModuleQuotasAsync();

    /// <summary>
    /// Updates one module's FreeTierLimit (0–10,000; 0 = subscriber-only) and optional
    /// description, records the audit columns, and invalidates the gate cache so the
    /// change applies immediately on this instance (≤60s elsewhere). 404 when the key
    /// has no seeded row.
    /// </summary>
    Task<Result<ModuleQuotaDto>> UpdateModuleQuotaAsync(
        long adminUserId, string moduleKey, UpdateModuleQuotaRequest request);
}