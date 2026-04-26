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
    /// Paginated payment history (REQ-SUB-022 / FR-SUB-039).
    /// Phone numbers and reference details are masked in the returned DTOs (BR-SUB-011).
    /// </summary>
    Task<Result<PaginatedResponse<List<SubscriptionHistoryItemDto>>>> GetHistoryPagedAsync(
        long teacherId, int page, int pageSize);

    /// <summary>
    /// Initiates a renewal flow (FR-SUB-040 — discriminated union response).
    /// When PaymobOptions.Enabled = false, the service flips the response to
    /// { mode: "manual", manual: { ... } } regardless of the requested channel
    /// (D-04 / FR-SUB-034).
    /// </summary>
    Task<Result<RenewInitiateResponse>> InitiateRenewalAsync(
        long teacherId, RenewInitiateRequest request);

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
    /// Internal confirmation pipeline — called by:
    ///   (a) PaymobWebhookController on a successful webhook callback,
    ///   (b) AdminSubscriptionService.ApprovePendingAsync.
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