using Edvanz.Application.Dtos;
using Edvanz.Application.Dtos.Payment;
using Edvanz.Domain.Enums;

namespace Edvanz.Application.ServiceContract;

/// <summary>
/// Defines the contract for Payment Module operations (Module 4).
/// Covers: payment collection, editing, deletion, unpaid overview,
/// custom amounts, assistant wallets, dashboard, departure, transfer,
/// offline sync, and student/parent views.
///
/// All methods return Result&lt;T&gt; for consistent error handling.
/// All methods are async per system architecture requirements.
///
/// TRANSACTION SAFETY:
/// All write operations use the ownsTransaction pattern:
///   bool ownsTransaction = !_unitOfWork.HasActiveTransaction;
/// This makes them safe for both standalone calls and nested calls.
/// </summary>
public interface IPaymentService
{
    // ══════════════════════════════════════════════
    // PAYMENT COLLECTION (REQ-PAY-001 through 010)
    // ══════════════════════════════════════════════

    /// <summary>
    /// Collects a payment from a student.
    /// REQ-PAY-001/002: All four collection methods produce a unified record.
    /// REQ-PAY-015/016: Amount pre-filled from session or custom amount.
    /// REQ-PAY-018/019: Automatically assigned to earliest unpaid period (BR-PAY-001).
    /// REQ-PAY-020/026: Duplicate detection and already-paid warning.
    /// REQ-PAY-021/025: Pro-rating for first period (BR-PAY-005).
    /// </summary>
    Task<Result<CollectPaymentResultDto>> CollectPaymentAsync(CollectPaymentDto dto);

    /// <summary>
    /// Gets the payment status for a student on the collection screen.
    /// REQ-PAY-NFR-006: Displayed immediately after student identification.
    /// </summary>
    Task<Result<PaymentStatusDto>> GetStudentPaymentStatusAsync(
        long teacherId, long teacherStudentId);

    /// <summary>
    /// Checks for duplicate payments (same day or already paid).
    /// REQ-PAY-020/026: Pre-collection check.
    /// </summary>
    Task<Result<DuplicateCheckResultDto>> CheckDuplicatePaymentAsync(
        long teacherId, long teacherStudentId);

    // ══════════════════════════════════════════════
    // PAYMENT EDIT/DELETE (REQ-PAY-027, BR-PAY-002)
    // ══════════════════════════════════════════════

    /// <summary>
    /// Edits an existing payment transaction.
    /// BR-PAY-002: Only the tutor role may edit.
    /// </summary>
    Task<Result<PaymentTransactionDto>> EditPaymentAsync(EditPaymentDto dto);

    /// <summary>
    /// Soft-deletes a payment transaction.
    /// BR-PAY-002: Only the tutor role may delete.
    /// </summary>
    Task<Result<bool>> DeletePaymentAsync(long teacherId, long transactionId, long deletedByUserId);

    /// <summary>
    /// Gets the edit history for a payment transaction.
    /// </summary>
    Task<Result<List<PaymentEditLogDto>>> GetPaymentEditHistoryAsync(
        long teacherId, long transactionId);

    // ══════════════════════════════════════════════
    // STUDENT PAYMENT HISTORY (REQ-PAY-052)
    // ══════════════════════════════════════════════

    /// <summary>
    /// Gets the complete payment history for a student.
    /// REQ-PAY-052: Single Student Payment History.
    /// REQ-PAY-092: Unified timeline across all sessions.
    /// </summary>
    Task<Result<PaginatedResponse<PaymentHistoryDto>>> GetStudentPaymentHistoryAsync(
        long teacherId, long teacherStudentId,
        DateTime? startDate, DateTime? endDate,
        int page, int pageSize);

    // ══════════════════════════════════════════════
    // CUSTOM AMOUNT (REQ-PAY-016, BR-PAY-003)
    // ══════════════════════════════════════════════

    /// <summary>
    /// Gets the custom payment amount for a student (null = use session default).
    /// </summary>
    Task<Result<decimal?>> GetCustomAmountAsync(long teacherId, long teacherStudentId);

    /// <summary>
    /// Sets a custom payment amount for a student.
    /// REQ-PAY-016: Overrides session default for all future collections.
    /// BR-PAY-003: Not affected by session amount changes.
    /// </summary>
    Task<Result<bool>> SetCustomAmountAsync(SetCustomAmountDto dto);

    // ══════════════════════════════════════════════
    // UNPAID OVERVIEW (REQ-PAY-028 through 033)
    // ══════════════════════════════════════════════

    /// <summary>
    /// Gets the paginated unpaid students overview.
    /// REQ-PAY-031/032/033: Filterable with search.
    /// </summary>
    Task<Result<PaginatedResponse<List<UnpaidStudentDto>>>> GetUnpaidStudentsAsync(
        long teacherId, UnpaidStudentsFilterDto filter);

    /// <summary>
    /// Gets the unpaid student count badge for a session.
    /// REQ-PAY-028: Notification badge on session view.
    /// </summary>
    Task<Result<int>> GetUnpaidCountBySessionAsync(long teacherId, long sessionId);

    /// <summary>
    /// Gets the Paid / Pro-rated / Unpaid students overview counts.
    /// REQ-PAY-031: Overview counts alongside the filterable unpaid list.
    /// </summary>
    Task<Result<StudentPaymentStatusCountsDto>> GetStudentPaymentStatusCountsAsync(long teacherId);

    // ══════════════════════════════════════════════
    // COLLECTOR VIEW (REQ-PAY-013/014)
    // ══════════════════════════════════════════════

    /// <summary>
    /// Gets the User Collection View.
    /// REQ-PAY-013: Per-user collection breakdown.
    /// REQ-PAY-014: The full view (all collectors) is tutor-only; when
    /// <paramref name="scopeToCollectorUserId"/> is supplied (assistant caller) the result is
    /// filtered to that single collector's own records.
    /// TODO(assistant-dashboard): interim own-scoping; the dedicated assistant collection view
    /// is to be built end-to-end by frontend + backend.
    /// </summary>
    Task<Result<List<CollectorSummaryDto>>> GetCollectorSummaryAsync(
        long teacherId, DateTime? startDate, DateTime? endDate, long? scopeToCollectorUserId = null);

    // ══════════════════════════════════════════════
    // WALLET MANAGEMENT (REQ-PAY-034 through 038)
    // ══════════════════════════════════════════════

    /// <summary>
    /// Gets all assistant wallets for a teacher, plus the combined total currently
    /// held across all assistants.
    /// REQ-PAY-035: View current wallet balance of each assistant.
    /// When <paramref name="scopeToAssistantUserId"/> is supplied (assistant caller) the result
    /// contains ONLY that assistant's own wallet (never peers'), and the combined total equals
    /// their own balance.
    /// TODO(assistant-dashboard): interim own-scoping; proper assistant dashboard TBD (frontend + backend).
    /// </summary>
    Task<Result<AssistantWalletsSummaryDto>> GetAllWalletsAsync(long teacherId, long? scopeToAssistantUserId = null);

    /// <summary>
    /// Gets detailed wallet for a specific assistant.
    /// REQ-PAY-035: Itemized collection list.
    /// When <paramref name="restrictToAssistantUserId"/> is supplied (assistant caller) the
    /// requested <paramref name="assistantId"/> is IGNORED and the caller's OWN wallet (resolved
    /// by their user id) is returned — an assistant can never read a peer's wallet.
    /// TODO(assistant-dashboard): interim own-scoping; proper assistant dashboard TBD (frontend + backend).
    /// </summary>
    Task<Result<AssistantWalletDto>> GetWalletDetailAsync(
        long teacherId, long assistantId, long? restrictToAssistantUserId = null);

    /// <summary>
    /// Resets an assistant's wallet to zero.
    /// REQ-PAY-036: Tutor confirms cash handover.
    /// REQ-PAY-037: Permanent ledger event.
    /// REQ-PAY-038: Fresh accumulation begins from zero.
    /// </summary>
    Task<Result<WalletResetLogDto>> ResetWalletAsync(WalletResetDto dto);

    /// <summary>
    /// Partial assistant-wallet withdrawal (api/v1 screens) — the tutor takes an amount of
    /// collected cash from an assistant. Generalizes <see cref="ResetWalletAsync"/> to a partial
    /// amount (defaults to the full balance): validates funds (409 on insufficient), decrements
    /// the balance, logs a WalletResetLog ledger entry, and is RowVersion-concurrency-safe
    /// (clean 409 on a concurrent update). Tutor-only at the controller. No schema change.
    /// </summary>
    Task<Result<WalletWithdrawalResult>> WithdrawFromWalletAsync(
        long teacherId, long assistantId, decimal? amount, long withdrawnByUserId);

    // ══════════════════════════════════════════════
    // DASHBOARD (REQ-PAY-039 through 044)
    // ══════════════════════════════════════════════

    /// <summary>
    /// Gets the Payment Overview Dashboard.
    /// REQ-PAY-039/040/041/042: Expected, collected, remaining revenue.
    /// REQ-PAY-043: Filterable by session, group, payment type, date range.
    /// When <paramref name="scopeToCollectorUserId"/> is supplied (assistant caller) the result is
    /// scoped to that assistant's OWN collections: <c>CollectedRevenue</c> = their own collected,
    /// while the teacher-wide figures (<c>ExpectedRevenue</c>/<c>RemainingRevenue</c>/per-session)
    /// come back <c>null</c> so the client shows an assistant-scoped view without confusing null and 0.
    /// TODO(assistant-dashboard): interim own-scoping; a real assistant dashboard (its own expected/
    /// target semantics) is to be designed and built end-to-end by frontend + backend.
    /// </summary>
    Task<Result<PaymentDashboardDto>> GetDashboardAsync(
        long teacherId, PaymentDashboardFilterDto filter, long? scopeToCollectorUserId = null);

    /// <summary>
    /// Gets the "Collected by Sessions" card data: one row per currently
    /// active session, with collected amount, paid/total student counts,
    /// and progress percentage.
    /// REQ-PAY-043: Per-session collection progress while the session is active.
    /// </summary>
    Task<Result<List<SessionCollectionSummaryDto>>> GetSessionsCollectionSummaryAsync(long teacherId);

    // ══════════════════════════════════════════════
    // DEPARTURE (REQ-PAY-066 through 075)
    // ══════════════════════════════════════════════

    /// <summary>
    /// Calculates and returns the departure summary before confirmation.
    /// REQ-PAY-067/068: Pro-rated obligation calculation.
    /// REQ-PAY-072: Departure summary screen.
    /// </summary>
    Task<Result<DepartureSummaryDto>> GetDepartureSummaryAsync(
        long teacherId, long teacherStudentId);

    /// <summary>
    /// Confirms a student departure.
    /// REQ-PAY-073: Unassign, record departure event, handle refund/charge.
    /// REQ-PAY-075: Optional tutor override.
    /// </summary>
    Task<Result<StudentDepartureDto>> ConfirmDepartureAsync(ConfirmDepartureDto dto);

    /// <summary>Teacher-wide paged list of departed students (search by name/code), newest first.</summary>
    Task<Result<DeparturesResponse>> GetDeparturesAsync(long teacherId, string? search, int page, int limit);

    // ══════════════════════════════════════════════
    // SESSION TRANSFER (REQ-PAY-085 through 092)
    // ══════════════════════════════════════════════

    /// <summary>
    /// Calculates and returns the pre-transfer financial summary.
    /// REQ-PAY-088: Pre-transfer summary shown before confirmation.
    /// </summary>
    Task<Result<TransferSummaryDto>> GetTransferSummaryAsync(
        long teacherId, long teacherStudentId,
        long sourceSessionId, long destinationSessionId);

    /// <summary>
    /// Confirms a session transfer with payment data continuity.
    /// REQ-PAY-085/086/089/090/091: Full payment history preserved.
    /// </summary>
    Task<Result<SessionTransferEventDto>> ConfirmTransferAsync(ConfirmTransferDto dto);

    // ══════════════════════════════════════════════
    // OFFLINE SYNC (REQ-PAY-076 through 084)
    // ══════════════════════════════════════════════

    /// <summary>
    /// Syncs offline-collected payment records to the server.
    /// REQ-PAY-080/081: Background sync on reconnection.
    /// REQ-PAY-082: Conflict detection and resolution.
    /// </summary>
    Task<Result<PaymentSyncResultDto>> SyncOfflinePaymentsAsync(OfflinePaymentSyncRequestDto dto);

    // ══════════════════════════════════════════════
    // STUDENT/PARENT VIEW
    // ══════════════════════════════════════════════

    /// <summary>
    /// Gets payment data for a student/parent viewing their own payments.
    /// Gated by TeacherConfiguration visibility settings.
    /// </summary>
    Task<Result<StudentPaymentViewDto>> GetStudentPaymentViewAsync(
        long teacherId, long teacherStudentId);

    /// <summary>
    /// Builds the full student/parent "Payment" tracking screen in one call: header, the
    /// single Upcoming month, the Paid section, and the Overdue section — pivoted on the
    /// teacher's local current month. Gated by the caller-specific visibility flag
    /// (<see cref="Domain.Enums.PaymentViewerType"/> → StudentVisibilityPayment /
    /// ParentVisibilityPayment); returns 403 when disabled (fail-closed on missing config).
    /// </summary>
    Task<Result<StudentPaymentTrackingDto>> GetStudentPaymentTrackingAsync(
        long teacherId, long teacherStudentId, PaymentViewerType viewer);

    // ══════════════════════════════════════════════
    // INTEGRATION HOOKS (called by other modules)
    // ══════════════════════════════════════════════

    /// <summary>
    /// Called by SessionService when a student is assigned to a session.
    /// Generates initial payment periods and creates the payment counter.
    /// </summary>
    Task<Result<bool>> OnStudentAssignedToSessionAsync(
        long teacherId, long teacherStudentId, long sessionId,
        string sessionName, DateTime assignedAt);

    /// <summary>
    /// Called by SessionService when a session's <c>SessionAmount</c> changes. Re-prices every
    /// FUTURE (next month onward, teacher-local) still-owed period of the session to
    /// <paramref name="newAmount"/>, EXCEPT students who carry their own custom price (BR-PAY-003).
    /// The current month and past/paid periods are left untouched. Runs on the caller's transaction
    /// (does not open/commit its own).
    /// </summary>
    Task<Result<bool>> OnSessionAmountChangedAsync(long teacherId, long sessionId, decimal newAmount);

    /// <summary>
    /// Called by SessionService after a session's date window changed. Billing periods are only
    /// generated ONCE — when a student is assigned — and only as far as the session's end date AT
    /// THAT MOMENT. Extending the end date afterwards therefore left the already-assigned students
    /// with no obligation for the newly-covered months: the session silently vanished from the
    /// payment screens for those months and its students read as "paid" with nothing due.
    ///
    /// This BACKFILLS the gap: for every currently assigned student of a <c>Monthly</c> session it
    /// creates the missing monthly periods, from the month after their latest existing period through
    /// the session's end month, at their effective price (custom amount, else the session amount).
    /// Idempotent and additive — existing periods are never touched, nothing is created past the end
    /// date, and a student who is already covered gets nothing. Safe to call on every session update,
    /// so re-saving a session also REPAIRS historical gaps.
    /// Runs on the caller's transaction (does not open/commit its own).
    /// </summary>
    Task<Result<bool>> BackfillSessionPeriodsThroughEndDateAsync(long teacherId, long sessionId);

    /// <summary>
    /// Called by SessionService when a student is unassigned from a session.
    /// Preserves complete payment history.
    /// </summary>
    Task<Result<bool>> OnStudentUnassignedFromSessionAsync(
        long teacherId, long teacherStudentId);

    /// <summary>
    /// Called by SessionService before a session is hard-deleted.
    /// Nullifies SessionId on payment records — denormalized data preserved.
    /// </summary>
    Task<Result<bool>> OnSessionDeletingAsync(long teacherId, long sessionId);

    /// <summary>
    /// Called by TeacherStudentService during permanent student purge.
    /// Nullifies TeacherStudentId on all payment records.
    /// </summary>
    Task<Result<bool>> OnStudentPermanentlyDeletedAsync(long teacherStudentId);

    /// <summary>
    /// Ensures an AssistantWallet record exists for the given assistant.
    /// Should be called when an assistant is created (from AssistantService)
    /// or as a safety check during payment collection.
    /// Creates the wallet if it doesn't already exist.
    /// </summary>
    /// <param name="teacherId">The owning teacher's ID.</param>
    /// <param name="assistantId">The Assistant entity's ID.</param>
    /// <param name="assistantUserId">The assistant's User.Id for wallet lookup.</param>
    Task<Result<bool>> EnsureAssistantWalletExistsAsync(
        long teacherId, long assistantId, long assistantUserId);
    /// <summary>
    /// Collects payments for multiple students in a single request ("Mark N students as Paid").
    /// Best-effort partial success: each student is processed independently via
    /// <see cref="CollectPaymentAsync"/> — the single source of truth for all payment
    /// business rules — and reported as Collected, NeedsConfirmation, or Failed.
    /// No explicit REQ-PAY covers batch collection (it is UI-derived); per-student behavior
    /// is governed by REQ-PAY-001/002/018/019/020/026 and BR-PAY-001 via the reused path.
    /// <paramref name="teacherId"/> and <paramref name="collectedByUserId"/> are supplied
    /// by the presentation layer from the JWT, never from the request body.
    /// </summary>
    Task<Result<BatchCollectResultDto>> BatchCollectPaymentAsync(
        BatchCollectPaymentDto dto, long teacherId, long collectedByUserId);

    /// <summary>
    /// Batch-edits multiple payment transactions in a single request (D2 — UI "Saved N changes").
    /// Best-effort partial success: each transaction is edited independently via
    /// <see cref="EditPaymentAsync"/> — the single source of truth for all edit business rules
    /// (PaymentEditLog, period/counter reversal, transaction ownership) — and reported as
    /// Succeeded or Failed. One item's failure never rolls back the others.
    /// BR-PAY-002: tutor-only; <paramref name="teacherId"/> and <paramref name="editedByUserId"/>
    /// are supplied by the presentation layer from the JWT, never from the request body.
    /// </summary>
    Task<Result<BatchEditResultDto>> BatchEditPaymentAsync(
        BatchEditPaymentDto dto, long teacherId, long editedByUserId);
    /// <summary>
    /// Batch-reverts multiple payment transactions in a single request (D1 — UI "Revert (N students)").
    /// A revert is expressed as an edit that zeroes the collected amount and marks the transaction
    /// Unpaid, applied independently per item via <see cref="EditPaymentAsync"/> — the single source
    /// of truth for PaymentEditLog, period reversal, counter reversal, ownership, and transaction
    /// ownership. No new reversal logic is introduced. Best-effort partial success: one item's
    /// failure never rolls back the others. BR-PAY-002: tutor-only; <paramref name="teacherId"/> and
    /// <paramref name="editedByUserId"/> come from the JWT, never the request body.
    /// </summary>
    Task<Result<BatchRevertResultDto>> BatchRevertPaymentAsync(
        BatchRevertPaymentDto dto, long teacherId, long editedByUserId);
}