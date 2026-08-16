using Edvanz.Application.Dtos;
using Edvanz.Application.Dtos.Subscription;

namespace Edvanz.Application.ServiceContract;

/// <summary>
/// Teacher-facing subscription operations (§4.1).
/// Covers status display, payment history, renewal initiation, manual submission,
/// and the internal payment-confirmation pipeline shared by the Paymob webhook
/// and admin manual-approval flows.
///
/// All methods return Result&lt;T&gt; for consistent error handling.
/// All methods are async per system architecture requirements.
///
/// AUTHORIZATION:
///   Every endpoint backed by this service is whitelisted via [AllowExpiredSubscription]
///   (FR-SUB-024) so an expired tutor can still access these to renew.
///
/// REQUIREMENTS:
///   REQ-SUB-003 / FR-SUB-003: GetCurrentAsync
///   REQ-SUB-022 / FR-SUB-039: GetHistoryPagedAsync
///   REQ-SUB-015 / FR-SUB-030: InitiateRenewalAsync
///   FR-SUB-033 / FR-SUB-040: SubmitManualAsync (manual fallback)
///   FR-SUB-036 / §6.3: ConfirmPaymentAsync (internal — webhook + admin)
/// </summary>
public interface ISubscriptionService
{
    /// <summary>
    /// Returns the teacher's current subscription with derived status, days remaining,
    /// and the price the next renewal will charge (FR-SUB-003).
    /// Status is computed at read time via SubscriptionStatusCalculator (D-08).
    /// </summary>
    Task<Result<CurrentSubscriptionDto>> GetCurrentAsync(long teacherId);

    /// <summary>
    /// Returns the single backend-driven indicator/banner contract (SubscriptionStatusDto):
    /// derived status, days remaining, attention level, call-to-action, a localized message, and
    /// the support WhatsApp number. Never a Failure for a new tutor — a subscription-less teacher
    /// gets HasSubscription=false with a "subscribe" CTA. The client renders this verbatim.
    /// </summary>
    Task<Result<SubscriptionStatusDto>> GetStatusAsync(long teacherId);

    /// <summary>
    /// Live subscription prices for the app to display the fee before a request
    /// (per-student monthly rate + flat managerial monthly). Display only — the request path
    /// always recomputes the authoritative amount server-side.
    /// </summary>
    Task<Result<SubscriptionPlansDto>> GetPlansAsync();

    /// <summary>
    /// Teacher submits a NEW-subscription request (plan + student count) for the super admin to
    /// activate. One live Pending request per teacher. The fee is computed server-side; Full
    /// requires a positive student count, Managerial ignores it.
    /// </summary>
    Task<Result<SubscriptionRequestDto>> CreateSubscriptionRequestAsync(
        long teacherId, long actingUserId, CreateSubscriptionRequestRequest request);

    /// <summary>Paginated history of the teacher's own subscription requests, newest first, all statuses.</summary>
    Task<Result<PaginatedResponse<List<SubscriptionRequestDto>>>> GetSubscriptionRequestsPagedAsync(
        long teacherId, int page, int pageSize);

    /// <summary>Teacher withdraws a Pending subscription request. Terminal rows are kept for audit.</summary>
    Task<Result<SubscriptionRequestDto>> CancelSubscriptionRequestAsync(
        long teacherId, long actingUserId, long requestId);

    /// <summary>
    /// Paginated payment history (REQ-SUB-022 / FR-SUB-039).
    /// Phone numbers and reference details are masked in the returned DTOs (BR-SUB-011).
    /// </summary>
    Task<Result<PaginatedResponse<List<SubscriptionHistoryItemDto>>>> GetHistoryPagedAsync(
        long teacherId, int page, int pageSize);

    /// <summary>
    /// Initiates a renewal flow (FR-SUB-040 — discriminated union response).
    /// The response is always { mode: "manual", manual: { ... } } regardless of the
    /// requested channel — the Paymob gateway path was removed 2026-07-17 (it was
    /// disabled everywhere and already fell through to manual).
    /// Price = Teacher.StudentCapacity × the per-student rate, snapshotted onto the
    /// pending row (BR-SUB-009).
    /// </summary>
    Task<Result<RenewInitiateResponse>> InitiateRenewalAsync(
        long teacherId, RenewInitiateRequest request);

    /// <summary>
    /// Teacher submits a capacity-increase request (increase-only; one live Pending
    /// request per teacher). Approval is a super-admin operation — capacity applies
    /// immediately on approve, the new price from the NEXT renewal.
    /// </summary>
    /// <param name="teacherId">The tenant teacher (resolved from the JWT).</param>
    /// <param name="actingUserId">The submitting user (teacher, or assistant acting for the tutor) — audit only.</param>
    /// <param name="request">Requested capacity + optional note.</param>
    Task<Result<CapacityRequestDto>> SubmitCapacityRequestAsync(
        long teacherId, long actingUserId, CreateCapacityRequestRequest request);

    /// <summary>
    /// Paginated history of the teacher's own capacity requests, newest first, all statuses.
    /// </summary>
    Task<Result<PaginatedResponse<List<CapacityRequestDto>>>> GetCapacityRequestsPagedAsync(
        long teacherId, int page, int pageSize);

    /// <summary>
    /// Teacher withdraws a Pending capacity request. Terminal rows are kept for audit.
    /// </summary>
    Task<Result<CapacityRequestDto>> CancelCapacityRequestAsync(
        long teacherId, long actingUserId, long requestId);

    /// <summary>
    /// Tutor submits the transaction reference they paid with externally.
    /// Transitions the pending payment to AwaitingSuperAdminApproval (FR-SUB-033).
    /// Payment details are encrypted before storage (REQ-SUB-NFR-004).
    /// </summary>
    Task<Result<RenewStatusDto>> SubmitManualAsync(
        long teacherId, ManualSubmitRequest request);

    /// <summary>
    /// Polling endpoint while awaiting webhook callback or admin review.
    /// </summary>
    Task<Result<RenewStatusDto>> GetRenewalStatusAsync(
        long teacherId, long pendingPaymentId);

    /// <summary>
    /// Internal confirmation pipeline — called by
    /// AdminSubscriptionService.ApprovePendingAsync (the Paymob webhook caller was
    /// removed 2026-07-17; the null resolvedByUserId path remains for any future
    /// gateway integration).
    ///
    /// Runs §6.3's full sequence:
    ///   - Capture paymentConfirmedAt = UtcNow ONCE (BR-SUB-013).
    ///   - Begin Serializable transaction.
    ///   - Pessimistic-lock currentSub.
    ///   - Compute StartDate per §6.2 (currentSub.EndDate or paymentConfirmedAt).
    ///   - FlipCurrentAndInsertNew + persist pending row resolution.
    ///   - Bounded retry on DbUpdateConcurrencyException / unique-violation (max 2).
    ///   - Commit, then synchronously invalidate cache, then enqueue
    ///     IRenewalNotificationJob (fire-and-forget).
    ///
    /// Idempotent on Status = Confirmed (EC-03): returns Success without re-running.
    /// </summary>
    /// <param name="pendingPaymentId">The pending payment to confirm.</param>
    /// <param name="resolvedByUserId">Admin user id, or null for webhook path.</param>
    Task<Result<ConfirmPaymentResultDto>> ConfirmPaymentAsync(
        long pendingPaymentId, long? resolvedByUserId);
}