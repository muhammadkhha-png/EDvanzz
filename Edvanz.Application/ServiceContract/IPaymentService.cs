using Edvanz.Application.Dtos;
using Edvanz.Application.Dtos.Payment;

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

    // ══════════════════════════════════════════════
    // COLLECTOR VIEW (REQ-PAY-013/014)
    // ══════════════════════════════════════════════

    /// <summary>
    /// Gets the User Collection View (tutor only).
    /// REQ-PAY-013: Per-user collection breakdown.
    /// REQ-PAY-014: Only tutor has access.
    /// </summary>
    Task<Result<List<CollectorSummaryDto>>> GetCollectorSummaryAsync(
        long teacherId, DateTime? startDate, DateTime? endDate);

    // ══════════════════════════════════════════════
    // WALLET MANAGEMENT (REQ-PAY-034 through 038)
    // ══════════════════════════════════════════════

    /// <summary>
    /// Gets all assistant wallets for a teacher.
    /// REQ-PAY-035: View current wallet balance of each assistant.
    /// </summary>
    Task<Result<List<AssistantWalletDto>>> GetAllWalletsAsync(long teacherId);

    /// <summary>
    /// Gets detailed wallet for a specific assistant.
    /// REQ-PAY-035: Itemized collection list.
    /// </summary>
    Task<Result<AssistantWalletDto>> GetWalletDetailAsync(long teacherId, long assistantId);

    /// <summary>
    /// Resets an assistant's wallet to zero.
    /// REQ-PAY-036: Tutor confirms cash handover.
    /// REQ-PAY-037: Permanent ledger event.
    /// REQ-PAY-038: Fresh accumulation begins from zero.
    /// </summary>
    Task<Result<WalletResetLogDto>> ResetWalletAsync(WalletResetDto dto);

    // ══════════════════════════════════════════════
    // DASHBOARD (REQ-PAY-039 through 044)
    // ══════════════════════════════════════════════

    /// <summary>
    /// Gets the Payment Overview Dashboard.
    /// REQ-PAY-039/040/041/042: Expected, collected, remaining revenue.
    /// REQ-PAY-043: Filterable by session, group, payment type, date range.
    /// </summary>
    Task<Result<PaymentDashboardDto>> GetDashboardAsync(
        long teacherId, PaymentDashboardFilterDto filter);

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
}