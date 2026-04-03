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
    public long TeacherId { get; set; }
    public long EventId { get; set; }
    public string? EventName { get; set; }
    public decimal? EventAmount { get; set; }
    public string? Notes { get; set; }
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
    /// Filter by completion: "FullyCollected", "PartiallyCollected", "NotStarted", or null for all.
    /// REQ-EVT-018: Completion status filter.
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
    public long TeacherId { get; set; }
    public long EventId { get; set; }
    public long TeacherStudentId { get; set; }
    public decimal CustomAmount { get; set; }
}