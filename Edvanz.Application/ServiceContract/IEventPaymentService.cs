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
    /// </summary>
    Task<Result<EventTrackingDto>> GetEventTrackingAsync(
        long teacherId, long eventId, int page, int pageSize);

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
    Task<Result<bool>> DeleteEventAsync(long teacherId, long eventId);

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
}