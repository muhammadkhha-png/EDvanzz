using Edvanz.Domain.Entities;
using Edvanz.Domain.Enums;

namespace Edvanz.Domain.Interfaces;

/// <summary>
/// Extended repository interface for the Payment Module (Module 4) and Event Payment Module (Module 5).
/// Centralizes all domain-specific query methods for payment-related entities:
/// PaymentTransaction, PaymentPeriod, StudentPaymentCounter, AssistantWallet,
/// WalletResetLog, PaymentEditLog, StudentDeparture, SessionTransferEvent,
/// PaymentEvent, EventStudentObligation, EventPaymentTransaction.
///
/// ARCHITECTURAL NOTE (same rationale as IUserRepo, ITeacherStudentRepo, IAttendanceRepo):
/// All expression-based queries are encapsulated here in named methods.
/// The Application layer never builds raw predicates. If a query changes,
/// you edit ONE method here — not every service that uses it.
///
/// Inherits from IGenericRepo&lt;PaymentTransaction, long&gt; for basic CRUD on the
/// primary entity. Other entities are accessed via named methods below.
/// </summary>
public interface IPaymentRepo : IGenericRepo<PaymentTransaction, long>
{
    // ══════════════════════════════════════════════
    // PAYMENT TRANSACTION QUERIES
    // ══════════════════════════════════════════════

    /// <summary>
    /// Gets a payment transaction by ID scoped to a teacher.
    /// Returns null if not found or belongs to different teacher.
    /// </summary>
    Task<PaymentTransaction?> GetTransactionByIdAndTeacherAsync(long transactionId, long teacherId);

    /// <summary>
    /// From a candidate set of <c>SessionOccurrence</c> ids, returns the subset referenced by any
    /// <c>PaymentTransaction</c> (its per-session billing anchor). Consumed by the Attendance module
    /// before it rebuilds a session's occurrences on a recurrence edit (SES-1) so payment-anchored
    /// occurrences are preserved rather than SET NULL. Returns an empty list for an empty input.
    /// </summary>
    Task<IReadOnlyList<long>> GetReferencedSessionOccurrenceIdsAsync(IEnumerable<long> sessionOccurrenceIds);

    /// <summary>
    /// Gets a paginated list of payment transactions for a student across all sessions.
    /// REQ-PAY-052: Student payment history with period grouping.
    /// </summary>
    Task<(IReadOnlyList<PaymentTransaction> Items, int TotalCount)> GetStudentPaymentHistoryPagedAsync(
        long teacherId, long teacherStudentId,
        DateTime? startDate, DateTime? endDate,
        int page, int pageSize);

    /// <summary>
    /// Gets all transactions for a specific payment period.
    /// Used for period balance calculation and display.
    /// </summary>
    Task<IReadOnlyList<PaymentTransaction>> GetTransactionsByPeriodAsync(long paymentPeriodId);

    /// <summary>
    /// Returns the CollectedByUserId of the most recent non-deleted payment the student made for the
    /// given session — i.e. who is holding the cash. Used to attribute a departure refund back to the
    /// correct assistant wallet. Null when there is no such payment (or it was collected by the tutor).
    /// </summary>
    Task<long?> GetLatestCollectorUserIdForStudentSessionAsync(long teacherId, long teacherStudentId, long sessionId);

    /// <summary>
    /// Gets transactions by a student on a specific local date.
    /// REQ-PAY-020: Same-day duplicate detection.
    /// </summary>
    Task<IReadOnlyList<PaymentTransaction>> GetSameDayTransactionsAsync(
        long teacherId, long teacherStudentId, DateTime localDate);

    /// <summary>
    /// Finds an offline-synced transaction by its client-generated id.
    /// Exactly-once replay guard for POST api/Payment/sync.
    /// </summary>
    Task<PaymentTransaction?> GetByClientEntryIdAsync(
        long teacherId, string clientEntryId);

    /// <summary>
    /// Gets transactions collected by a specific user in a date range.
    /// REQ-PAY-013: Collector view. REQ-PAY-058: Collector performance report.
    /// </summary>
    Task<(IReadOnlyList<PaymentTransaction> Items, int TotalCount)> GetCollectorTransactionsPagedAsync(
        long teacherId, long collectedByUserId,
        DateTime? startDate, DateTime? endDate,
        int page, int pageSize);

    /// <summary>
    /// Gets all transactions for a session in a date range.
    /// REQ-PAY-053: Session payment report.
    /// </summary>
    Task<(IReadOnlyList<PaymentTransaction> Items, int TotalCount)> GetSessionTransactionsPagedAsync(
        long teacherId, long sessionId,
        DateTime? startDate, DateTime? endDate,
        int page, int pageSize);

    /// <summary>
    /// Gets all transactions across all sessions for a date range.
    /// REQ-PAY-060: Daily collection report. REQ-PAY-061: Period revenue report.
    /// </summary>
    Task<(IReadOnlyList<PaymentTransaction> Items, int TotalCount)> GetTransactionsByDateRangePagedAsync(
        long teacherId,
        DateTime startDate, DateTime endDate,
        long? sessionId, long? collectedByUserId,
        int page, int pageSize,
        string? search = null);

    /// <summary>
    /// Distribution of money collected by per-month amount in [start, end] for the teacher (optionally
    /// one collector): groups the per-period settlement slices by applied amount so a multi-month
    /// payment counts once per month. Drives the "how many paid X" summary cards.
    /// </summary>
    Task<IReadOnlyList<(decimal Amount, int Count)>> GetCollectionAmountTiersAsync(
        long teacherId, DateTime startInclusive, DateTime endInclusive, long? collectedByUserId);

    // ══════════════════════════════════════════════
    // PAYMENT PERIOD QUERIES
    // ══════════════════════════════════════════════

    /// <summary>
    /// Gets the earliest unpaid payment period for a student in a session.
    /// BR-PAY-001: Core payment assignment logic.
    /// Uses index on (TeacherId, TeacherStudentId, PaymentStatus, PeriodSequence).
    /// </summary>
    Task<PaymentPeriod?> GetEarliestUnpaidPeriodAsync(long teacherId, long teacherStudentId, long? sessionId);

    /// <summary>
    /// All unpaid periods (PaymentStatus != Paid) for the student/session whose PeriodStart is on
    /// or before <paramref name="throughMonthEnd"/>, earliest-first (by PeriodSequence). Tracked
    /// so the caller can apply a cascading payment across them. The cutoff enforces the
    /// "pay overdue up to this month, at most one month in advance" rule.
    /// </summary>
    Task<List<PaymentPeriod>> GetUnpaidPeriodsThroughAsync(
        long teacherId, long teacherStudentId, long? sessionId, DateTime throughMonthEnd);

    /// <summary>
    /// FUTURE re-priceable periods for a SESSION (a session-price change): periods of
    /// <paramref name="sessionId"/> whose PeriodStart is on/after <paramref name="fromMonthStart"/>
    /// and are still Unpaid or PartiallyPaid, EXCLUDING students who carry an individual
    /// CustomPaymentAmount (BR-PAY-003 — their price is not affected by session changes). Tracked
    /// (the caller rewrites AmountDue/PaymentStatus and saves). Ordered by student then sequence.
    /// </summary>
    Task<List<PaymentPeriod>> GetRepriceableSessionDefaultPeriodsAsync(
        long teacherId, long sessionId, DateTime fromMonthStart);

    /// <summary>
    /// FUTURE re-priceable periods for one STUDENT (a per-student price change): the student's
    /// periods whose PeriodStart is on/after <paramref name="fromMonthStart"/> and are still Unpaid
    /// or PartiallyPaid. Tracked so the caller can rewrite AmountDue/PaymentStatus and save.
    /// </summary>
    Task<List<PaymentPeriod>> GetRepriceableStudentPeriodsAsync(
        long teacherId, long teacherStudentId, DateTime fromMonthStart);

    /// <summary>
    /// Total arrears (sum of each unpaid month's remaining due) the student owes through
    /// <paramref name="throughMonthEnd"/> for the session. Server-owned "amount due" for the
    /// collect lookup and mark-paid; never includes months in advance.
    /// </summary>
    Task<decimal> GetOverdueTotalThroughAsync(
        long teacherId, long teacherStudentId, long? sessionId, DateTime throughMonthEnd);

    /// <summary>
    /// Returns the most recent period the student actually paid into (AmountPaid &gt; 0) for the
    /// session — i.e. the current month if paid, otherwise the previous paid month. Used for the
    /// departure refund when proration is DISABLED (refund the full paid amount of that single
    /// period, not a pro-rated or cumulative amount). Null if the student has paid nothing.
    /// </summary>
    Task<PaymentPeriod?> GetLatestPaidPeriodAsync(long teacherId, long teacherStudentId, long? sessionId);

    /// <summary>
    /// Gets a payment period by ID.
    /// </summary>
    Task<PaymentPeriod?> GetPaymentPeriodByIdAsync(long paymentPeriodId);

    /// <summary>
    /// Gets all payment periods for a student in a session, ordered by sequence.
    /// REQ-PAY-052: Student payment history timeline.
    /// </summary>
    Task<IReadOnlyList<PaymentPeriod>> GetPaymentPeriodsByStudentAndSessionAsync(
        long teacherId, long teacherStudentId, long? sessionId);

    /// <summary>
    /// Gets all payment periods for a student across all sessions.
    /// REQ-PAY-092: Unified timeline across all sessions.
    /// </summary>
    Task<IReadOnlyList<PaymentPeriod>> GetAllPaymentPeriodsByStudentAsync(
        long teacherId, long teacherStudentId);

    /// <summary>
    /// Gets ALL of a student's payment periods (across sessions) with their non-deleted
    /// <c>PaymentTransactions</c> eager-loaded, ordered by period start. Backs the
    /// student/parent payment tracking screen, where each paid period needs its settlement
    /// date (the latest transaction's local collection date).
    /// </summary>
    Task<IReadOnlyList<PaymentPeriod>> GetStudentPeriodsWithTransactionsAsync(
        long teacherId, long teacherStudentId);

    /// <summary>
    /// Gets a student's payment periods filtered to an optional date window (by period start),
    /// ordered by session then sequence. Backs the payment-history startDate/endDate filter.
    /// </summary>
    Task<IReadOnlyList<PaymentPeriod>> GetPaymentPeriodsByStudentInRangeAsync(
        long teacherId, long teacherStudentId, DateTime? startDate, DateTime? endDate);

    /// <summary>
    /// Gets unpaid payment periods for a specific session.
    /// REQ-PAY-028: Unpaid badge count per session.
    /// </summary>
    Task<int> CountUnpaidStudentsBySessionAsync(long teacherId, long sessionId);

    /// <summary>
    /// Count of active students currently assigned to a session (SessionId not null).
    /// Used as the dashboard "total students" — the roster a teacher expects to collect from.
    /// </summary>
    Task<int> CountAssignedStudentsAsync(long teacherId);

    /// <summary>
    /// Per-session collection for the "collected by session" tracking card, scoped BY PERIOD to
    /// [startInclusive, endExclusive): <c>CashCollected</c> = sum of <c>PaymentPeriod.AmountPaid</c>
    /// on the session's periods whose <c>PeriodStart</c> falls in the window; <c>PaidStudents</c> =
    /// distinct students with <c>AmountPaid &gt; 0</c> on their period(s) for that session in the
    /// window. This answers "how much of THIS session's THIS-month bill was collected, and how many
    /// students paid their this-month bill" — so a payment toward a previous month reflects under
    /// that previous month's card, NOT the current month's (reconciles with the roster's paid/unpaid
    /// split). Deliberately DIFFERENT from the transaction-date/cash-based dashboard + assistant
    /// "collected this month" figures (<see cref="GetCashCollectedInRangeAsync"/> /
    /// <see cref="GetDashboardPerCollectorAsync"/>), which are unchanged. Excludes soft-deleted /
    /// purge-orphaned students. §7.4.
    /// </summary>
    Task<IReadOnlyList<(long SessionId, decimal CashCollected, int PaidStudents)>>
        GetSessionMonthCollectionAsync(long teacherId, DateTime startInclusive, DateTime endExclusive);

    /// <summary>
    /// Per-session count of students currently assigned to it — the per-session card's
    /// "total students" denominator.
    /// </summary>
    Task<IReadOnlyList<(long SessionId, int TotalStudents)>>
        GetAssignedStudentCountsPerSessionAsync(long teacherId);

    /// <summary>
    /// Actual cash collected (by transaction date) in [startInclusive, endExclusive), optionally
    /// for one session — the dashboard's "collected this month". Net of refunds (soft-deleted /
    /// edited transactions), and independent of which month each payment settles.
    /// </summary>
    Task<decimal> GetCashCollectedInRangeAsync(
        long teacherId, long? sessionId, DateTime startInclusive, DateTime endExclusive);

    /// <summary>
    /// Count of DISTINCT students who made at least one (non-deleted) payment in
    /// [startInclusive, endExclusive), optionally scoped to one session. Backs the date-filtered
    /// collections summary ("how many students paid" over the range).
    /// </summary>
    Task<int> CountDistinctPayingStudentsInRangeAsync(
        long teacherId, long? sessionId, DateTime startInclusive, DateTime endExclusive);

    /// <summary>
    /// All collections a collector took in [startInclusive, endExclusive), unpaged —
    /// merged with refunds into a single chronological month log.
    /// </summary>
    Task<IReadOnlyList<PaymentTransaction>> GetCollectorTransactionsInRangeAsync(
        long teacherId, long collectorUserId, DateTime startInclusive, DateTime endExclusive);

    /// <summary>
    /// Refunds taken back from a collector in [startInclusive, endExclusive), from the payment
    /// edit-log trail (delete/reversal = full amount; amount reduction = the drop).
    /// </summary>
    Task<IReadOnlyList<CollectorRefundRow>> GetCollectorRefundsInRangeAsync(
        long teacherId, long collectorUserId, DateTime startInclusive, DateTime endExclusive);

    /// <summary>
    /// Student-departure refunds (RefundDue) confirmed within [startInclusive, endExclusive), sourced
    /// directly from <c>StudentDeparture</c> (authoritative <c>FinalAmount</c>). Teacher-wide, or
    /// narrowed to one collector. Drives the negative refund lines on the collections ledger and the
    /// per-collector refund subtraction.
    /// </summary>
    Task<IReadOnlyList<DepartureRefundRow>> GetDepartureRefundsByDateRangeAsync(
        long teacherId, DateTime startInclusive, DateTime endExclusive, long? collectedByUserId = null);

    /// <summary>
    /// Gets the maximum period sequence for a student in a session.
    /// Used when generating new periods to determine the next sequence number.
    /// </summary>
    Task<int> GetMaxPeriodSequenceAsync(long teacherId, long teacherStudentId, long sessionId);

    /// <summary>
    /// Gets student payment status counts for the Paid / Pro-rated / Unpaid
    /// overview card. Classification is per student, based on the same
    /// "earliest outstanding period" concept as <see cref="GetEarliestUnpaidPeriodAsync"/>
    /// (BR-PAY-001): no outstanding period → Paid; earliest outstanding period
    /// is prorated (REQ-PAY-021/022 first-period proration) → Pro-rated;
    /// otherwise → Unpaid. Students with no payment periods at all are not counted.
    /// </summary>
    Task<(int Paid, int ProRated, int Unpaid)> GetStudentPaymentStatusCountsAsync(
        long teacherId, DateTime selectedMonthEnd);

    /// <summary>
    /// Count of assigned students with a period in [monthStart, monthEnd] that is
    /// <c>PartiallyPaid</c> (0 &lt; AmountPaid &lt; AmountDue) — the "part paid this month" bucket,
    /// matching the <c>status="partial"</c> classification in
    /// <see cref="GetStudentsByPaymentStatusPagedAsync"/>. Backs the collections summary.
    /// </summary>
    Task<int> CountPartiallyPaidStudentsInMonthAsync(
        long teacherId, DateTime monthStart, DateTime monthEnd);

    /// <summary>
    /// Adds a new payment period.
    /// </summary>
    Task AddPaymentPeriodAsync(PaymentPeriod period);

    /// <summary>
    /// Adds multiple payment periods in bulk.
    /// Used when generating all periods for a newly assigned student.
    /// </summary>
    Task AddPaymentPeriodsRangeAsync(IEnumerable<PaymentPeriod> periods);

    /// <summary>
    /// Updates an existing payment period.
    /// </summary>
    Task UpdatePaymentPeriodAsync(PaymentPeriod period);

    // ══════════════════════════════════════════════
    // PAYMENT TRANSACTION ALLOCATION LEDGER (PAY-1)
    // ══════════════════════════════════════════════

    /// <summary>
    /// Adds per-period settlement rows for a transaction (one per period a cascade collection
    /// cleared). Allocations may reference a not-yet-persisted transaction via the navigation
    /// property; EF fixes up the FK on save.
    /// </summary>
    Task AddPaymentTransactionAllocationsRangeAsync(IEnumerable<PaymentTransactionAllocation> allocations);

    /// <summary>
    /// Returns the settlement ledger for a transaction, TRACKED and with each allocation's
    /// <see cref="PaymentPeriod"/> eagerly loaded (also tracked), so a caller can reverse each
    /// period's amount in place. Ordered by the period's start date (oldest first).
    /// </summary>
    Task<IReadOnlyList<PaymentTransactionAllocation>> GetAllocationsByTransactionAsync(long transactionId);

    /// <summary>
    /// Removes fully-consumed allocation rows (a slice whose amount has been entirely reversed).
    /// </summary>
    Task RemovePaymentTransactionAllocationsAsync(IEnumerable<PaymentTransactionAllocation> allocations);

    // ══════════════════════════════════════════════
    // STUDENT PAYMENT COUNTER QUERIES
    // ══════════════════════════════════════════════

    /// <summary>
    /// Gets the payment counter for a student under a teacher.
    /// O(1) lookup via unique index.
    /// </summary>
    Task<StudentPaymentCounter?> GetPaymentCounterAsync(long teacherId, long teacherStudentId);

    /// <summary>
    /// Adds a new payment counter.
    /// </summary>
    Task AddPaymentCounterAsync(StudentPaymentCounter counter);

    /// <summary>
    /// Updates an existing payment counter.
    /// Uses RowVersion for optimistic concurrency.
    /// </summary>
    Task UpdatePaymentCounterAsync(StudentPaymentCounter counter);

    /// <summary>
    /// Unpaid Students Overview rows (REQ-PAY-031/032/033), derived from <c>PaymentPeriods</c>
    /// and judged ONLY through <paramref name="throughMonthEnd"/> (<c>PeriodStart &lt;= cutoff</c>)
    /// per CLAUDE.md §7.4 — periods are pre-generated to the session end, so a counter-derived
    /// read counts months that are not yet owed.
    ///
    /// Scoped by the PERIOD's session/group, not the student's current assignment: a student who
    /// transferred out still owes the old session's arrears. Only ACTIVE (non-recycled) students
    /// are returned. <paramref name="paymentType"/> maps onto <c>PaymentPeriod.PeriodType</c>.
    /// <paramref name="minConsecutiveUnpaid"/> filters on the unpaid-period count, which — because
    /// collection cascades oldest-first — is also the consecutive count (BR-PAY-006).
    /// </summary>
    Task<(IReadOnlyList<UnpaidStudentRow> Items, int TotalCount)> GetUnpaidStudentsPagedAsync(
        long teacherId,
        long? sessionId, long? sessionGroupId,
        PaymentType? paymentType,
        int? minConsecutiveUnpaid,
        string? search,
        DateTime throughMonthEnd,
        int page, int pageSize);

    /// <summary>
    /// Gets the sum of total outstanding across all students for a teacher.
    /// REQ-PAY-NFR-008: Live total outstanding in Unpaid Students View.
    /// </summary>
    Task<decimal> GetTotalOutstandingAmountAsync(long teacherId, long? sessionId);

    // ══════════════════════════════════════════════
    // SCREEN QUERIES (api/v1 — frontend payment.json)
    // ══════════════════════════════════════════════

    /// <summary>
    /// CollectPayment screen student list: students filtered by assignment
    /// (all | assigned | unassigned) + name/code search, paginated, each with
    /// payment status/amount/unpaid-months from their counter. Also returns the
    /// per-tab counts (all/assigned/unassigned) for the current search.
    /// </summary>
    Task<(IReadOnlyList<CollectStudentRow> Items, int TotalCount, int CountAll, int CountAssigned, int CountUnassigned)>
        GetCollectStudentsPagedAsync(
            long teacherId, string filter, string? search, int page, int pageSize,
            DateTime unpaidThroughMonthEnd);

    /// <summary>
    /// PaymentTracking "students by status" list: students whose current status
    /// (by the earliest-outstanding-period rule, same as
    /// <see cref="GetStudentPaymentStatusCountsAsync"/>) matches
    /// <paramref name="status"/> (paid | prorated | unpaid | partial), paginated, each with that
    /// month's paid/due amounts and their counter's outstanding/unpaid-months. Also
    /// returns the group's month-scoped collected/expected totals and total outstanding.
    ///
    /// <para><paramref name="status"/> is OPTIONAL: when null, ALL of the (assigned) scope's
    /// students are returned, each carrying its OWN computed status on
    /// <see cref="StudentByStatusRow.Status"/> (a per-session mixed-status roster in one call).
    /// When supplied, behaviour is unchanged (rows are filtered to that status).</para>
    ///
    /// <para><paramref name="sessionId"/> (optional) restricts the assigned-scope to a single
    /// session; <paramref name="search"/> (optional) filters by student name OR studentCode
    /// (case-insensitive). Both intersect with — never replace — the assigned-only gating.</para>
    /// </summary>
    Task<(IReadOnlyList<StudentByStatusRow> Items, int TotalCount, decimal GroupCollected, decimal GroupExpected, decimal GroupUnpaid, decimal GroupExpectedRate)>
        GetStudentsByPaymentStatusPagedAsync(
            long teacherId, string? status,
            DateTime monthStart, DateTime monthEnd,
            int page, int pageSize,
            long? sessionId = null, string? search = null);

    /// <summary>
    /// SessionPaymentCollectedByYear matrix: students who have at least one payment period in
    /// the given year, paginated by student name, each with their per-calendar-month cells
    /// (aggregated across sessions). Returns the total student count for paging.
    /// </summary>
    Task<(IReadOnlyList<YearlyStudentRow> Items, int TotalCount)>
        GetYearlyCollectionsPagedAsync(
            long teacherId, DateTime yearStart, DateTime yearEnd, int page, int pageSize);

    /// <summary>
    /// CollectPaymentSession lookup: resolves a single student for collection by QR/barcode,
    /// then student code, then name (first match by name), returning the amount they should pay
    /// (custom amount → earliest-unpaid-period remaining → session amount) and paid/unpaid state.
    /// Returns null when nothing matches or no lookup key is supplied.
    /// </summary>
    Task<CollectLookupRow?> ResolveCollectLookupAsync(
        long teacherId, string? qr, string? code, string? name, DateTime throughMonthEnd);

    // ══════════════════════════════════════════════
    // ASSISTANT WALLET QUERIES
    // ══════════════════════════════════════════════

    /// <summary>
    /// Gets the wallet for an assistant under a teacher.
    /// </summary>
    Task<AssistantWallet?> GetAssistantWalletAsync(long teacherId, long assistantId);

    /// <summary>
    /// Gets the wallet by assistant user ID.
    /// Used during payment collection to find the collector's wallet.
    /// </summary>
    Task<AssistantWallet?> GetAssistantWalletByUserIdAsync(long teacherId, long assistantUserId);

    /// <summary>Finds a center-assistant's wallet under a teacher by CenterAssistantId (its unique key).</summary>
    Task<AssistantWallet?> GetAssistantWalletByCenterAssistantIdAsync(long teacherId, long centerAssistantId);

    /// <summary>
    /// Gets all assistant wallets for a teacher.
    /// REQ-PAY-035: View current wallet balance of each assistant.
    /// </summary>
    Task<IReadOnlyList<AssistantWallet>> GetAllAssistantWalletsAsync(long teacherId);

    /// <summary>
    /// Adds a new assistant wallet.
    /// </summary>
    Task AddAssistantWalletAsync(AssistantWallet wallet);

    /// <summary>
    /// Updates an existing assistant wallet.
    /// Uses RowVersion for optimistic concurrency.
    /// </summary>
    Task UpdateAssistantWalletAsync(AssistantWallet wallet);

    // ══════════════════════════════════════════════
    // WALLET RESET LOG QUERIES
    // ══════════════════════════════════════════════

    /// <summary>
    /// Adds a new wallet reset log entry.
    /// REQ-PAY-037: Permanent ledger event — never deleted.
    /// </summary>
    Task AddWalletResetLogAsync(WalletResetLog log);

    /// <summary>
    /// Gets all wallet reset logs for an assistant.
    /// REQ-PAY-059: Assistant Wallet History Report.
    /// </summary>
    Task<IReadOnlyList<WalletResetLog>> GetWalletResetLogsAsync(long teacherId, long assistantId);

    /// <summary>Reset/withdrawal ledger entries for a center-assistant's wallet (keyed by CenterAssistantId).</summary>
    Task<IReadOnlyList<WalletResetLog>> GetWalletResetLogsForCenterAssistantAsync(long teacherId, long centerAssistantId);

    /// <summary>
    /// Cash withdrawals/hand-overs taken from a collector's OWN wallet within [start, endExclusive),
    /// resolved from the collector's User id (→ their teacher-scoped Assistant record). Empty for a
    /// collector without a wallet (e.g. the tutor). Drives the negative withdrawal lines on a
    /// collector-scoped collections ledger.
    /// </summary>
    Task<IReadOnlyList<WalletResetLog>> GetWalletResetLogsForCollectorInRangeAsync(
        long teacherId, long collectorUserId, DateTime startInclusive, DateTime endExclusive);

    // ══════════════════════════════════════════════
    // PAYMENT EDIT LOG QUERIES
    // ══════════════════════════════════════════════

    /// <summary>
    /// Adds a new payment edit log entry.
    /// </summary>
    Task AddPaymentEditLogAsync(PaymentEditLog log);

    /// <summary>
    /// Gets all edit logs for a payment transaction.
    /// </summary>
    Task<IReadOnlyList<PaymentEditLog>> GetPaymentEditLogsAsync(long paymentTransactionId);

    // ══════════════════════════════════════════════
    // FORGIVENESS QUERIES (waive outstanding balance — reversible)
    // ══════════════════════════════════════════════

    /// <summary>Adds a forgiveness header row (per-period allocations are added separately).</summary>
    Task AddPaymentForgivenessAsync(PaymentForgiveness forgiveness);

    /// <summary>Adds per-period waiver slices for a forgiveness (nav-linked so EF fixes the FK on save).</summary>
    Task AddPaymentForgivenessAllocationsRangeAsync(IEnumerable<PaymentForgivenessAllocation> allocations);

    /// <summary>
    /// Gets one forgiveness by id (tenant-scoped), TRACKED, with its allocations and each allocation's
    /// <c>PaymentPeriod</c> eagerly loaded (also tracked), so a reversal can restore the exact per-period
    /// <c>ForgivenAmount</c>. Null when not found / not this teacher's.
    /// </summary>
    Task<PaymentForgiveness?> GetForgivenessByIdAndTeacherAsync(long teacherId, long forgivenessId);

    /// <summary>All forgivenesses for a student (any status), newest first — the history surface.</summary>
    Task<IReadOnlyList<PaymentForgiveness>> GetForgivenessesByStudentAsync(long teacherId, long teacherStudentId);

    /// <summary>
    /// The student's per-month rate: their <c>CustomPaymentAmount</c> override, else the current
    /// session's <c>SessionAmount</c>, else 0. Backs whole-month-multiple note validation
    /// (collect/edit) and the collect-lookup <c>monthlyAmount</c>.
    /// </summary>
    Task<decimal> GetStudentMonthlyRateAsync(long teacherId, long teacherStudentId);

    // ══════════════════════════════════════════════
    // DEPARTURE QUERIES
    // ══════════════════════════════════════════════

    /// <summary>
    /// Adds a new student departure record.
    /// REQ-PAY-073: Departure event recorded permanently.
    /// </summary>
    Task AddStudentDepartureAsync(StudentDeparture departure);

    /// <summary>
    /// Gets departure records for a student.
    /// </summary>
    Task<IReadOnlyList<StudentDeparture>> GetStudentDeparturesAsync(long teacherId, long teacherStudentId);

    /// <summary>Teacher-wide paged list of departed students (search by name/code), newest first.</summary>
    Task<(IReadOnlyList<DepartureListRow> Items, int TotalCount)> GetDeparturesPagedAsync(
        long teacherId, string? search, int page, int pageSize);

    /// <summary>
    /// Counts student departures confirmed in [startInclusive, endExclusive), split by outcome
    /// (RefundDue / AmountOwed; the remainder is NoObligation). Backs the collections summary's
    /// "departed" metric over the true date range.
    /// </summary>
    Task<(int Total, int RefundDue, int AmountOwed)> CountDeparturesInRangeAsync(
        long teacherId, DateTime startInclusive, DateTime endExclusive);

    // ══════════════════════════════════════════════
    // SESSION TRANSFER QUERIES
    // ══════════════════════════════════════════════

    /// <summary>
    /// Adds a new session transfer event.
    /// REQ-PAY-089: Permanently retained, never deleted.
    /// </summary>
    Task AddSessionTransferEventAsync(SessionTransferEvent transferEvent);

    /// <summary>
    /// Gets transfer events for a student.
    /// REQ-PAY-092: Unified timeline display.
    /// </summary>
    Task<IReadOnlyList<SessionTransferEvent>> GetStudentTransferEventsAsync(
        long teacherId, long teacherStudentId);

    // ══════════════════════════════════════════════
    // DASHBOARD AGGREGATES (REQ-PAY-039 through 044)
    // ══════════════════════════════════════════════

    /// <summary>
    /// Gets the expected, collected, and remaining revenue for a teacher.
    /// REQ-PAY-040/041/042: Dashboard views.
    /// Filterable by session, session group, payment type, date range.
    /// </summary>
    Task<(decimal Expected, decimal Collected, decimal Remaining)> GetDashboardAggregatesAsync(
        long teacherId,
        long? sessionId, long? sessionGroupId,
        PaymentType? paymentType,
        DateTime? startDate, DateTime? endDate);

    /// <summary>
    /// Gets per-session breakdown of expected/collected/remaining.
    /// REQ-PAY-043: Filterable by session for drill-down.
    /// </summary>
    Task<IReadOnlyList<(long SessionId, string SessionName, long? SessionGroupId, string? SessionGroupName, decimal Expected, decimal Collected, decimal Remaining)>>
        GetDashboardPerSessionAsync(
            long teacherId,
            long? sessionGroupId,
            PaymentType? paymentType,
            DateTime? startDate, DateTime? endDate);

    /// <summary>
    /// Gets the collection summary for every currently active session
    /// (<c>EndDate &gt;= today</c>): collected/expected amounts and
    /// paid/total student counts, alongside the schedule fields needed to
    /// build the display label.
    /// REQ-PAY-043: "Collected by Sessions" card — per-session collection
    /// progress while the session is active.
    /// </summary>
    Task<IReadOnlyList<ActiveSessionCollectionSummaryRow>> GetActiveSessionsCollectionSummaryAsync(
        long teacherId);

    /// <summary>
    /// Gets per-collector breakdown of collected amounts.
    /// REQ-PAY-041: Collected revenue broken down by collecting users.
    /// </summary>
    Task<IReadOnlyList<(long UserId, string? UserName, decimal Collected, int TransactionCount)>>
        GetDashboardPerCollectorAsync(
            long teacherId,
            DateTime? startDate, DateTime? endDate);

    // ══════════════════════════════════════════════
    // EVENT QUERIES (Module 5)
    // ══════════════════════════════════════════════

    /// <summary>
    /// Adds a new payment event.
    /// REQ-EVT-001: Create one-time payment events.
    /// </summary>
    Task AddPaymentEventAsync(PaymentEvent paymentEvent);

    /// <summary>
    /// Gets a payment event by ID scoped to a teacher.
    /// </summary>
    Task<PaymentEvent?> GetPaymentEventByIdAndTeacherAsync(long eventId, long teacherId);

    /// <summary>
    /// Gets all active (non-deleted) events for a teacher.
    /// REQ-EVT-014: Event list display.
    /// </summary>
    Task<(IReadOnlyList<PaymentEvent> Items, int TotalCount)> GetPaymentEventsPagedAsync(
        long teacherId, int page, int pageSize);

    /// <summary>
    /// Updates an existing payment event.
    /// </summary>
    Task UpdatePaymentEventAsync(PaymentEvent paymentEvent);

    /// <summary>
    /// Adds event student obligations in bulk.
    /// BR-EVT-001: Created at event creation time for target scope.
    /// </summary>
    Task AddEventObligationsRangeAsync(IEnumerable<EventStudentObligation> obligations);

    /// <summary>
    /// Gets a specific event obligation for a student.
    /// </summary>
    Task<EventStudentObligation?> GetEventObligationAsync(long eventId, long teacherStudentId);

    /// <summary>
    /// Gets all obligations for an event with paging, paid/unpaid separation, and search.
    /// REQ-EVT-015: Paid and unpaid student lists.
    /// REQ-EVT-016: Searchable by student name or student code.
    /// </summary>
    Task<(IReadOnlyList<EventStudentObligation> Items, int TotalCount)> GetEventObligationsPagedAsync(
        long eventId, long teacherId,
        PaymentStatus? statusFilter,
        string? search,
        int page, int pageSize);

    /// <summary>
    /// Updates an event student obligation (e.g., after payment collection).
    /// </summary>
    Task UpdateEventObligationAsync(EventStudentObligation obligation);

    /// <summary>
    /// Deletes an event student obligation.
    /// REQ-EVT-021: Remove student from event if they haven't paid.
    /// </summary>
    Task DeleteEventObligationAsync(EventStudentObligation obligation);

    /// <summary>
    /// Gets filtered and paginated events for a teacher.
    /// REQ-EVT-017/018: Searchable by name, filterable by scope and completion.
    /// </summary>
    Task<(IReadOnlyList<PaymentEvent> Items, int TotalCount)> GetPaymentEventsFilteredPagedAsync(
        long teacherId,
        string? searchName,
        EventTargetScopeType? scopeTypeFilter,
        string? completionStatus,
        int page, int pageSize);

    /// <summary>
    /// Adds an event payment transaction.
    /// REQ-EVT-009: Collect event payment.
    /// </summary>
    Task AddEventPaymentTransactionAsync(EventPaymentTransaction transaction);

    /// <summary>
    /// Gets event payment transactions for reporting.
    /// REQ-EVT-023: Single Event Payment Report.
    /// </summary>
    Task<IReadOnlyList<EventPaymentTransaction>> GetEventPaymentTransactionsAsync(
        long eventId, long teacherId);

    // ══════════════════════════════════════════════
    // INTEGRATION HOOKS (called before delete/purge)
    // ══════════════════════════════════════════════

    // ══════════════════════════════════════════════
    // TARGET SCOPE RESOLUTION (Module 5 event creation)
    // ══════════════════════════════════════════════

    /// <summary>
    /// Gets all active (non-deleted) student IDs assigned to a specific session.
    /// REQ-EVT-004: Session-scoped event target resolution.
    /// </summary>
    Task<List<long>> GetStudentIdsBySessionAsync(long teacherId, long sessionId);

    /// <summary>
    /// Gets all active (non-deleted) student IDs across all sessions in a session group.
    /// REQ-EVT-005: Session group-scoped event target resolution.
    /// </summary>
    Task<List<long>> GetStudentIdsByGroupAsync(long teacherId, long sessionGroupId);

    /// <summary>
    /// Gets all active (non-deleted) student IDs under a teacher's account.
    /// REQ-EVT-006: All students scope event target resolution.
    /// </summary>
    Task<List<long>> GetAllStudentIdsAsync(long teacherId);

    // ══════════════════════════════════════════════
    // INTEGRATION HOOKS (called before delete/purge)
    // ══════════════════════════════════════════════

    /// <summary>
    /// Nullifies SessionId on all payment transactions and periods for a session
    /// before the session is hard-deleted. Uses ExecuteUpdateAsync for bulk operation.
    /// Same pattern as AttendanceRepo.NullifySessionIdOnRecordsForSessionAsync.
    /// </summary>
    Task NullifySessionIdOnPaymentRecordsAsync(long sessionId);

    /// <summary>
    /// Severs a student's payment records during permanent purge: nullifies TeacherStudentId on the
    /// audit records that must survive (transactions, departures, transfers, event obligations —
    /// denormalized name/code fields preserved), DELETEs the student's PaymentPeriods (billing
    /// obligations are meaningless once the student is gone and would otherwise orphan into
    /// dashboard aggregates), and deletes the StudentPaymentCounter.
    /// Same pattern as AttendanceRepo.NullifyStudentReferencesOnRecordsAsync.
    /// </summary>
    Task NullifyStudentReferencesOnPaymentRecordsAsync(long teacherStudentId);

    /// <summary>
    /// Recalculates the consecutive unpaid count from payment period records.
    /// BR-PAY-006: Based on sequential unpaid periods with no paid period in between.
    /// Called after payment edits/deletions where simple increment/decrement won't work.
    /// </summary>
    Task<int> RecalculateConsecutiveUnpaidAsync(long teacherId, long teacherStudentId);

    /// <summary>
    /// One-time cleanup support: every CURRENTLY-assigned student (across all teachers) that still
    /// carries an UNPAID/PARTIAL PaymentPeriod under a session OTHER than their current one — arrears
    /// stranded in an old session by the pre-carry-over reassign. Soft-deleted students are excluded by
    /// the global query filter; students with no current session are excluded by the SessionId != null
    /// predicate. See <see cref="StrandedStudentRow"/>.
    /// </summary>
    Task<IReadOnlyList<StrandedStudentRow>> GetStudentsWithStrandedUnpaidPeriodsAsync();

    /// <summary>Counts the (non-deleted) payment events owned by a teacher (free-tier quota).</summary>
    Task<int> CountEventsByTeacherAsync(long teacherId);
    Task<DateTime?> GetLastWalletResetAtAsync(long teacherId, long assistantId);
    // ══════════════════════════════════════════════
    // ATTENDANCE SCREEN PAYMENT ENRICHMENT (ShowPaymentInfoOnAttendanceScreen)
    // ══════════════════════════════════════════════

    /// <summary>
    /// Batched, per-student payment/debt snapshot for the Take/Edit Attendance list's optional
    /// payment enrichment — bounded to the caller's page of <paramref name="teacherStudentIds"/>,
    /// never the full roster. Reuses the exact same arrears rules as the Unpaid Students Overview
    /// (<see cref="UnpaidStudentRow"/>) per CLAUDE.md §7.4.
    /// </summary>
    Task<Dictionary<long, AttendanceScreenPaymentInfoRow>> GetPaymentInfoForAttendanceBatchAsync(
        long teacherId, IReadOnlyCollection<long> teacherStudentIds,
        DateTime throughMonthEnd, DateTime lastMonthStart, DateTime lastMonthEnd);

}