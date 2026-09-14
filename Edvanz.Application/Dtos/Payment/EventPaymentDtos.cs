using Edvanz.Domain.Enums;

namespace Edvanz.Application.Dtos.Payment;

// ══════════════════════════════════════════════════════════════════════════
// EVENT CREATION/MANAGEMENT DTOs
// ══════════════════════════════════════════════════════════════════════════

/// <summary>
/// Represents a single scope entry within an event's target definition.
/// REQ-EVT-004: Multiple scopes can be combined within a single event.
/// </summary>
public class EventScopeEntry
{
    public EventTargetScopeType ScopeType { get; set; }
    /// <summary>
    /// IDs for this scope: student IDs for IndividualStudents, session ID for Session,
    /// group ID for SessionGroup, empty for AllStudents.
    /// </summary>
    public List<long> ScopeIds { get; set; } = new();
}

/// <summary>
/// Input DTO for creating a new payment event.
/// REQ-EVT-002: All mandatory fields.
/// REQ-EVT-004: Supports combining multiple scopes with automatic deduplication.
/// </summary>
public class CreateEventDto
{
    /// <summary>SERVER-SET from the JWT — recorded on each scope row (<c>AssignedByUserId</c>) so
    /// the targeting decision is attributable. Never read from the request body.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public long ActingUserId { get; set; }

    public long TeacherId { get; set; }
    /// <summary>
    /// REQ-EVT-002: Mandatory descriptive name (e.g., "Book Purchase — Term 1").
    /// </summary>
    public string EventName { get; set; } = null!;
    /// <summary>
    /// REQ-EVT-002: Mandatory fixed amount.
    /// </summary>
    public decimal EventAmount { get; set; }
    /// <summary>
    /// REQ-EVT-004: One or more scope entries that can be combined.
    /// Example: [{ ScopeType: Session, ScopeIds: [5] }, { ScopeType: IndividualStudents, ScopeIds: [101, 102] }]
    /// The system deduplicates students appearing in multiple scopes.
    /// </summary>
    public List<EventScopeEntry> TargetScopes { get; set; } = new();
    /// <summary>
    /// REQ-EVT-002: Mandatory event date.
    /// </summary>
    public DateTime EventDate { get; set; }
    /// <summary>
    /// REQ-EVT-002: Optional free-text notes.
    /// </summary>
    public string? Notes { get; set; }
}

/// <summary>
/// Input DTO for updating an event (e.g., adding/removing students).
/// REQ-EVT-020: Add students to event after creation.
/// </summary>
public class UpdateEventDto
{
    /// <summary>
    /// SERVER-SET from the JWT role — never read from the request body. True when the caller is an
    /// assistant, which makes student REMOVAL a 403 (BR-EVT-003 is tutor-only; creating, editing and
    /// collecting stay delegable).
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public bool IsAssistantCaller { get; set; }

    /// <summary>SERVER-SET from the JWT — who performed the change, for the audit rows.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public long ActingUserId { get; set; }

    public long TeacherId { get; set; }
    public long EventId { get; set; }
    public string? EventName { get; set; }
    public decimal? EventAmount { get; set; }
    public string? Notes { get; set; }

    /// <summary>
    /// The item's date. OMITTED (null) = unchanged.
    /// </summary>
    /// <remarks>
    /// A <c>DateTime?</c> and not a <c>DateOnly?</c> because this is a REQUEST field and deployed
    /// clients send full ISO strings, which <c>DateOnly</c> cannot read (§11b). Only the date part
    /// is persisted — the column is <c>date</c>.
    /// </remarks>
    public DateTime? EventDate { get; set; }

    /// <summary>
    /// Whether students who join a targeted class later are added automatically.
    /// OMITTED (null) = unchanged.
    /// </summary>
    /// <remarks>
    /// Nullable on purpose. An old client that does not know this field must not flip it by
    /// omission — the same rule that stopped an unrelated date edit from wiping an exam's
    /// description (BUG-20).
    /// </remarks>
    public bool? AutoIncludeNewStudents { get; set; }

    /// <summary>
    /// Whether this item's unpaid dues appear on the attendance collect sheet. OMITTED (null) =
    /// unchanged. The per-item half of the pair; the teacher-level switch can disable all of them.
    /// </summary>
    public bool? CollectDuringAttendance { get; set; }
    /// <summary>
    /// Student IDs to add to the event.
    /// REQ-EVT-020: Explicitly add students after creation.
    /// </summary>
    public List<long>? StudentIdsToAdd { get; set; }
    /// <summary>
    /// Student IDs to remove from the event.
    /// BR-EVT-003: Only tutor may remove students from scope.
    /// </summary>
    public List<long>? StudentIdsToRemove { get; set; }
}

/// <summary>
/// Display DTO for a payment event.
/// REQ-EVT-014: Event tracking summary.
/// </summary>
public class EventDto
{
    /// <summary>
    /// Students the caller asked to remove who could NOT be removed because they had already paid.
    /// Empty on a clean update.
    ///
    /// <para>Exists because the old path skipped them silently and still answered "updated
    /// successfully" — the tutor saw a success toast and an unchanged roster with no explanation.</para>
    /// </summary>
    public List<string> BlockedRemovals { get; set; } = new();

    /// <summary>Students marked "not taking it" — excluded from <c>TotalStudents</c> and from
    /// expected revenue. Additive; 0 on any response that does not derive totals.</summary>
    public int ExemptStudents { get; set; }

    /// <summary>Whether students who join the targeted classes later are added automatically.</summary>
    public bool AutoIncludeNewStudents { get; set; }

    /// <summary>Whether this item's unpaid dues appear on the attendance collect sheet (subject to
    /// the teacher-level switch).</summary>
    public bool CollectDuringAttendance { get; set; }

    /// <summary>Closed = no further collection; refunds and history still work. The escape hatch
    /// for an item that cannot be deleted because money was collected against it.</summary>
    public bool IsClosed { get; set; }

    public DateTime? ClosedAt { get; set; }

    public long Id { get; set; }
    public string EventName { get; set; } = null!;
    public decimal EventAmount { get; set; }
    public EventTargetScopeType TargetScopeType { get; set; }
    public DateTime EventDate { get; set; }
    public DateTime CreateAt { get; set; }
    public string? Notes { get; set; }
    public int TotalStudents { get; set; }
    public int PaidStudents { get; set; }
    public int PartiallyPaidStudents { get; set; }
    public int UnpaidStudents { get; set; }
    public decimal TotalExpectedRevenue { get; set; }
    public decimal TotalCollectedRevenue { get; set; }
    public decimal RemainingRevenue { get; set; }

    /// <summary>Who this item targets, in a form a list row can render in one line. Never null;
    /// an item that predates the scope table reports its legacy <c>TargetScopeType</c>.</summary>
    public ExtrasScopeSummaryDto ScopeSummary { get; set; } = new();
}

/// <summary>
/// The audience of a "Books &amp; fees" item, summarised for display.
///
/// <para>Built from <c>PaymentEventScopes</c>, so it can name the actual classes and groups. An
/// item with no scope rows is either individually targeted — those students are recorded by their
/// obligations, not by a scope row — or predates the table, and reports its legacy
/// <c>TargetScopeType</c> instead.</para>
/// </summary>
public class ExtrasScopeSummaryDto
{
    /// <summary>
    /// "sessions" | "groups" | "students" | "all" | "mixed". A STRING, not the
    /// <c>EventTargetScopeType</c> enum, because "mixed" is not a scope type: an item may target
    /// two classes and a group at once, which that enum cannot express.
    /// </summary>
    public string Type { get; set; } = "students";

    /// <summary>Up to three target names, for the row's one-line summary. Empty for
    /// <c>all</c> and <c>students</c>, which have no named targets.</summary>
    public List<string> Labels { get; set; } = new();

    /// <summary>How many named targets are NOT in <see cref="Labels"/>, so a row can read
    /// "Class A, Class B +3" without the client guessing at the remainder.</summary>
    public int ExtraCount { get; set; }
}

/// <summary>
/// Detailed event tracking view with student lists.
/// REQ-EVT-014/015: Full tracking view with paid/unpaid sections.
/// </summary>
public class EventTrackingDto
{
    public EventDto Event { get; set; } = null!;
    public List<EventStudentObligationDto> PaidStudents { get; set; } = new();
    public List<EventStudentObligationDto> UnpaidStudents { get; set; } = new();
}

// ══════════════════════════════════════════════════════════════════════════
// EVENT STUDENT OBLIGATION DTOs
// ══════════════════════════════════════════════════════════════════════════

/// <summary>
/// Display DTO for a student's event obligation.
/// REQ-EVT-015: Per-student payment status and amounts.
/// </summary>
public class EventStudentObligationDto
{
    public long Id { get; set; }
    public long? TeacherStudentId { get; set; }
    public string? StudentName { get; set; }
    public string? StudentCode { get; set; }
    public decimal AmountDue { get; set; }
    public decimal AmountPaid { get; set; }
    public decimal Outstanding { get; set; }
    public PaymentStatus PaymentStatus { get; set; }
    public DateTime? LastPaymentDate { get; set; }
    public string? CollectingUserName { get; set; }
    /// <summary>
    /// REQ-EVT-007: Whether this student has a custom amount override.
    /// </summary>
    public bool HasCustomAmount { get; set; }
}

// ══════════════════════════════════════════════════════════════════════════
// EVENT PAYMENT COLLECTION DTOs
// ══════════════════════════════════════════════════════════════════════════

/// <summary>
/// Input DTO for collecting an event payment.
/// REQ-EVT-009: Same collection methods as regular payments.
/// </summary>
public class CollectEventPaymentDto
{
    /// <summary>
    /// The collector's free-text note for this payment — how a partial or unusual amount is
    /// explained. Mirrors <c>CollectPaymentDto.CollectionNote</c>; the fee module learned that a
    /// collector taking an odd amount needs somewhere to say why.
    /// </summary>
    public string? CollectionNote { get; set; }

    public long TeacherId { get; set; }
    public long EventId { get; set; }
    public long TeacherStudentId { get; set; }
    public decimal Amount { get; set; }
    public PaymentCollectionMethod PaymentMethod { get; set; }
    public long? CollectedByUserId { get; set; }
    /// <summary>
    /// REQ-EVT-012: Confirmation for already-paid student.
    /// </summary>
    public bool AlreadyPaidConfirmed { get; set; } = false;
    public string? OnlineTransactionRef { get; set; }
}

/// <summary>
/// Result DTO for event payment collection.
/// </summary>
public class EventPaymentResultDto
{
    public EventPaymentTransactionDto? Transaction { get; set; }
    /// <summary>
    /// REQ-EVT-012: Whether student has already fully paid.
    /// </summary>
    public bool IsAlreadyPaid { get; set; } = false;
    public decimal? PreviouslyPaidAmount { get; set; }
}

/// <summary>
/// Display DTO for an event payment transaction.
/// REQ-EVT-013: Shows collecting user and payment details.
/// </summary>
public class EventPaymentTransactionDto
{
    public long Id { get; set; }
    public string EventName { get; set; } = null!;
    public string? StudentName { get; set; }
    public string? StudentCode { get; set; }
    public decimal AmountPaid { get; set; }
    public PaymentCollectionMethod PaymentMethod { get; set; }
    public long? CollectedByUserId { get; set; }
    public string? CollectedByUserName { get; set; }
    public DateTime CollectedAt { get; set; }
    public bool IsOnlinePayment { get; set; }

    /// <summary>The collector's note for this payment, when they left one.</summary>
    public string? CollectionNote { get; set; }

    /// <summary>
    /// True when this payment's amount has been corrected at least once. Pair with
    /// <see cref="OriginalAmount"/> to read "was X, now Y" rather than presenting a corrected
    /// figure as if it were the amount the collector actually took.
    /// </summary>
    public bool IsEdited { get; set; }

    /// <summary>
    /// What was originally collected, before the first correction; null when never corrected.
    /// DERIVED from the audit trail, not stored on the payment.
    /// </summary>
    public decimal? OriginalAmount { get; set; }
}

// ══════════════════════════════════════════════════════════════════════════
// EVENT REPORT DTOs
// ══════════════════════════════════════════════════════════════════════════

/// <summary>
/// Input DTO for generating an event report.
/// REQ-EVT-023/024: Event report types.
/// </summary>
public class EventReportRequestDto
{
    public EventReportType ReportType { get; set; }
    public long? EventId { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
}

/// <summary>
/// Single event payment report data.
/// REQ-EVT-023: Itemized paid/unpaid student lists.
/// </summary>
public class SingleEventReportDto
{
    public EventDto Event { get; set; } = null!;
    public List<EventStudentObligationDto> PaidStudents { get; set; } = new();
    public List<EventStudentObligationDto> UnpaidStudents { get; set; } = new();
}

/// <summary>
/// All events summary row.
/// REQ-EVT-024: Per-event aggregated summary.
/// </summary>
public class EventSummaryRowDto
{
    public long EventId { get; set; }
    public string EventName { get; set; } = null!;
    public DateTime EventDate { get; set; }
    public int TotalStudents { get; set; }
    public decimal TotalExpected { get; set; }
    public decimal TotalCollected { get; set; }
    public decimal Outstanding { get; set; }
    public decimal CompletionPercentage { get; set; }
}

/// <summary>
/// All events summary report.
/// REQ-EVT-024: Aggregated data across all events.
/// </summary>
public class AllEventsSummaryReportDto
{
    public List<EventSummaryRowDto> Events { get; set; } = new();
    public decimal GrandTotalExpected { get; set; }
    public decimal GrandTotalCollected { get; set; }
    public decimal GrandTotalOutstanding { get; set; }
}


// ══════════════════════════════════════════════════════════════════════════
// EVENT LIST FILTER DTO (REQ-EVT-017/018)
// ══════════════════════════════════════════════════════════════════════════

/// <summary>
/// Filter criteria for the Event Overview list.
/// REQ-EVT-017: Searchable by event name.
/// REQ-EVT-018: Filterable by scope type and completion status.
/// </summary>
public class EventListFilterDto
{
    public string? SearchName { get; set; }
    public EventTargetScopeType? ScopeTypeFilter { get; set; }
    /// <summary>
    /// Filter by completion: "Open", "FullyCollected", "PartiallyCollected", "NotStarted", or null
    /// for all. REQ-EVT-018: Completion status filter.
    /// <para>
    /// All four are evaluated against the OBLIGATION ROWS, never the cached revenue columns, so a
    /// chip and the list it opens can never disagree. "Open" is the complement of
    /// "FullyCollected" — any live, non-exempt student who still owes something — and it exists as
    /// a server value on purpose: a client narrowing only the loaded page would hide every
    /// unsettled item past page 1.
    /// </para>
    /// </summary>
    public string? CompletionStatus { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

/// <summary>
/// DTO for setting a custom amount on a specific student within an event.
/// REQ-EVT-007: Override default event amount for individual student.
/// </summary>
public class SetEventStudentCustomAmountDto
{
    /// <summary>SERVER-SET from the JWT — who set the price, for the audit row and the UI's
    /// "set by hand by X" line. Never read from the request body.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public long ActingUserId { get; set; }

    public long TeacherId { get; set; }
    public long EventId { get; set; }
    public long TeacherStudentId { get; set; }
    public decimal CustomAmount { get; set; }
}

/// <summary>Body for refunding a collected "Books &amp; fees" payment.</summary>
public class RefundEventPaymentDto
{
    /// <summary>
    /// How much to hand back. OMITTED or null = the whole payment (the common case: wrong student,
    /// or the student returned the item). A partial amount may not exceed what was taken.
    /// </summary>
    public decimal? Amount { get; set; }

    /// <summary>Why — stored on the audit row and shown in the payment's history.</summary>
    public string? Reason { get; set; }
}

/// <summary>Body for correcting the amount of a collected "Books &amp; fees" payment.</summary>
public class EditEventPaymentDto
{
    /// <summary>The corrected amount. Must be greater than zero; refund instead to zero it out.</summary>
    public decimal NewAmount { get; set; }

    /// <summary>Why — stored on the audit row.</summary>
    public string? Reason { get; set; }
}

/// <summary>Body for closing or reopening a "Books &amp; fees" item.</summary>
public class SetExtrasItemClosedDto
{
    /// <summary>
    /// True = closed (no further collection). False = reopened.
    /// <para>
    /// Explicit rather than a toggle route: a retried request must land on the state the caller
    /// intended, and a toggle would flip it back.
    /// </para>
    /// </summary>
    public bool Closed { get; set; }
}

/// <summary>Body for marking a student as not taking (or taking again) an item.</summary>
public class SetEventStudentExemptDto
{
    /// <summary>True = not taking it; false = expected to pay again.</summary>
    public bool Exempt { get; set; }

    /// <summary>Optional reason, surfaced on the student's row.</summary>
    public string? Reason { get; set; }
}

// ══════════════════════════════════════════════════════════════════════════════
// TRACKING v2 — the shapes the rebuilt screens read (2026-09-14)
//
// The legacy EventTrackingDto returned paid[] + unpaid[] as FULL ARRAYS while
// accepting page/pageSize, and discarded both total counts — so it had no
// pagination metadata at all, its "unpaid" list silently contained partially-paid
// and overpaid students, and it could answer nothing "by session" or "by group".
// It is kept alive and delegating for the published openapi/Postman contract.
// ══════════════════════════════════════════════════════════════════════════════

/// <summary>One item's header: the five populations and the money, over the WHOLE item.</summary>
public class ExtrasItemSummaryDto
{
    public int TotalStudents { get; set; }
    public int PaidStudents { get; set; }
    public int PartiallyPaidStudents { get; set; }
    public int UnpaidStudents { get; set; }

    /// <summary>Students marked "not taking it". Excluded from <see cref="TotalStudents"/> and from
    /// <see cref="ExpectedAmount"/> — no money is expected from them.</summary>
    public int ExemptStudents { get; set; }

    public decimal ExpectedAmount { get; set; }
    public decimal CollectedAmount { get; set; }
    public decimal RemainingAmount { get; set; }

    /// <summary>What the exempt students would have owed. Shown so "expected" is explainable.</summary>
    public decimal ExemptAmount { get; set; }

    public decimal CompletionPercent { get; set; }
}

/// <summary>
/// One row of the by-session or by-group breakdown. Folded from the SAME grouped query as the
/// header, so a breakdown can never drift from the total it sits under.
/// </summary>
public class ExtrasBreakdownRowDto
{
    /// <summary>Session id or group id, as a string (this module's wire convention for entity ids).
    /// Null means "no class" — students with no current session, which must appear as their own row
    /// or the breakdown stops summing to the header.</summary>
    public string? Id { get; set; }

    public string? Name { get; set; }
    public int TotalStudents { get; set; }
    public int PaidStudents { get; set; }
    public int PartiallyPaidStudents { get; set; }
    public int UnpaidStudents { get; set; }
    public int ExemptStudents { get; set; }
    public decimal ExpectedAmount { get; set; }
    public decimal CollectedAmount { get; set; }
    public decimal RemainingAmount { get; set; }
}

/// <summary>Who collected for this item. Activity-driven; force-scoped for an assistant caller.</summary>
public class ExtrasCollectorRowDto
{
    public string UserId { get; set; } = string.Empty;
    public string? Name { get; set; }

    /// <summary>"Teacher" for the account owner, otherwise "Assistant" — resolved by identity, not
    /// by an active-assistant allow-list (a removed assistant's collections are still hers).</summary>
    public string Role { get; set; } = "Assistant";

    public decimal CollectedAmount { get; set; }
    public int TransactionCount { get; set; }

    /// <summary>DISTINCT students, so two partials from one student count once.</summary>
    public int StudentCount { get; set; }
}

/// <summary>Drives the "N new students in these classes · add them?" banner.</summary>
public class ExtrasNewJoinersDto
{
    /// <summary>Students currently in a targeted class who have no obligation on this item.
    /// Always 0 for a legacy item created before scope rows existed.</summary>
    public int Count { get; set; }

    /// <summary>When true the banner is unnecessary — joiners are added automatically.</summary>
    public bool AutoIncludeEnabled { get; set; }
}

/// <summary>The item tracking screen's header payload — breakdowns only, no student rows.</summary>
public class ExtrasItemTrackingResponse
{
    public EventDto Item { get; set; } = new();
    public ExtrasItemSummaryDto Summary { get; set; } = new();
    public List<ExtrasBreakdownRowDto> BySession { get; set; } = new();
    public List<ExtrasBreakdownRowDto> ByGroup { get; set; } = new();
    public List<ExtrasCollectorRowDto> ByCollector { get; set; } = new();
    public ExtrasNewJoinersDto NewJoiners { get; set; } = new();
}

/// <summary>One student on an item's roster.</summary>
public class ExtrasObligationRowDto
{
    public string ObligationId { get; set; } = string.Empty;
    public string? TeacherStudentId { get; set; }
    public string? StudentName { get; set; }
    public string? StudentCode { get; set; }

    public string? SessionId { get; set; }
    public string? SessionName { get; set; }
    public string? SessionGroupId { get; set; }
    public string? SessionGroupName { get; set; }

    public decimal AmountDue { get; set; }
    public decimal AmountPaid { get; set; }
    public decimal Outstanding { get; set; }

    /// <summary>Serialized as a string by the global JsonStringEnumConverter.
    /// ORTHOGONAL to <see cref="IsExempt"/>: an exempt obligation is <c>Unpaid</c> AND exempt.</summary>
    public PaymentStatus PaymentStatus { get; set; }

    public bool IsExempt { get; set; }
    public DateTime? ExemptedAt { get; set; }
    public string? ExemptedByName { get; set; }
    public string? ExemptReason { get; set; }

    /// <summary>A human set this student's price. A stored flag, not
    /// <c>AmountDue != item.EventAmount</c> — that comparison breaks the moment the item's own
    /// amount changes, because a paid obligation then legitimately differs.</summary>
    public bool IsCustomAmount { get; set; }
    public string? CustomAmountSetByName { get; set; }
    public DateTime? CustomAmountSetAt { get; set; }

    public DateTime? LastPaymentAt { get; set; }
    public string? LastCollectedByName { get; set; }
    public int TransactionsCount { get; set; }
}

/// <summary>
/// An item's paged roster plus its four chip counts.
///
/// <para>The counts are measured on the SEARCHED set but BEFORE the chip filter, so selecting a
/// chip never renumbers the chips — and they share one bucket definition with the list, so a chip
/// can never claim a number the list it opens disagrees with.</para>
/// </summary>
public class ExtrasStudentsPageDto : PaginatedResponse<List<ExtrasObligationRowDto>>
{
    public int PaidCount { get; set; }
    public int PartiallyPaidCount { get; set; }
    public int UnpaidCount { get; set; }
    public int ExemptCount { get; set; }

    /// <summary>Everyone matching the search, before any chip filter. The sum of the four above.</summary>
    public int SearchedCount { get; set; }
}

/// <summary>One unpaid item for one student — the attendance sheet's line item.</summary>
public class ExtrasStudentDebtDto
{
    public string ObligationId { get; set; } = string.Empty;
    public string ItemId { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    public decimal AmountDue { get; set; }
    public decimal AmountPaid { get; set; }
    public decimal Outstanding { get; set; }

    /// <summary>A calendar day, so the wire says it is a day (§11b). Responses only.</summary>
    public DateOnly ItemDate { get; set; }
}

/// <summary>Every student's unpaid dues, student-keyed — the offline hydration feed.</summary>
public class ExtrasDebtsResponse
{
    /// <summary>Keyed by <c>teacherStudentId</c> as a string, so the app can look a scanned student
    /// up without scanning a list.</summary>
    public Dictionary<string, List<ExtrasStudentDebtDto>> ByStudent { get; set; } = new();

    /// <summary>False when the teacher has turned books &amp; fees off for the attendance screen.
    /// The client keys off THIS, never off an empty dictionary — "off" and "nobody owes anything"
    /// must not look the same.</summary>
    public bool ShowExtrasInfo { get; set; } = true;
}
