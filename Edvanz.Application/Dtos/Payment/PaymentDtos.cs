using Edvanz.Domain.Enums;

namespace Edvanz.Application.Dtos.Payment;

// ══════════════════════════════════════════════════════════════════════════
// PAYMENT COLLECTION DTOs
// ══════════════════════════════════════════════════════════════════════════

/// <summary>
/// Input DTO for collecting a payment from a student.
/// REQ-PAY-001: Supports all four collection methods.
/// REQ-PAY-012: Captures all required metadata.
/// </summary>
public class CollectPaymentDto
{
    public long TeacherId { get; set; }
    public long TeacherStudentId { get; set; }
    public long SessionId { get; set; }
    public decimal Amount { get; set; }
    public PaymentCollectionMethod PaymentMethod { get; set; }
    /// <summary>
    /// The user collecting this payment (auto-set from auth context if null).
    /// REQ-PAY-011: Automatically associated with logged-in user.
    /// </summary>
    public long? CollectedByUserId { get; set; }
    /// <summary>
    /// Whether the user has confirmed a same-day duplicate warning.
    /// REQ-PAY-020: Second payment on same day requires confirmation.
    /// </summary>
    public bool DuplicateConfirmed { get; set; } = false;
    /// <summary>
    /// Whether the user has confirmed payment for an already-paid period.
    /// REQ-PAY-026: Warning before allowing payment for a fully-paid period.
    /// PAY-9 / INERT BY DESIGN: the monthly engine already accepts up to one month in advance, and
    /// paying beyond that is a hard 422 (<c>PaymentAmountExceedsAdvanceLimit</c>) — so once a student
    /// is "already paid" there is genuinely nothing left to collect and no override is possible. The
    /// flag is kept for wire-compat and the concurrency-retry path; the already-paid message is
    /// worded as a terminal statement (not a "proceed?" prompt) to match.
    /// </summary>
    public bool AlreadyPaidConfirmed { get; set; } = false;
    /// <summary>
    /// Online payment reference (for online payment methods only).
    /// REQ-PAY-008: Transaction reference.
    /// </summary>
    public string? OnlineTransactionRef { get; set; }
    /// <summary>
    /// Whether this is an offline-collected record being synced.
    /// REQ-PAY-079: Offline records with full metadata.
    /// </summary>
    public bool IsOfflineRecord { get; set; } = false;
    public string? OfflineDeviceId { get; set; }
    public DateTime? OfflineCollectedAt { get; set; }

    /// <summary>
    /// Client-generated unique id for offline records (uuid). Replaying a
    /// record whose ClientEntryId already exists for this teacher returns
    /// success without recording again (exactly-once sync). Ignored for
    /// online collections.
    /// </summary>
    public string? ClientEntryId { get; set; }
}

/// <summary>
/// Result DTO returned after payment collection attempt.
/// Contains warnings, pro-rate info, and the created transaction.
/// </summary>
public class CollectPaymentResultDto
{
    public PaymentTransactionDto? Transaction { get; set; }
    /// <summary>
    /// Whether a same-day duplicate was detected.
    /// REQ-PAY-020: Requires explicit confirmation.
    /// </summary>
    public bool IsSameDayDuplicate { get; set; } = false;
    public decimal? TodayPaidAmount { get; set; }
    public string? TodayPaidSessionName { get; set; }
    /// <summary>
    /// Whether the period was already fully paid.
    /// REQ-PAY-026: Warning to collector.
    /// </summary>
    public bool IsAlreadyPaid { get; set; } = false;
    /// <summary>
    /// Whether a pro-rated amount was applied.
    /// REQ-PAY-025: Pro-rate indicator.
    /// </summary>
    public bool IsProRated { get; set; } = false;
    public string? ProRatedTierLabel { get; set; }
    public decimal? OriginalAmount { get; set; }
    public decimal? ProRatedAmount { get; set; }
}

// ══════════════════════════════════════════════════════════════════════════
// PAYMENT STATUS DTOs
// ══════════════════════════════════════════════════════════════════════════

/// <summary>
/// Student payment status for the collection screen.
/// REQ-PAY-NFR-006: Displayed prominently after student identification.
/// </summary>
public class PaymentStatusDto
{
    public long TeacherStudentId { get; set; }
    public string StudentName { get; set; } = null!;
    public string StudentCode { get; set; } = null!;
    public string? SessionName { get; set; }
    public PaymentStatus CurrentStatus { get; set; }
    public decimal AmountDue { get; set; }
    public decimal AmountPaid { get; set; }
    public decimal Outstanding { get; set; }
    public string? PeriodLabel { get; set; }
    public int ConsecutiveUnpaid { get; set; }
    public bool IsProRated { get; set; }
    public string? ProRatedTierLabel { get; set; }
    public bool HasCustomAmount { get; set; }
    public decimal? CustomAmount { get; set; }
}

/// <summary>
/// Result of a duplicate check for a student.
/// REQ-PAY-020/026: Same-day and already-paid detection.
/// </summary>
public class DuplicateCheckResultDto
{
    public bool HasSameDayPayment { get; set; }
    public decimal? TodayPaidAmount { get; set; }
    public string? TodayPaidPeriodLabel { get; set; }
    public bool IsCurrentPeriodPaid { get; set; }
    public string? CurrentPeriodLabel { get; set; }
}

// ══════════════════════════════════════════════════════════════════════════
// PAYMENT TRANSACTION DTOs
// ══════════════════════════════════════════════════════════════════════════

/// <summary>
/// Display DTO for a single payment transaction.
/// REQ-PAY-012: Shows all required metadata.
/// </summary>
public class PaymentTransactionDto
{
    public long Id { get; set; }
    public long? TeacherStudentId { get; set; }
    public string? StudentName { get; set; }
    public string? StudentCode { get; set; }
    public string SessionName { get; set; } = null!;
    public long? SessionId { get; set; }
    public decimal AmountDue { get; set; }
    public decimal AmountPaid { get; set; }
    public PaymentCollectionMethod PaymentMethod { get; set; }
    public PaymentStatus PaymentTransactionStatus { get; set; }
    public long? CollectedByUserId { get; set; }
    public string? CollectedByUserName { get; set; }
    public DateTime CollectedAt { get; set; }
    public DateTime LocalCollectedAt { get; set; }
    public bool IsPartial { get; set; }
    public bool IsProRated { get; set; }
    public string? ProRatedTierLabel { get; set; }
    public bool IsOnlinePayment { get; set; }
    public string? OnlineTransactionRef { get; set; }
    public string? PeriodLabel { get; set; }
    public bool IsEdited { get; set; }
}

/// <summary>
/// Student payment history with period grouping.
/// REQ-PAY-052: Single Student Payment History.
/// </summary>
public class PaymentHistoryDto
{
    public long TeacherStudentId { get; set; }
    public string StudentName { get; set; } = null!;
    public string StudentCode { get; set; } = null!;
    public decimal TotalAmountPaid { get; set; }
    public decimal TotalOutstanding { get; set; }
    public List<PaymentPeriodDto> Periods { get; set; } = new();
    public List<SessionTransferEventDto> Transfers { get; set; } = new();
    public List<StudentDepartureDto> Departures { get; set; } = new();
}

/// <summary>
/// Display DTO for a payment period.
/// </summary>
public class PaymentPeriodDto
{
    public long Id { get; set; }
    public string SessionName { get; set; } = null!;
    public PeriodType PeriodType { get; set; }
    public DateTime PeriodStart { get; set; }
    public DateTime PeriodEnd { get; set; }
    public decimal AmountDue { get; set; }
    public decimal AmountPaid { get; set; }
    public PaymentStatus PaymentStatus { get; set; }
    public bool IsProRated { get; set; }
    public decimal ProRatedFraction { get; set; }
    public int PeriodSequence { get; set; }
    public bool IsCarriedForward { get; set; }
    public string? OriginSessionName { get; set; }
    public List<PaymentTransactionDto> Transactions { get; set; } = new();
}

// ══════════════════════════════════════════════════════════════════════════
// EDIT DTOs
// ══════════════════════════════════════════════════════════════════════════

/// <summary>
/// Input DTO for editing a payment transaction.
/// BR-PAY-002: Only tutor may edit.
/// </summary>
public class EditPaymentDto
{
    public long TeacherId { get; set; }
    public long TransactionId { get; set; }
    public decimal? NewAmount { get; set; }
    public PaymentStatus? NewStatus { get; set; }
    public long? NewPaymentPeriodId { get; set; }
    public string? EditReason { get; set; }
    public long EditedByUserId { get; set; }
}

/// <summary>
/// Display DTO for a payment edit log entry.
/// </summary>
public class PaymentEditLogDto
{
    public long Id { get; set; }
    public PaymentEditAction EditAction { get; set; }
    public decimal PreviousAmount { get; set; }
    public decimal NewAmount { get; set; }
    public PaymentStatus PreviousStatus { get; set; }
    public PaymentStatus NewStatus { get; set; }
    public DateTime EditedAt { get; set; }
    public long? EditedByUserId { get; set; }
    public string? EditReason { get; set; }
}

// ══════════════════════════════════════════════════════════════════════════
// CUSTOM AMOUNT DTOs
// ══════════════════════════════════════════════════════════════════════════

/// <summary>
/// Input DTO for setting a custom payment amount for a student.
/// REQ-PAY-016: Custom amount overrides session default.
/// </summary>
public class SetCustomAmountDto
{
    public long TeacherId { get; set; }
    public long TeacherStudentId { get; set; }
    public decimal? CustomAmount { get; set; }
}

// ══════════════════════════════════════════════════════════════════════════
// UNPAID OVERVIEW DTOs
// ══════════════════════════════════════════════════════════════════════════

/// <summary>
/// Filter criteria for the Unpaid Students View.
/// REQ-PAY-032: Filterable by session, group, payment type, consecutive count.
/// </summary>
public class UnpaidStudentsFilterDto
{
    public long? SessionId { get; set; }
    public long? SessionGroupId { get; set; }
    public PaymentType? PaymentType { get; set; }
    public int? MinConsecutiveUnpaid { get; set; }
    public string? Search { get; set; }

    /// <summary>
    /// Optional arrears cutoff as <c>"YYYY-MM"</c>. Outstanding amounts and unpaid-period counts
    /// are computed only through the END of this month (CLAUDE.md §7.4), so pre-generated future
    /// periods are never reported as owed. Omitted → the teacher's current local (Africa/Cairo)
    /// month. A malformed value returns 422.
    /// </summary>
    public string? AsOfMonth { get; set; }

    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

/// <summary>
/// Display DTO for an unpaid student row.
/// REQ-PAY-031: Displays student details, unpaid periods, outstanding amount.
/// </summary>
public class UnpaidStudentDto
{
    public long TeacherStudentId { get; set; }
    public string StudentName { get; set; } = null!;
    public string StudentCode { get; set; } = null!;
    public string? SessionName { get; set; }
    public long? SessionId { get; set; }
    public int ConsecutiveUnpaid { get; set; }
    public int TotalUnpaidPeriods { get; set; }
    public decimal TotalOutstanding { get; set; }
    public DateTime? LastPaymentDate { get; set; }
    public List<string> UnpaidPeriodLabels { get; set; } = new();
}

/// <summary>
/// Counts for the Paid / Pro-rated / Unpaid students overview card.
/// A student is Paid when they have no outstanding period; among students
/// with an outstanding period, Pro-rated breaks out those whose earliest
/// outstanding period is a prorated first period (REQ-PAY-021/022) from the
/// remaining regular Unpaid students.
/// </summary>
public class StudentPaymentStatusCountsDto
{
    public int PaidCount { get; set; }
    public int ProRatedCount { get; set; }
    public int UnpaidCount { get; set; }
}

// ══════════════════════════════════════════════════════════════════════════
// COLLECTOR VIEW DTOs
// ══════════════════════════════════════════════════════════════════════════

/// <summary>
/// Display DTO for the collector summary view.
/// REQ-PAY-013: Per-user collection breakdown.
/// </summary>
public class CollectorSummaryDto
{
    public long UserId { get; set; }
    public string UserName { get; set; } = null!;
    public string UserRole { get; set; } = null!;
    public decimal TotalCollected { get; set; }
    public int TransactionCount { get; set; }
    public decimal? CurrentWalletBalance { get; set; }
}

// ══════════════════════════════════════════════════════════════════════════
// WALLET DTOs
// ══════════════════════════════════════════════════════════════════════════

/// <summary>
/// Display DTO for an assistant's wallet.
/// REQ-PAY-035: Wallet balance and itemized collection list.
/// </summary>
public class AssistantWalletDto
{
    public long AssistantId { get; set; }
    public string AssistantName { get; set; } = null!;
    public decimal CurrentBalance { get; set; }
    public decimal TotalCollected { get; set; }
    public int TransactionCount { get; set; }
    public DateTime? LastCollectionAt { get; set; }
}

/// <summary>
/// Aggregated view of all assistant wallets for a teacher.
/// REQ-PAY-035: Tutor views current wallet balance of each assistant, plus the
/// combined total currently held across all assistants.
/// </summary>
public class AssistantWalletsSummaryDto
{
    /// <summary>Sum of <see cref="AssistantWalletDto.CurrentBalance"/> across all assistants.</summary>
    public decimal TotalCurrentBalance { get; set; }
    public List<AssistantWalletDto> Assistants { get; set; } = new();
}

/// <summary>
/// Input DTO for resetting an assistant's wallet.
/// REQ-PAY-036: Tutor confirms cash handover.
/// </summary>
public class WalletResetDto
{
    public long TeacherId { get; set; }
    public long AssistantId { get; set; }
    public long ResetByUserId { get; set; }
}

/// <summary>
/// Display DTO for a wallet reset log entry.
/// REQ-PAY-037: Permanent ledger event.
/// </summary>
public class WalletResetLogDto
{
    public long Id { get; set; }
    public string? AssistantName { get; set; }
    public decimal AmountReset { get; set; }
    public DateTime ResetAt { get; set; }
    public long ResetByUserId { get; set; }
}

// ══════════════════════════════════════════════════════════════════════════
// DASHBOARD DTOs
// ══════════════════════════════════════════════════════════════════════════

/// <summary>
/// Filter criteria for the Payment Overview Dashboard.
/// REQ-PAY-043: Filterable by session, group, payment type, date range.
/// </summary>
public class PaymentDashboardFilterDto
{
    public long? SessionId { get; set; }
    public long? SessionGroupId { get; set; }
    public PaymentType? PaymentType { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
}

/// <summary>
/// Payment Overview Dashboard data.
/// REQ-PAY-039/040/041/042: Expected, collected, remaining revenue.
/// </summary>
public class PaymentDashboardDto
{
    /// <summary>
    /// Teacher-wide expected revenue. <c>null</c> when the caller is an assistant — an
    /// assistant's own dashboard has no teacher-wide expectation, and null (not 0) lets the
    /// client distinguish "not available to you" from a genuine zero.
    /// TODO(assistant-dashboard): a real per-assistant target/expected figure is to be
    /// designed and built end-to-end by frontend + backend.
    /// </summary>
    public decimal? ExpectedRevenue { get; set; }

    /// <summary>
    /// Collected revenue. Teacher caller → all collectors combined. Assistant caller →
    /// ONLY the money that assistant personally collected.
    /// </summary>
    public decimal CollectedRevenue { get; set; }

    /// <summary>Teacher-wide remaining revenue. <c>null</c> for an assistant caller (see <see cref="ExpectedRevenue"/>).</summary>
    public decimal? RemainingRevenue { get; set; }

    /// <summary>Per-session breakdown. <c>null</c> for an assistant caller (teacher-wide view, not their own data).</summary>
    public List<SessionRevenueBreakdownDto>? PerSessionBreakdown { get; set; } = new();

    /// <summary>Per-collector breakdown. Assistant caller → a single entry (themselves).</summary>
    public List<CollectorRevenueBreakdownDto> PerCollectorBreakdown { get; set; } = new();
}

/// <summary>
/// Per-session revenue breakdown.
/// REQ-PAY-043: Per-session granularity.
/// </summary>
public class SessionRevenueBreakdownDto
{
    public long SessionId { get; set; }
    public string SessionName { get; set; } = null!;
    public decimal Expected { get; set; }
    public decimal Collected { get; set; }
    public decimal Remaining { get; set; }
}

/// <summary>
/// Per-collector revenue breakdown.
/// REQ-PAY-041: Broken down by collecting users.
/// </summary>
public class CollectorRevenueBreakdownDto
{
    public long UserId { get; set; }
    public string? UserName { get; set; }
    public decimal Collected { get; set; }
    public int TransactionCount { get; set; }
}

/// <summary>
/// Display DTO for the "Collected by Sessions" card — one row per currently
/// active session (<c>EndDate &gt;= today</c>).
/// REQ-PAY-043: Per-session collection progress while the session is active.
/// </summary>
public class SessionCollectionSummaryDto
{
    public long SessionId { get; set; }
    public string SessionName { get; set; } = null!;

    /// <summary>e.g. "Saturday - 5:00 PM - prep3".</summary>
    public string ScheduleLabel { get; set; } = null!;
    public decimal CollectedAmount { get; set; }
    public decimal ExpectedAmount { get; set; }
    public int PaidStudentCount { get; set; }
    public int TotalStudentCount { get; set; }

    /// <summary>Rounded 0-100. <c>CollectedAmount / ExpectedAmount</c>; 0 when nothing is due yet.</summary>
    public decimal PercentCollected { get; set; }
}

// ══════════════════════════════════════════════════════════════════════════
// DEPARTURE DTOs
// ══════════════════════════════════════════════════════════════════════════

/// <summary>
/// Departure summary displayed before confirmation.
/// REQ-PAY-072: All fields shown on departure summary screen.
/// </summary>
public class DepartureSummaryDto
{
    public string StudentName { get; set; } = null!;
    public string StudentCode { get; set; } = null!;
    public string SessionName { get; set; } = null!;
    public string CurrentPeriodLabel { get; set; } = null!;
    public int TotalOccurrencesInPeriod { get; set; }
    public int AttendedOccurrences { get; set; }
    public decimal FullPeriodAmount { get; set; }
    public decimal ProRatedAmount { get; set; }
    public PaymentStatus PaymentStatusAtDeparture { get; set; }
    public DepartureOutcome DepartureOutcome { get; set; }
    public decimal FinalAmount { get; set; }
    public string OutcomeLabel { get; set; } = null!;

    // ── Anchored-month disclosure (added with the attendance-based refund correction) ──
    // The refund is always computed against ONE month: the month of the student's LATEST
    // PAID period in this session (or the teacher-local current month when they never paid).
    // These fields let the frontend show exactly which month the numbers describe.

    /// <summary>First day of the anchored month the refund was calculated against.</summary>
    public DateTime PeriodStart { get; set; }

    /// <summary>Display label of the anchored month (e.g. "July 2026").</summary>
    public string MonthLabel { get; set; } = null!;

    /// <summary>
    /// Id of the anchored <c>PaymentPeriod</c> (the latest paid month). Null when the student
    /// never paid in this session — nothing to reverse at confirm time.
    /// </summary>
    public long? PaymentPeriodId { get; set; }

    /// <summary>
    /// Cash the student actually paid for the anchored month (<c>PaymentPeriod.AmountPaid</c>).
    /// This is the refund base and the hard ceiling for a tutor override on a refund.
    /// </summary>
    public decimal PaidAmount { get; set; }
}

/// <summary>
/// Input DTO for confirming a student departure.
/// REQ-PAY-073: Confirm Departure action.
/// REQ-PAY-075: Optional tutor override.
/// </summary>
public class ConfirmDepartureDto
{
    public long TeacherId { get; set; }
    public long TeacherStudentId { get; set; }
    public long SessionId { get; set; }
    public decimal? OverrideAmount { get; set; }
    public long ConfirmedByUserId { get; set; }

    /// <summary>
    /// Optional (defaults false — existing clients unaffected). When true, the student is also
    /// soft-deleted (moved to the recycle bin) as part of the departure, not just unassigned.
    /// When false, the departure only unassigns the student and processes the refund.
    /// </summary>
    public bool DeleteStudent { get; set; } = false;
}

/// <summary>
/// Display DTO for a student departure record.
/// </summary>
public class StudentDepartureDto
{
    public long Id { get; set; }
    public string SessionName { get; set; } = null!;
    public string? StudentName { get; set; }
    public PaymentStatus PaymentStatusAtDeparture { get; set; }
    public int TotalOccurrencesInPeriod { get; set; }
    public int AttendedOccurrences { get; set; }
    public decimal FullPeriodAmount { get; set; }
    public decimal ProRatedAmount { get; set; }
    public decimal FinalAmount { get; set; }
    public bool IsTutorOverride { get; set; }
    public DepartureOutcome DepartureOutcome { get; set; }
    public DateTime DepartedAt { get; set; }
}

// ══════════════════════════════════════════════════════════════════════════
// SESSION TRANSFER DTOs
// ══════════════════════════════════════════════════════════════════════════

/// <summary>
/// Pre-transfer financial summary.
/// REQ-PAY-088: Shown before tutor confirms transfer.
/// </summary>
public class TransferSummaryDto
{
    public string StudentName { get; set; } = null!;
    public string StudentCode { get; set; } = null!;
    public string SourceSessionName { get; set; } = null!;
    public string DestinationSessionName { get; set; } = null!;
    public PaymentStatus PaymentStatusInSource { get; set; }
    public decimal OutstandingBalance { get; set; }
    public decimal CreditBalance { get; set; }
    public string DestinationPaymentType { get; set; } = null!;
    public decimal DestinationSessionAmount { get; set; }
}

/// <summary>
/// Input DTO for confirming a session transfer.
/// REQ-PAY-088: Confirm Transfer action.
/// </summary>
public class ConfirmTransferDto
{
    public long TeacherId { get; set; }
    public long TeacherStudentId { get; set; }
    public long SourceSessionId { get; set; }
    public long DestinationSessionId { get; set; }
    public long TransferredByUserId { get; set; }
}

/// <summary>
/// Display DTO for a session transfer event.
/// REQ-PAY-089: Permanently retained in history.
/// </summary>
public class SessionTransferEventDto
{
    public long Id { get; set; }
    public string SourceSessionName { get; set; } = null!;
    public string DestinationSessionName { get; set; } = null!;
    public PaymentStatus PaymentStatusAtTransfer { get; set; }
    public decimal OutstandingBalance { get; set; }
    public decimal CreditBalance { get; set; }
    public DateTime TransferredAt { get; set; }
}

// ══════════════════════════════════════════════════════════════════════════
// OFFLINE SYNC DTOs
// ══════════════════════════════════════════════════════════════════════════

/// <summary>
/// Input DTO for syncing offline-collected payment records.
/// REQ-PAY-080/081: Batch sync on reconnection.
/// </summary>
public class OfflinePaymentSyncRequestDto
{
    public long TeacherId { get; set; }
    public List<CollectPaymentDto> OfflineRecords { get; set; } = new();
}

/// <summary>
/// Result of an offline sync operation.
/// REQ-PAY-080: Shows how many records were synced.
/// REQ-PAY-082: Lists conflicts for resolution.
/// Per-record outcomes ride <see cref="EntryResults"/> (keyed by
/// ClientEntryId) so the client can transition each queued op individually —
/// mirrors attendance's SyncResultDto. The aggregate counts and
/// <see cref="Conflicts"/> stay for wire compatibility.
/// </summary>
public class PaymentSyncResultDto
{
    public int SyncedCount { get; set; }
    public int ConflictCount { get; set; }
    public int FailedCount { get; set; }
    public List<PaymentConflictDto> Conflicts { get; set; } = new();
    public List<PaymentSyncEntryResultDto> EntryResults { get; set; } = new();
}

/// <summary>
/// Outcome of one offline payment record in a sync batch.
/// </summary>
public class PaymentSyncEntryResultDto
{
    /// <summary>Echo of the record's client-generated id (may be null for
    /// legacy clients that did not send one).</summary>
    public string? ClientEntryId { get; set; }
    public bool Success { get; set; }
    public bool IsConflict { get; set; }
    public string? ErrorMessage { get; set; }

    /// <summary>True when this record was already recorded by an earlier
    /// sync (ClientEntryId dedup) — success without a new transaction.</summary>
    public bool AlreadySynced { get; set; }

    /// <summary>The existing transaction on conflict / already-synced.</summary>
    public PaymentTransactionDto? ExistingRecord { get; set; }
}

/// <summary>
/// A detected sync conflict.
/// REQ-PAY-082: Both records shown for tutor resolution.
/// </summary>
public class PaymentConflictDto
{
    public CollectPaymentDto OfflineRecord { get; set; } = null!;
    public PaymentTransactionDto ExistingRecord { get; set; } = null!;
    public string ConflictReason { get; set; } = null!;
}

// ══════════════════════════════════════════════════════════════════════════
// REPORT DTOs
// ══════════════════════════════════════════════════════════════════════════

/// <summary>
/// Input DTO for generating a payment report.
/// REQ-PAY-048: Ten report types.
/// </summary>
public class PaymentReportRequestDto
{
    public PaymentReportType ReportType { get; set; }
    public long? StudentId { get; set; }
    public long? SessionId { get; set; }
    public long? SessionGroupId { get; set; }
    public long? AssistantId { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public PaymentType? PaymentType { get; set; }
    public int? MinConsecutiveUnpaid { get; set; }
    public string? SortBy { get; set; }
    public string? SortDirection { get; set; }
}

/// <summary>
/// Standard report header.
/// REQ-PAY-049: All reports display standard header information.
/// </summary>
public class PaymentReportHeaderDto
{
    public string TutorAccountName { get; set; } = null!;
    public string ReportType { get; set; } = null!;
    public string ReportTitle { get; set; } = null!;
    public DateTime GeneratedAt { get; set; }
    public DateTime? PeriodStart { get; set; }
    public DateTime? PeriodEnd { get; set; }
    public string? ActiveFilters { get; set; }
}

// ══════════════════════════════════════════════════════════════════════════
// STUDENT/PARENT VIEW DTOs
// ══════════════════════════════════════════════════════════════════════════

/// <summary>
/// Payment data visible to students/parents.
/// Gated by TeacherConfiguration.StudentVisibilityPayment / ParentVisibilityPayment.
/// </summary>
public class StudentPaymentViewDto
{
    public string SessionName { get; set; } = null!;
    public PaymentStatus CurrentStatus { get; set; }
    public decimal AmountDue { get; set; }
    public decimal AmountPaid { get; set; }
    public decimal Outstanding { get; set; }
    public List<PaymentPeriodDto> Periods { get; set; } = new();
}
// ══════════════════════════════════════════════════════════════════════════
// BATCH COLLECTION DTOs (UI: "Mark N students as Paid")
// ══════════════════════════════════════════════════════════════════════════

/// <summary>
/// Input DTO for collecting payments from multiple students in one request.
/// SessionId and PaymentMethod are shared across all items (the collection screen
/// is session-scoped). TeacherId and CollectedByUserId are intentionally absent —
/// both are resolved from the JWT by the presentation layer, matching the single
/// collect endpoint (REQ-PAY-011 / BR-PAY-004).
/// </summary>
public class BatchCollectPaymentDto
{
    public long SessionId { get; set; }
    public PaymentCollectionMethod PaymentMethod { get; set; }

    /// <summary>Cascades to each item's same-day duplicate confirmation (REQ-PAY-020).</summary>
    public bool ConfirmAllDuplicates { get; set; } = false;

    /// <summary>Cascades to each item's already-paid confirmation (REQ-PAY-026).</summary>
    public bool ConfirmAllAlreadyPaid { get; set; } = false;

    public List<BatchCollectItemDto> Items { get; set; } = new();
}

/// <summary>A single student entry within a batch collection request.</summary>
public class BatchCollectItemDto
{
    public long TeacherStudentId { get; set; }
    public decimal Amount { get; set; }
    /// <summary>Online payment reference, for online collection methods only (REQ-PAY-008).</summary>
    public string? OnlineTransactionRef { get; set; }
}

/// <summary>
/// Aggregated result of a batch collection.
/// Mirrors the offline-sync result shape: totals plus a per-student outcome list.
/// </summary>
public class BatchCollectResultDto
{
    public int TotalRequested { get; set; }
    public int CollectedCount { get; set; }
    public int NeedsConfirmationCount { get; set; }
    public int FailedCount { get; set; }
    public List<BatchCollectItemResultDto> Results { get; set; } = new();
}

/// <summary>Per-student outcome within a batch collection.</summary>
public class BatchCollectItemResultDto
{
    public long TeacherStudentId { get; set; }
    public BatchCollectItemStatus Status { get; set; }
    /// <summary>Localized outcome message (reused from the single-collect path).</summary>
    public string? Message { get; set; }
    /// <summary>The created transaction — populated only when Status is Collected.</summary>
    public PaymentTransactionDto? Transaction { get; set; }
    /// <summary>REQ-PAY-020: same-day duplicate — requires ConfirmAllDuplicates to proceed.</summary>
    public bool IsSameDayDuplicate { get; set; }
    /// <summary>REQ-PAY-026: period already fully paid — requires ConfirmAllAlreadyPaid to proceed.</summary>
    public bool IsAlreadyPaid { get; set; }
    /// <summary>Total already collected today for this student, when a same-day duplicate is detected.</summary>
    public decimal? TodayPaidAmount { get; set; }
}
// ══════════════════════════════════════════════════════════════════════════
// BATCH EDIT DTOs  (UI: "Saved N changes" / "Submit N students" — D2)
// ══════════════════════════════════════════════════════════════════════════

/// <summary>
/// Input DTO for batch-editing payment transactions (D2).
/// Mirrors the batch-collect request shape: TeacherId and EditedByUserId are
/// intentionally ABSENT — both are resolved from the JWT by the presentation layer,
/// never trusted from the body (BR-PAY-002). Each item is applied independently via
/// EditPaymentAsync — the single source of truth for all edit business rules.
/// </summary>
public class BatchEditPaymentDto
{
    public List<BatchEditItemDto> Items { get; set; } = new();
}

/// <summary>A single transaction edit within a batch request.</summary>
public class BatchEditItemDto
{
    public long TransactionId { get; set; }
    /// <summary>New paid amount; null leaves the amount unchanged.</summary>
    public decimal? NewAmount { get; set; }
    /// <summary>New status (reversal is a status change → logged as Reversed); null leaves it unchanged.</summary>
    public PaymentStatus? NewStatus { get; set; }
    /// <summary>Reassign the transaction to a different period; null leaves it unchanged.</summary>
    public long? NewPaymentPeriodId { get; set; }
    /// <summary>Optional audit reason recorded on the PaymentEditLog (≤ EditReasonMaxLength).</summary>
    public string? EditReason { get; set; }
}

/// <summary>
/// Aggregated result of a batch edit. Mirrors BatchCollectResultDto:
/// totals plus a per-transaction outcome list.
/// (Requirement wording: TotalRequested / Successful / Failed.)
/// </summary>
public class BatchEditResultDto
{
    public int TotalRequested { get; set; }
    public int SucceededCount { get; set; }
    public int FailedCount { get; set; }
    public List<BatchEditItemResultDto> Results { get; set; } = new();
}

/// <summary>Per-transaction outcome within a batch edit.</summary>
public class BatchEditItemResultDto
{
    public long TransactionId { get; set; }
    public BatchEditItemStatus Status { get; set; }
    /// <summary>Underlying HTTP status from the reused edit path:
    /// 200 success, 400 validation, 404 not found/not owned, 500 unexpected.</summary>
    public int StatusCode { get; set; }
    /// <summary>Localized outcome message (reused from the single-edit path).</summary>
    public string? Message { get; set; }
    /// <summary>The updated transaction — populated only when Status is Succeeded.</summary>
    public PaymentTransactionDto? Transaction { get; set; }
}
// ══════════════════════════════════════════════════════════════════════════
// BATCH REVERT DTOs  (UI: "Revert (N students)" — D1)
// ══════════════════════════════════════════════════════════════════════════

/// <summary>
/// Input DTO for batch-reverting payment transactions (D1).
/// A revert is expressed as an edit that zeroes the collected amount and marks the
/// transaction Unpaid, reusing EditPaymentAsync (single source of truth). TeacherId and
/// EditedByUserId are resolved from the JWT by the presentation layer, never from the body
/// (BR-PAY-002). One Reason applies to every transaction in the batch and is recorded on
/// each PaymentEditLog.
/// </summary>
public class BatchRevertPaymentDto
{
    public List<long> TransactionIds { get; set; } = new();
    /// <summary>Audit reason recorded on every reverted transaction's PaymentEditLog (≤ EditReasonMaxLength).</summary>
    public string? Reason { get; set; }
}

/// <summary>Aggregated result of a batch revert. Mirrors BatchEditResultDto.</summary>
public class BatchRevertResultDto
{
    public int TotalRequested { get; set; }
    public int RevertedCount { get; set; }
    public int FailedCount { get; set; }
    public List<BatchRevertItemResultDto> Results { get; set; } = new();
}

/// <summary>Per-transaction outcome within a batch revert.</summary>
public class BatchRevertItemResultDto
{
    public long TransactionId { get; set; }
    /// <summary>Reuses BatchEditItemStatus — a revert has the same binary Succeeded/Failed outcome as an edit.</summary>
    public BatchEditItemStatus Status { get; set; }
    /// <summary>Underlying HTTP status from the reused edit path: 200 success, 404 not found/not owned, 500 unexpected.</summary>
    public int StatusCode { get; set; }
    /// <summary>Localized outcome message (reused from the single-edit path).</summary>
    public string? Message { get; set; }
    /// <summary>The reverted transaction (amount 0, status Unpaid) — populated only when Status is Succeeded.</summary>
    public PaymentTransactionDto? Transaction { get; set; }
}