using Edvanz.Application.Dtos;
using Edvanz.Application.Dtos.Payment;

namespace Edvanz.Application.ServiceContract;

/// <summary>
/// Defines the contract for Event-Based Payment Module operations (Module 5).
/// Covers: event creation, event payment collection, event tracking,
/// event management, and event reporting.
///
/// All methods return Result&lt;T&gt; for consistent error handling.
/// All methods are async per system architecture requirements.
///
/// TRANSACTION SAFETY:
/// All write operations use the ownsTransaction pattern.
/// </summary>
public interface IEventPaymentService
{
    // ══════════════════════════════════════════════
    // EVENT CREATION (REQ-EVT-001 through 008)
    // ══════════════════════════════════════════════

    /// <summary>
    /// Creates a new one-time payment event.
    /// REQ-EVT-001/002: Independent of regular session payment cycle.
    /// BR-EVT-001: Obligations created for students in scope at creation time.
    /// </summary>
    Task<Result<EventDto>> CreateEventAsync(CreateEventDto dto);

    // ══════════════════════════════════════════════
    // EVENT LISTING & TRACKING (REQ-EVT-014/015)
    // ══════════════════════════════════════════════

    /// <summary>
    /// Gets a paginated list of events for a teacher.
    /// REQ-EVT-014: Event list with tracking summary.
    /// </summary>
    Task<Result<PaginatedResponse<List<EventDto>>>> GetEventsAsync(
        long teacherId, EventListFilterDto filter);

    /// <summary>
    /// Gets the detailed tracking view for a specific event.
    /// REQ-EVT-014/015: Full tracking view with paid/unpaid student lists.
    /// REQ-EVT-016: Searchable by student name or student code.
    /// </summary>
    Task<Result<EventTrackingDto>> GetEventTrackingAsync(
        long teacherId, long eventId, string? search, int page, int pageSize);

    // ══════════════════════════════════════════════
    // EVENT MANAGEMENT (REQ-EVT-020 through 022)
    // ══════════════════════════════════════════════

    /// <summary>
    /// Updates an event (add/remove students, edit name/amount).
    /// REQ-EVT-020: Add students to event after creation.
    /// BR-EVT-003: Only tutor may remove students or delete events.
    /// </summary>
    Task<Result<EventDto>> UpdateEventAsync(UpdateEventDto dto);

    /// <summary>
    /// Sets a custom amount for a specific student within an event.
    /// REQ-EVT-007: Override default event amount for individual student.
    /// </summary>
    Task<Result<bool>> SetEventStudentCustomAmountAsync(SetEventStudentCustomAmountDto dto);

    /// <summary>
    /// Deletes an event (soft delete, no recovery).
    /// REQ-EVT-022: Confirmation required. Collected payments retained.
    /// BR-EVT-005: Deletion is irreversible.
    /// </summary>
    /// <remarks>
    /// REFUSED with 409 <c>ExtrasItemHasPaymentsCannotDelete</c> once any money has been collected
    /// and not refunded — deleting would hide the item that a surviving transaction row is
    /// attributed to, so its money would appear in the ledger with nothing to explain it. Closing
    /// (<see cref="SetEventClosedAsync"/>) is the remedy the message names.
    /// </remarks>
    Task<Result<bool>> DeleteEventAsync(long teacherId, long eventId, long actingUserId);

    /// <summary>
    /// Closes or reopens an item. Closed = no further collection; refunds, history and tracking
    /// all keep working, and auto-include stops adding new joiners.
    /// </summary>
    /// <remarks>
    /// This is the escape hatch for an item that cannot be deleted because money was collected
    /// against it. Reversible on purpose — a tutor who closed one a class too early must be able
    /// to reopen it rather than recreate it and lose the history.
    /// </remarks>
    Task<Result<EventDto>> SetEventClosedAsync(
        long teacherId, long eventId, bool closed, long actingUserId);

    // ══════════════════════════════════════════════
    // EVENT PAYMENT COLLECTION (REQ-EVT-009 through 013)
    // ══════════════════════════════════════════════

    /// <summary>
    /// Collects a payment for an event from a student.
    /// REQ-EVT-009: Same collection methods as regular payments.
    /// REQ-EVT-011: Supports partial payments.
    /// REQ-EVT-012: Already-paid duplicate warning.
    /// REQ-EVT-013: Included in collector's wallet balance.
    /// </summary>
    Task<Result<EventPaymentResultDto>> CollectEventPaymentAsync(CollectEventPaymentDto dto);

    // ══════════════════════════════════════════════
    // EVENT REPORTING (REQ-EVT-023 through 026)
    // ══════════════════════════════════════════════

    /// <summary>
    /// Generates an event payment report.
    /// REQ-EVT-023: Single Event Payment Report.
    /// REQ-EVT-024: All Events Summary Report.
    /// </summary>
    Task<Result<object>> GenerateEventReportAsync(
        long teacherId, EventReportRequestDto request);

    /// <summary>
    /// Exports an event report as PDF or Excel.
    /// REQ-EVT-025: Exportable in PDF or Excel format.
    /// </summary>
    Task<Result<byte[]>> ExportEventReportAsync(
        long teacherId, EventReportRequestDto request, string format);

    // ══════════════════════════════════════════════
    // MONEY CORRECTIONS (added 2026-09-14) — TUTOR-ONLY at the API gate
    // ══════════════════════════════════════════════

    /// <summary>
    /// Refunds a collected "Books &amp; fees" payment, in full or in part. The money leaves the
    /// ORIGINAL collector's wallet (a correction of their figure), reset-aware so cash already
    /// handed over does not drive their balance negative, and the audit row it writes IS the
    /// negative line the collections ledger shows.
    ///
    /// <para>Full refund SOFT-deletes the payment; a partial one reduces its amount. The obligation
    /// and the item's collected total are then RECOMPUTED from the surviving payments.</para>
    ///
    /// <para>TUTOR-ONLY (<c>roleOnly ["Teacher","SuperAdmin"]</c>), matching
    /// <c>DELETE api/payment/transactions/{id}</c> and BR-PAY-002: this moves money out.</para>
    /// </summary>
    /// <summary>
    /// One student's recorded payments on one "Books &amp; fees" item, newest first — the list the
    /// tutor picks from before refunding or correcting a collection.
    /// </summary>
    /// <remarks>
    /// Already-refunded payments are ABSENT (the transaction's query filter), so a payment can
    /// never be offered for refunding twice. Gated on <c>View</c> (also satisfied by
    /// <c>CollectPayment</c>) — reading who paid what is not itself a money action; the refund and
    /// the correction below are tutor-only.
    /// </remarks>
    Task<Result<List<EventPaymentTransactionDto>>> GetEventStudentPaymentsAsync(
        long teacherId, long eventId, long teacherStudentId);

    Task<Result<bool>> RefundEventPaymentAsync(
        long teacherId, long transactionId, decimal? amount, long actingUserId, string? reason);

    /// <summary>
    /// Corrects the amount of a collected "Books &amp; fees" payment. Audited, wallet-adjusted
    /// (downward only is treated as a reversal and is reset-aware), and the obligation and item
    /// totals are recomputed. TUTOR-ONLY for the same reason as the refund.
    /// </summary>
    Task<Result<bool>> EditEventPaymentAsync(
        long teacherId, long transactionId, decimal newAmount, long actingUserId, string? reason);

    /// <summary>
    /// Marks a student as not taking (or taking again) a "Books &amp; fees" item.
    ///
    /// <para>BLOCKED with 409 when the student has already paid — refund first. That is what keeps
    /// "Σ AmountPaid == Σ non-deleted payments" true in every state, and it answers rather than
    /// skipping silently, which is what the old student-removal path did.</para>
    ///
    /// <para>Exemption is an ADMINISTRATIVE statement, not a money movement, so it sits under the
    /// module's <c>Edit</c> permission rather than being tutor-only.</para>
    /// </summary>
    Task<Result<bool>> SetEventStudentExemptAsync(
        long teacherId, long eventId, long teacherStudentId,
        bool exempt, long actingUserId, string? reason);

    // ══════════════════════════════════════════════
    // AUTO-INCLUDE — per-item "add students who join these classes later"
    // ══════════════════════════════════════════════

    /// <summary>
    /// Gives the students just assigned to a session an obligation on every OPEN, auto-include
    /// "Books &amp; fees" item whose targeting covers that session (directly, through its group, or
    /// via an <c>AllStudents</c> rule).
    ///
    /// <para>Call ONCE PER ASSIGN CALL, after the per-student loop, and INSIDE its transaction —
    /// never from the per-student hook. Bulk-assigning 300 students with 5 live items would
    /// otherwise mean 300 scope queries and 1,500 inserts under one set of locks.</para>
    ///
    /// <para>Idempotent: only the missing obligations are inserted.</para>
    /// </summary>
    Task MaterializeAutoIncludeForSessionAsync(
        long teacherId, long sessionId, IReadOnlyCollection<long> teacherStudentIds);

    /// <summary>
    /// The <c>AllStudents</c> counterpart, for students who have just been CREATED and have no
    /// session yet — they never fire an assignment hook, so this is the only path by which an
    /// account-wide item reaches them.
    ///
    /// <para>Takes a COLLECTION for the same reason as its sibling: a bulk import can create
    /// hundreds of session-less students at once, and one query per student is the very cost this
    /// design exists to avoid.</para>
    /// </summary>
    Task MaterializeAutoIncludeForNewStudentsAsync(
        long teacherId, IReadOnlyCollection<long> teacherStudentIds);

    // ══════════════════════════════════════════════
    // TRACKING v2 — what the rebuilt screens read
    // ══════════════════════════════════════════════

    /// <summary>
    /// One item's tracking header: the five populations, the money, and the by-session /
    /// by-group / by-collector breakdowns. NO student rows — those are paged separately, because
    /// the legacy endpoint returned them as full arrays while accepting page/pageSize and then
    /// discarded both total counts.
    ///
    /// <para>The header and both breakdowns are folded from ONE grouped query, so a breakdown can
    /// never drift from the total it sits under. Sessions come from the student's CURRENT
    /// assignment, which is both what a tutor means by "the 7 PM class" and the column the audience
    /// resolves through (§7.8).</para>
    ///
    /// <para><paramref name="scopeToCollectorUserId"/> force-scopes the by-collector breakdown for
    /// an assistant caller. The paid/unpaid ROSTER stays teacher-wide on purpose — an assistant must
    /// see who still owes in order to collect it.</para>
    /// </summary>
    Task<Result<ExtrasItemTrackingResponse>> GetExtrasItemTrackingAsync(
        long teacherId, long itemId, long? scopeToCollectorUserId);

    /// <summary>
    /// One item's student roster, paged, with the four chip counts.
    ///
    /// <para><paramref name="status"/> accepts <c>paid</c> / <c>partiallyPaid</c> / <c>unpaid</c> /
    /// <c>exempt</c>; anything else (including <c>all</c> and null) means no chip filter — an
    /// unrecognised value must not 422 a list screen.</para>
    ///
    /// <para>"unpaid" means <c>!IsExempt &amp;&amp; PaymentStatus == Unpaid</c>. The old filter was
    /// BINARY (<c>Paid</c> vs everything else), so its unpaid list silently contained
    /// partially-paid and overpaid students.</para>
    /// </summary>
    Task<Result<ExtrasStudentsPageDto>> GetExtrasItemStudentsAsync(
        long teacherId, long itemId, string? status,
        long? sessionId, long? sessionGroupId, long? collectedByUserId,
        string? search, int page, int pageSize);

    /// <summary>
    /// Every student's unpaid "Books &amp; fees" dues, student-keyed — the attendance sheet's data
    /// and the offline hydration feed.
    ///
    /// <para>Returns <c>ShowExtrasInfo = false</c> with an empty map when the teacher has turned the
    /// behaviour off, so "off" and "nobody owes anything" are distinguishable on the wire. Pass
    /// <paramref name="teacherStudentIds"/> to scope it to one roster; omit for the whole account.</para>
    /// </summary>
    Task<Result<ExtrasDebtsResponse>> GetExtrasDebtsAsync(
        long teacherId, IReadOnlyCollection<long>? teacherStudentIds);

    /// <summary>
    /// Adds every student now in one of the item's targeted classes who has no obligation yet —
    /// the action behind the "N new students · add them?" banner. Returns how many were added.
    ///
    /// <para>Shares ONE resolver with the banner's count, so the number offered and the number
    /// added cannot disagree. Exists as its own endpoint because the auto-include materializer
    /// fires on ASSIGNMENT, and an item whose <c>AutoIncludeNewStudents</c> is off has, by
    /// definition, no assignment-time path to them.</para>
    ///
    /// <para>409 on a CLOSED item: closing exists precisely to stop further obligation.</para>
    /// </summary>
    Task<Result<int>> AddNewJoinersAsync(long teacherId, long itemId, long actingUserId);
}