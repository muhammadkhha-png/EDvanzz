using Edvanz.Application.Dtos.Payment;
using Edvanz.Application.ServiceContract;
using Microsoft.AspNetCore.Mvc;

namespace Edvanz.API.Controllers;

/// <summary>
/// API controller for the Event-Based Payment Module (Module 5).
/// Manages one-time payment events: creation, collection, tracking,
/// management (update/delete), and reporting.
/// All endpoints are teacher-scoped via the teacherId route parameter.
///
/// All endpoint documentation follows the existing project pattern:
/// WHAT IT DOES → TABLES READ/WRITTEN → SAMPLE REQUEST.
/// </summary>
public class EventPaymentController : ApiBaseController
{
    private readonly IEventPaymentService _eventService;

    public EventPaymentController(IEventPaymentService eventService)
    {
        _eventService = eventService;
    }

    // ══════════════════════════════════════════════════════════════════════════
    // ENDPOINT 1: CREATE EVENT
    // ══════════════════════════════════════════════════════════════════════════
    //
    // WHAT IT DOES:
    //   Creates a new one-time payment event with target scope resolution.
    //   REQ-EVT-001/002: Event name, Amount, Target Scope, Date are mandatory.
    //   BR-EVT-001: Obligations created for students in scope at creation time.
    //
    // TABLES WRITTEN: PaymentEvents, EventStudentObligations
    // TABLES READ: TeacherStudents, Sessions, SessionGroups
    //
    // SAMPLE: POST /api/eventpayment/123/events
    //   { "eventName": "Book Purchase — Term 1", "eventAmount": 50.00,
    //     "targetScopeType": "Session", "targetScopeIds": [789],
    //     "eventDate": "2025-01-15" }
    //
    // ══════════════════════════════════════════════════════════════════════════
    [HttpPost("{teacherId:long}/events")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateEvent(
        [FromRoute] long teacherId,
        [FromBody] CreateEventDto dto)
    {
        dto.TeacherId = teacherId;
        var result = await _eventService.CreateEventAsync(dto);
        return ToResponse(result);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // ENDPOINT 2: LIST EVENTS
    // ══════════════════════════════════════════════════════════════════════════
    //
    // WHAT IT DOES:
    //   Returns paginated list of active (non-deleted) events for the teacher.
    //   REQ-EVT-014: Event list with tracking summary.
    //
    // TABLES READ: PaymentEvents
    //
    // SAMPLE: GET /api/eventpayment/123/events?page=1&pageSize=20
    //
    // ══════════════════════════════════════════════════════════════════════════
    [HttpGet("{teacherId:long}/events")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetEvents(
        [FromRoute] long teacherId,
        [FromQuery] EventListFilterDto filter)
    {
        var result = await _eventService.GetEventsAsync(teacherId, filter);
        return ToResponse(result);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // ENDPOINT 3: EVENT TRACKING DETAIL
    // ══════════════════════════════════════════════════════════════════════════
    //
    // WHAT IT DOES:
    //   Returns detailed tracking view for a specific event with paid/unpaid lists.
    //   REQ-EVT-014/015: Full tracking with paid student list and unpaid student list.
    //
    // TABLES READ: PaymentEvents, EventStudentObligations
    //
    // SAMPLE: GET /api/eventpayment/123/events/456/tracking?page=1&pageSize=20
    //
    // ══════════════════════════════════════════════════════════════════════════
    [HttpGet("{teacherId:long}/events/{eventId:long}/tracking")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetEventTracking(
        [FromRoute] long teacherId,
        [FromRoute] long eventId,
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var result = await _eventService.GetEventTrackingAsync(teacherId, eventId, search, page, pageSize);
        return ToResponse(result);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // ENDPOINT 4: UPDATE EVENT
    // ══════════════════════════════════════════════════════════════════════════
    //
    // WHAT IT DOES:
    //   Updates an event: name, amount, and add/remove students.
    //   REQ-EVT-020: Add students to event after creation.
    //   BR-EVT-003: Only tutor may remove students or modify events.
    //
    // TABLES WRITTEN: PaymentEvents, EventStudentObligations
    // TABLES READ: PaymentEvents, TeacherStudents
    //
    // SAMPLE: PUT /api/eventpayment/123/events/456
    //   { "studentIdsToAdd": [101, 102] }
    //
    // ══════════════════════════════════════════════════════════════════════════
    [HttpPut("{teacherId:long}/events/{eventId:long}")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateEvent(
        [FromRoute] long teacherId,
        [FromRoute] long eventId,
        [FromBody] UpdateEventDto dto)
    {
        dto.TeacherId = teacherId;
        dto.EventId = eventId;
        var result = await _eventService.UpdateEventAsync(dto);
        return ToResponse(result);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // ENDPOINT 5: DELETE EVENT
    // ══════════════════════════════════════════════════════════════════════════
    //
    // WHAT IT DOES:
    //   Soft-deletes an event. Collected payments are retained.
    //   REQ-EVT-022: Confirmation required. Deletion is irreversible.
    //
    // TABLES WRITTEN: PaymentEvents
    //
    // SAMPLE: DELETE /api/eventpayment/123/events/456
    //
    // ══════════════════════════════════════════════════════════════════════════
    [HttpDelete("{teacherId:long}/events/{eventId:long}")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteEvent(
        [FromRoute] long teacherId,
        [FromRoute] long eventId)
    {
        var result = await _eventService.DeleteEventAsync(teacherId, eventId);
        return ToResponse(result);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // ENDPOINT 5b: SET CUSTOM AMOUNT FOR STUDENT IN EVENT
    // ══════════════════════════════════════════════════════════════════════════
    //
    // WHAT IT DOES:
    //   Overrides the event amount for a specific student.
    //   REQ-EVT-007: Custom amount per student within event.
    //
    // TABLES WRITTEN: EventStudentObligations
    //
    // SAMPLE: PUT /api/eventpayment/123/events/456/students/789/custom-amount
    //   { "customAmount": 75.00 }
    //
    // ══════════════════════════════════════════════════════════════════════════
    [HttpPut("{teacherId:long}/events/{eventId:long}/students/{teacherStudentId:long}/custom-amount")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetEventStudentCustomAmount(
        [FromRoute] long teacherId,
        [FromRoute] long eventId,
        [FromRoute] long teacherStudentId,
        [FromBody] SetEventStudentCustomAmountDto dto)
    {
        dto.TeacherId = teacherId;
        dto.EventId = eventId;
        dto.TeacherStudentId = teacherStudentId;
        var result = await _eventService.SetEventStudentCustomAmountAsync(dto);
        return ToResponse(result);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // ENDPOINT 6: COLLECT EVENT PAYMENT
    // ══════════════════════════════════════════════════════════════════════════
    //
    // WHAT IT DOES:
    //   Collects a payment from a student for an event.
    //   REQ-EVT-009: Same collection methods as regular payments.
    //   REQ-EVT-012: Already-paid duplicate warning.
    //   REQ-EVT-013: Included in collector's wallet balance.
    //
    // TABLES WRITTEN: EventPaymentTransactions, EventStudentObligations, PaymentEvents, AssistantWallets
    // TABLES READ: PaymentEvents, EventStudentObligations, TeacherStudents
    //
    // SAMPLE: POST /api/eventpayment/123/events/456/collect
    //   { "teacherStudentId": 789, "amount": 50.00, "paymentMethod": "ManualCode" }
    //
    // ══════════════════════════════════════════════════════════════════════════
    [HttpPost("{teacherId:long}/events/{eventId:long}/collect")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CollectEventPayment(
        [FromRoute] long teacherId,
        [FromRoute] long eventId,
        [FromBody] CollectEventPaymentDto dto)
    {
        dto.TeacherId = teacherId;
        dto.EventId = eventId;
        var result = await _eventService.CollectEventPaymentAsync(dto);
        return ToResponse(result);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // ENDPOINT 7: GENERATE EVENT REPORT
    // ══════════════════════════════════════════════════════════════════════════
    [HttpPost("{teacherId:long}/reports/generate")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GenerateEventReport(
        [FromRoute] long teacherId,
        [FromBody] EventReportRequestDto request)
    {
        var result = await _eventService.GenerateEventReportAsync(teacherId, request);
        return ToResponse(result);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // ENDPOINT 8: EXPORT EVENT REPORT
    // ══════════════════════════════════════════════════════════════════════════
    [HttpPost("{teacherId:long}/reports/export")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ExportEventReport(
        [FromRoute] long teacherId,
        [FromBody] EventReportRequestDto request,
        [FromQuery] string format = "xlsx")
    {
        var result = await _eventService.ExportEventReportAsync(teacherId, request, format);
        if (!result.IsSuccess)
            return ToResponse(result);

        var contentType = format == "pdf" ? "application/pdf"
            : "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
        var fileName = $"event-report-{DateTime.UtcNow:yyyyMMdd-HHmmss}.{format}";

        return File(result.Data!, contentType, fileName);
    }
}