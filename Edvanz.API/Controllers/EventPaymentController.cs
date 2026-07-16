using Edvanz.API.Attributes;
using Edvanz.Application.Dtos.Payment;
using Edvanz.Application.IservicesContract;
using Edvanz.Application.ServiceContract;
using Edvanz.Domain.Constants;
using Edvanz.Domain.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Edvanz.API.Controllers;

/// <summary>
/// Event-Based Payment Module (Module 5) — teacher and assistant endpoints.
///
/// ARCHITECTURE CHANGES (auth rebase):
/// <list type="bullet">
///   <item>Base class changed from <see cref="ApiBaseController"/> →
///         <see cref="ModuleSixApiBaseController"/> to gain
///         <c>ResolveTeacherIdAsync()</c> and <c>GetActingUserId()</c>.</item>
///   <item>Class-level <c>[Authorize]</c> added — every action requires a
///         valid JWT.</item>
///   <item><c>{teacherId}</c> removed from every route. Tenant id is derived
///         from the JWT only (catalog §1.3 / REQ-EVT-NFR-001).</item>
///   <item>Every action carries <c>[ModulePermission]</c>.</item>
///   <item><c>CollectedByUserId</c> is always set from <c>GetActingUserId()</c>
///         — never from the request body (REQ-EVT-013).</item>
/// </list>
///
/// ABSOLUTE TUTOR-ONLY GATES (BR-EVT-003):
///   DeleteEvent uses <c>roleOnly: ["Teacher","SuperAdmin"]</c> and cannot
///   be delegated to assistants under any configuration.
///
/// ASSISTANT-DELEGATABLE GATES (REQ-USR-019):
///   View, Create, Edit, CollectPayment, GenerateReports — accessible to
///   assistants when the named permission is granted.
///
/// SERVICE-LEVEL GAP (flagged for future fix):
///   <c>UpdateEventDto.StudentIdsToRemove</c> is handled inside the same
///   endpoint as <c>StudentIdsToAdd</c>. BR-EVT-003 restricts student removal
///   to tutor-only, but the current <c>EventPaymentService.UpdateEventAsync</c>
///   does not check caller role before processing removals. Once this endpoint
///   is wired to the authenticated role the service must enforce that check.
/// </summary>
[Authorize]
public sealed class EventPaymentController : ModuleSixApiBaseController
{
    private readonly IEventPaymentService _eventService;

    public EventPaymentController(
        IEventPaymentService eventService,
        ICurrentUserService currentUser,
        IUnitOfWork unitOfWork)
        : base(currentUser, unitOfWork)
    {
        _eventService = eventService;
    }

    // ══════════════════════════════════════════════════════════════════════════
    // ENDPOINT 1: CREATE EVENT
    // POST api/eventpayment/events
    // ══════════════════════════════════════════════════════════════════════════
    //
    // WHAT IT DOES:
    //   Creates a new one-time payment event with target scope resolution.
    //   REQ-EVT-001/002: Event name, Amount, Target Scope, Date are mandatory.
    //   BR-EVT-001: Obligations created for students in scope at creation time.
    //
    // AUTH: Teacher (module) OR Assistant with Event-Based Payment.Create permission.
    //
    // TABLES WRITTEN: PaymentEvents, EventStudentObligations
    // TABLES READ: TeacherStudents, Sessions, SessionGroups
    //
    // ══════════════════════════════════════════════════════════════════════════
    [HttpPost("events")]
    [ModulePermission(PaymentConstants.EventModuleName, PaymentConstants.EventPermissionCreate)]
    [ProducesResponseType(typeof(Edvanz.Application.Dtos.Result<Edvanz.Application.Dtos.Payment.EventDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(object), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(object), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> CreateEvent([FromBody] CreateEventDto dto)
    {
        long? teacherId = await ResolveTeacherIdAsync();
        if (teacherId is null) return TeacherNotResolved();

        dto.TeacherId = teacherId.Value;

        var result = await _eventService.CreateEventAsync(dto);
        return ToResponse(result);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // ENDPOINT 2: LIST EVENTS
    // GET api/eventpayment/events
    // ══════════════════════════════════════════════════════════════════════════
    //
    // WHAT IT DOES:
    //   Returns paginated, filtered list of active events for the teacher.
    //   REQ-EVT-017/018: Event Overview — searchable, filterable by scope and completion.
    //
    // AUTH: Teacher (module) OR Assistant with Event-Based Payment.View permission.
    //
    // ══════════════════════════════════════════════════════════════════════════
    [HttpGet("events")]
    [ModulePermission(PaymentConstants.EventModuleName, PaymentConstants.EventPermissionView)]
    [ProducesResponseType(typeof(Edvanz.Application.Dtos.Result<Edvanz.Application.Dtos.PaginatedResponse<System.Collections.Generic.List<Edvanz.Application.Dtos.Payment.EventDto>>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(object), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetEvents([FromQuery] EventListFilterDto filter)
    {
        long? teacherId = await ResolveTeacherIdAsync();
        if (teacherId is null) return TeacherNotResolved();

        var result = await _eventService.GetEventsAsync(teacherId.Value, filter);
        return ToResponse(result);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // ENDPOINT 3: EVENT TRACKING DETAIL
    // GET api/eventpayment/events/{eventId}/tracking
    // ══════════════════════════════════════════════════════════════════════════
    //
    // WHAT IT DOES:
    //   Returns the detailed tracking view for a specific event, with separate
    //   paid/unpaid student lists, search, and pagination.
    //   REQ-EVT-014/015/016: Full tracking view.
    //
    // AUTH: Teacher (module) OR Assistant with Event-Based Payment.View permission.
    //
    // ══════════════════════════════════════════════════════════════════════════
    [HttpGet("events/{eventId:long}/tracking")]
    [ModulePermission(PaymentConstants.EventModuleName, PaymentConstants.EventPermissionView)]
    [ProducesResponseType(typeof(Edvanz.Application.Dtos.Result<Edvanz.Application.Dtos.Payment.EventTrackingDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(object), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetEventTracking(
        [FromRoute] long eventId,
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        long? teacherId = await ResolveTeacherIdAsync();
        if (teacherId is null) return TeacherNotResolved();

        var result = await _eventService.GetEventTrackingAsync(teacherId.Value, eventId, search, page, pageSize);
        return ToResponse(result);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // ENDPOINT 4: UPDATE EVENT
    // PUT api/eventpayment/events/{eventId}
    // ══════════════════════════════════════════════════════════════════════════
    //
    // WHAT IT DOES:
    //   Updates an event: name, amount, notes, and add/remove students.
    //   REQ-EVT-019: Edit name, amount, notes.
    //   REQ-EVT-020: Add more students after creation.
    //   REQ-EVT-021: Remove students (unpaid only — service enforces).
    //
    // AUTH: Teacher (module) OR Assistant with Event-Based Payment.Edit permission.
    //
    // SERVICE-LEVEL GAP: StudentIdsToRemove processing must additionally check
    //   that the caller is Teacher/SuperAdmin (BR-EVT-003). This check is missing
    //   in EventPaymentService.UpdateEventAsync — flagged for a follow-up fix.
    //
    // ══════════════════════════════════════════════════════════════════════════
    [HttpPut("events/{eventId:long}")]
    [ModulePermission(PaymentConstants.EventModuleName, PaymentConstants.EventPermissionEdit)]
    [ProducesResponseType(typeof(Edvanz.Application.Dtos.Result<Edvanz.Application.Dtos.Payment.EventDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(object), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateEvent(
        [FromRoute] long eventId,
        [FromBody] UpdateEventDto dto)
    {
        long? teacherId = await ResolveTeacherIdAsync();
        if (teacherId is null) return TeacherNotResolved();

        dto.TeacherId = teacherId.Value;
        dto.EventId = eventId;

        var result = await _eventService.UpdateEventAsync(dto);
        return ToResponse(result);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // ENDPOINT 5: DELETE EVENT
    // DELETE api/eventpayment/events/{eventId}
    // ══════════════════════════════════════════════════════════════════════════
    //
    // WHAT IT DOES:
    //   Soft-deletes an event. Collected EventPaymentTransactions are retained.
    //   REQ-EVT-022: Confirmation required before calling; deletion is irreversible.
    //   BR-EVT-003/005: ABSOLUTE — only the tutor role may delete events.
    //
    // AUTH: Teacher or SuperAdmin ONLY (roleOnly gate).
    //
    // ══════════════════════════════════════════════════════════════════════════
    [HttpDelete("events/{eventId:long}")]
    [ModulePermission(roles: new[] { "Teacher", "SuperAdmin" }, roleOnly: true)]
    [ProducesResponseType(typeof(Edvanz.Application.Dtos.Result<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(object), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteEvent([FromRoute] long eventId)
    {
        long? teacherId = await ResolveTeacherIdAsync();
        if (teacherId is null) return TeacherNotResolved();

        var result = await _eventService.DeleteEventAsync(teacherId.Value, eventId);
        return ToResponse(result);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // ENDPOINT 5b: SET CUSTOM AMOUNT FOR STUDENT IN EVENT
    // PUT api/eventpayment/events/{eventId}/students/{teacherStudentId}/custom-amount
    // ══════════════════════════════════════════════════════════════════════════
    //
    // WHAT IT DOES:
    //   Overrides the event amount for a specific student.
    //   REQ-EVT-007: Custom amount per student within event.
    //
    // AUTH: Teacher (module) OR Assistant with Event-Based Payment.Edit permission.
    //
    // ══════════════════════════════════════════════════════════════════════════
    [HttpPut("events/{eventId:long}/students/{teacherStudentId:long}/custom-amount")]
    [ModulePermission(PaymentConstants.EventModuleName, PaymentConstants.EventPermissionEdit)]
    [ProducesResponseType(typeof(Edvanz.Application.Dtos.Result<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(object), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetEventStudentCustomAmount(
        [FromRoute] long eventId,
        [FromRoute] long teacherStudentId,
        [FromBody] SetEventStudentCustomAmountDto dto)
    {
        long? teacherId = await ResolveTeacherIdAsync();
        if (teacherId is null) return TeacherNotResolved();

        dto.TeacherId = teacherId.Value;
        dto.EventId = eventId;
        dto.TeacherStudentId = teacherStudentId;

        var result = await _eventService.SetEventStudentCustomAmountAsync(dto);
        return ToResponse(result);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // ENDPOINT 6: COLLECT EVENT PAYMENT
    // POST api/eventpayment/events/{eventId}/collect
    // ══════════════════════════════════════════════════════════════════════════
    //
    // WHAT IT DOES:
    //   Collects a payment for an event from a student.
    //   REQ-EVT-009: Same collection methods as regular payments.
    //   REQ-EVT-011: Supports partial payments.
    //   REQ-EVT-012: Already-paid duplicate warning.
    //   REQ-EVT-013: CollectedByUserId always sourced from JWT (acting user).
    //
    // AUTH: Teacher (module) OR Assistant with Event-Based Payment.CollectPayment permission.
    //
    // TABLES WRITTEN: EventPaymentTransactions, EventStudentObligations, PaymentEvents, AssistantWallets
    //
    // ══════════════════════════════════════════════════════════════════════════
    [HttpPost("events/{eventId:long}/collect")]
    [ModulePermission(PaymentConstants.EventModuleName, PaymentConstants.EventPermissionCollectPayment)]
    [ProducesResponseType(typeof(Edvanz.Application.Dtos.Result<Edvanz.Application.Dtos.Payment.EventPaymentResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(object), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(object), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CollectEventPayment(
        [FromRoute] long eventId,
        [FromBody] CollectEventPaymentDto dto)
    {
        long? teacherId = await ResolveTeacherIdAsync();
        if (teacherId is null) return TeacherNotResolved();

        // REQ-EVT-013: actor identity is always the authenticated user — never client-supplied.
        dto.TeacherId = teacherId.Value;
        dto.EventId = eventId;
        dto.CollectedByUserId = GetActingUserId();

        var result = await _eventService.CollectEventPaymentAsync(dto);
        return ToResponse(result);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // ENDPOINT 7: GENERATE EVENT REPORT
    // POST api/eventpayment/reports/generate
    // ══════════════════════════════════════════════════════════════════════════
    //
    // WHAT IT DOES:
    //   Generates a Single Event Report (REQ-EVT-023) or All Events Summary
    //   Report (REQ-EVT-024).
    //
    // AUTH: Teacher (module) OR Assistant with Event-Based Payment.GenerateReports permission.
    //
    // ══════════════════════════════════════════════════════════════════════════
    [HttpPost("reports/generate")]
    [ModulePermission(PaymentConstants.EventModuleName, PaymentConstants.EventPermissionGenerateReports)]
    [ProducesResponseType(typeof(Edvanz.Application.Dtos.Result<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(object), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(object), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GenerateEventReport([FromBody] EventReportRequestDto request)
    {
        long? teacherId = await ResolveTeacherIdAsync();
        if (teacherId is null) return TeacherNotResolved();

        var result = await _eventService.GenerateEventReportAsync(teacherId.Value, request);
        return ToResponse(result);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // ENDPOINT 8: EXPORT EVENT REPORT
    // POST api/eventpayment/reports/export
    // ══════════════════════════════════════════════════════════════════════════
    //
    // WHAT IT DOES:
    //   Exports an event report as PDF or Excel.
    //   REQ-EVT-025: Exportable in PDF or Excel format.
    //   REQ-EVT-026: Standard header in all exported reports.
    //
    // AUTH: Teacher (module) OR Assistant with Event-Based Payment.GenerateReports permission.
    //
    // NOTE (P2): IPaymentReportExportService is a stub — real PDF/Excel rendering
    //   is not yet implemented.
    //
    // ══════════════════════════════════════════════════════════════════════════
    [HttpPost("reports/export")]
    [ModulePermission(PaymentConstants.EventModuleName, PaymentConstants.EventPermissionGenerateReports)]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(object), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(object), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ExportEventReport(
        [FromBody] EventReportRequestDto request,
        [FromQuery] string format = "xlsx")
    {
        long? teacherId = await ResolveTeacherIdAsync();
        if (teacherId is null) return TeacherNotResolved();

        var result = await _eventService.ExportEventReportAsync(teacherId.Value, request, format);
        if (!result.IsSuccess)
            return ToResponse(result);

        var contentType = format == "pdf"
            ? "application/pdf"
            : "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
        var fileName = $"event-report-{DateTime.UtcNow:yyyyMMdd-HHmmss}.{format}";

        return File(result.Data!, contentType, fileName);
    }
}
