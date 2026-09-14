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
        // Recorded on each scope row so the targeting decision is attributable.
        dto.ActingUserId = GetActingUserId();

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
    // Widened (D14): an assistant granted only CollectPayment would otherwise 403 on the very list
    // they must collect from. alsoAllowPermission is checked ONLY after the primary fails and never
    // past a module-not-assigned failure, so it can only ever widen — no caller loses access.
    [ModulePermission(PaymentConstants.EventModuleName, PaymentConstants.EventPermissionView,
        alsoAllowPermission: PaymentConstants.EventPermissionCollectPayment)]
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
    [ModulePermission(PaymentConstants.EventModuleName, PaymentConstants.EventPermissionView,
        alsoAllowPermission: PaymentConstants.EventPermissionCollectPayment)]
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
        // Role + actor from the token, never the body: student REMOVAL is tutor-only (BR-EVT-003)
        // and every mutation is audited against the acting user.
        dto.IsAssistantCaller = IsAssistantCaller();
        dto.ActingUserId = GetActingUserId();

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

        var result = await _eventService.DeleteEventAsync(
            teacherId.Value, eventId, GetActingUserId());
        return ToResponse(result);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // ENDPOINT 5a2: CLOSE / REOPEN AN ITEM
    // PUT api/eventpayment/items/{itemId}/closed
    // ══════════════════════════════════════════════════════════════════════════
    //
    // WHAT IT DOES:
    //   Closed = no further collection. Refunds, history, tracking and the ledger all keep
    //   working, and auto-include stops adding new joiners.
    //
    //   This is the ESCAPE HATCH for an item that cannot be deleted because money was collected
    //   against it (DELETE answers 409 and names this route). Reversible on purpose: a tutor who
    //   closed one a class too early must be able to reopen it rather than recreate it and lose
    //   the history.
    //
    // AUTH: Teacher or SuperAdmin ONLY — it is the counterpart of DELETE, and BR-EVT-003 makes
    //   ending an item's life a tutor decision.
    //
    // ══════════════════════════════════════════════════════════════════════════
    [HttpPut("items/{itemId:long}/closed")]
    [ModulePermission(roles: new[] { "Teacher", "SuperAdmin" }, roleOnly: true)]
    [ProducesResponseType(typeof(Edvanz.Application.Dtos.Result<EventDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(object), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetItemClosed(
        [FromRoute] long itemId, [FromBody] SetExtrasItemClosedDto dto)
    {
        long? teacherId = await ResolveTeacherIdAsync();
        if (teacherId is null) return TeacherNotResolved();

        var result = await _eventService.SetEventClosedAsync(
            teacherId.Value, itemId, dto.Closed, GetActingUserId());
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

        dto.ActingUserId = GetActingUserId();

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
    // ITEM TRACKING v2 — header + breakdowns (NO student rows)
    // GET api/eventpayment/items/{itemId}/tracking
    // ══════════════════════════════════════════════════════════════════════════
    //
    // Replaces events/{id}/tracking, which returned paid[]/unpaid[] as FULL ARRAYS while accepting
    // page/pageSize, discarded both total counts, and could answer nothing by session or by group.
    // The legacy route stays alive and delegating for the published openapi/Postman contract.
    //
    // AUTH: the module's View permission, ALSO satisfied by CollectPayment. Without that widening an
    //   assistant granted only CollectPayment would 403 on the very list they must collect from —
    //   the same widen-only mechanism already used on /api/v1/payments/collections.
    //
    // The by-collector breakdown is force-scoped for an assistant caller; the paid/unpaid ROSTER is
    // deliberately teacher-wide, because an assistant must see who still owes in order to collect.
    //
    // ══════════════════════════════════════════════════════════════════════════
    [HttpGet("items/{itemId:long}/tracking")]
    [ModulePermission(PaymentConstants.EventModuleName, PaymentConstants.EventPermissionView,
        alsoAllowPermission: PaymentConstants.EventPermissionCollectPayment)]
    [ProducesResponseType(typeof(Edvanz.Application.Dtos.Result<Edvanz.Application.Dtos.Payment.ExtrasItemTrackingResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(object), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetExtrasItemTracking([FromRoute] long itemId)
    {
        long? teacherId = await ResolveTeacherIdAsync();
        if (teacherId is null) return TeacherNotResolved();

        var result = await _eventService.GetExtrasItemTrackingAsync(
            teacherId.Value, itemId, AssistantScopeUserId());
        return ToResponse(result);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // ITEM ROSTER — paged, with the four chip counts
    // GET api/eventpayment/items/{itemId}/students
    //     ?status=all|paid|partiallyPaid|unpaid|exempt&sessionId=&sessionGroupId=
    //     &collectedByUserId=&search=&page=&pageSize=
    // ══════════════════════════════════════════════════════════════════════════
    //
    // The chip counts are measured on the SEARCHED set but BEFORE the chip filter, so selecting a
    // chip never renumbers the chips; and they share one bucket definition with the list, so a chip
    // can never claim a number the list it opens disagrees with (BUG-17/BUG-23).
    //
    // ══════════════════════════════════════════════════════════════════════════
    [HttpGet("items/{itemId:long}/students")]
    [ModulePermission(PaymentConstants.EventModuleName, PaymentConstants.EventPermissionView,
        alsoAllowPermission: PaymentConstants.EventPermissionCollectPayment)]
    [ProducesResponseType(typeof(Edvanz.Application.Dtos.Result<Edvanz.Application.Dtos.Payment.ExtrasStudentsPageDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(object), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetExtrasItemStudents(
        [FromRoute] long itemId,
        [FromQuery] string? status = null,
        [FromQuery] long? sessionId = null,
        [FromQuery] long? sessionGroupId = null,
        [FromQuery] long? collectedByUserId = null,
        [FromQuery] string? search = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        long? teacherId = await ResolveTeacherIdAsync();
        if (teacherId is null) return TeacherNotResolved();

        // An assistant may narrow the roster to their OWN collections, but may never ask for a
        // peer's — same forced own-scope as every other collector-filtered read in the module.
        collectedByUserId = AssistantScopeUserId() ?? collectedByUserId;

        var result = await _eventService.GetExtrasItemStudentsAsync(
            teacherId.Value, itemId, status, sessionId, sessionGroupId, collectedByUserId,
            search, page, pageSize);
        return ToResponse(result);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // ONE STUDENT'S PAYMENTS ON ONE ITEM
    // GET api/eventpayment/items/{itemId}/students/{teacherStudentId}/payments
    // ══════════════════════════════════════════════════════════════════════════
    //
    // WHAT IT DOES:
    //   The list the tutor picks from before refunding or correcting a collection — newest first,
    //   with who took each one and, where a payment has been corrected, what it originally was.
    //
    //   ALREADY-REFUNDED payments are ABSENT (the transaction's !IsDeleted query filter), so a
    //   payment can never be offered for refunding twice.
    //
    // AUTH: View, ALSO satisfied by CollectPayment — reading who paid what is not a money action.
    //   The refund and the correction themselves stay tutor-only (below).
    //
    // ══════════════════════════════════════════════════════════════════════════
    [HttpGet("items/{itemId:long}/students/{teacherStudentId:long}/payments")]
    [ModulePermission(PaymentConstants.EventModuleName, PaymentConstants.EventPermissionView,
        alsoAllowPermission: PaymentConstants.EventPermissionCollectPayment)]
    [ProducesResponseType(typeof(Edvanz.Application.Dtos.Result<List<EventPaymentTransactionDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(object), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetExtrasStudentPayments(
        [FromRoute] long itemId, [FromRoute] long teacherStudentId)
    {
        long? teacherId = await ResolveTeacherIdAsync();
        if (teacherId is null) return TeacherNotResolved();

        return ToResponse(await _eventService.GetEventStudentPaymentsAsync(
            teacherId.Value, itemId, teacherStudentId));
    }

    // ══════════════════════════════════════════════════════════════════════════
    // ADD THE NEW JOINERS — the "N new students · add them?" banner's action
    // POST api/eventpayment/items/{itemId}/new-joiners
    // ══════════════════════════════════════════════════════════════════════════
    //
    // Its own endpoint because the auto-include materializer fires on ASSIGNMENT, so an item whose
    // AutoIncludeNewStudents is off has no assignment-time path to a later joiner by definition.
    // Shares ONE scope resolver with the banner's COUNT, so the number offered and the number added
    // cannot disagree — a tutor tapping "add 3" and getting 2 is the bug that shape invites.
    //
    // Under Edit, not roleOnly: adding an obligation is an administrative act, not money out.
    //
    // ══════════════════════════════════════════════════════════════════════════
    [HttpPost("items/{itemId:long}/new-joiners")]
    [ModulePermission(PaymentConstants.EventModuleName, PaymentConstants.EventPermissionEdit)]
    [ProducesResponseType(typeof(Edvanz.Application.Dtos.Result<int>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(object), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(object), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> AddExtrasNewJoiners([FromRoute] long itemId)
    {
        long? teacherId = await ResolveTeacherIdAsync();
        if (teacherId is null) return TeacherNotResolved();

        var result = await _eventService.AddNewJoinersAsync(
            teacherId.Value, itemId, GetActingUserId());
        return ToResponse(result);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // UNPAID DUES, STUDENT-KEYED — the attendance sheet + offline hydration feed
    // GET api/eventpayment/debts?teacherStudentIds=1,2,3
    // ══════════════════════════════════════════════════════════════════════════
    //
    // Returns showExtrasInfo:false with an empty map when the teacher has switched the behaviour
    // off, so "off" and "nobody owes anything" are distinguishable on the wire — the client keys
    // off the FLAG, never off an empty map (the ShowPaymentInfo precedent).
    //
    // View, also satisfied by CollectPayment: this is the feed the collect sheet is built from, so
    // an assistant who may collect must be able to read it.
    //
    // ══════════════════════════════════════════════════════════════════════════
    [HttpGet("debts")]
    [ModulePermission(PaymentConstants.EventModuleName, PaymentConstants.EventPermissionView,
        alsoAllowPermission: PaymentConstants.EventPermissionCollectPayment)]
    [ProducesResponseType(typeof(Edvanz.Application.Dtos.Result<Edvanz.Application.Dtos.Payment.ExtrasDebtsResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(object), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetExtrasDebts([FromQuery] string? teacherStudentIds = null)
    {
        long? teacherId = await ResolveTeacherIdAsync();
        if (teacherId is null) return TeacherNotResolved();

        // Comma-separated ids so one roster can be hydrated in a single call. Unparseable entries
        // are ignored rather than 400-ing the whole request: a partially-stale client id list must
        // still get the students it named correctly.
        List<long>? ids = null;
        if (!string.IsNullOrWhiteSpace(teacherStudentIds))
        {
            ids = teacherStudentIds
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(x => long.TryParse(x, out var n) ? n : (long?)null)
                .Where(x => x.HasValue)
                .Select(x => x!.Value)
                .Distinct()
                .ToList();
        }

        var result = await _eventService.GetExtrasDebtsAsync(teacherId.Value, ids);
        return ToResponse(result);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // REFUND A COLLECTED "BOOKS & FEES" PAYMENT  (money OUT — TUTOR-ONLY)
    // POST api/eventpayment/transactions/{transactionId}/refund
    // ══════════════════════════════════════════════════════════════════════════
    //
    // WHY roleOnly AND NOT A MODULE PERMISSION: this moves money out of a collector's wallet.
    // BR-PAY-002 makes the equivalent fee action absolute tutor-only
    // (DELETE api/payment/transactions/{id}), and an assistant refunding their own collection is
    // exactly the case that rule exists to prevent. Creating and COLLECTING stay delegable.
    //
    // TABLES WRITTEN: EventPaymentTransactions (soft delete / amount), EventPaymentEditLogs,
    //                 EventStudentObligations, PaymentEvents, AssistantWallets
    //
    // ══════════════════════════════════════════════════════════════════════════
    [HttpPost("transactions/{transactionId:long}/refund")]
    [ModulePermission(roles: new[] { "Teacher", "SuperAdmin" }, roleOnly: true)]
    [ProducesResponseType(typeof(Edvanz.Application.Dtos.Result<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(object), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(object), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RefundEventPayment(
        [FromRoute] long transactionId,
        [FromBody] RefundEventPaymentDto? dto)
    {
        long? teacherId = await ResolveTeacherIdAsync();
        if (teacherId is null) return TeacherNotResolved();

        var result = await _eventService.RefundEventPaymentAsync(
            teacherId.Value, transactionId, dto?.Amount, GetActingUserId(), dto?.Reason);
        return ToResponse(result);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // CORRECT A COLLECTED AMOUNT  (money — TUTOR-ONLY, same reasoning as the refund)
    // PUT api/eventpayment/transactions/{transactionId}
    // ══════════════════════════════════════════════════════════════════════════
    [HttpPut("transactions/{transactionId:long}")]
    [ModulePermission(roles: new[] { "Teacher", "SuperAdmin" }, roleOnly: true)]
    [ProducesResponseType(typeof(Edvanz.Application.Dtos.Result<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(object), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(object), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> EditEventPayment(
        [FromRoute] long transactionId,
        [FromBody] EditEventPaymentDto dto)
    {
        long? teacherId = await ResolveTeacherIdAsync();
        if (teacherId is null) return TeacherNotResolved();

        var result = await _eventService.EditEventPaymentAsync(
            teacherId.Value, transactionId, dto.NewAmount, GetActingUserId(), dto.Reason);
        return ToResponse(result);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // MARK A STUDENT AS NOT TAKING (OR TAKING AGAIN) AN ITEM
    // PUT api/eventpayment/events/{eventId}/students/{teacherStudentId}/exempt
    // ══════════════════════════════════════════════════════════════════════════
    //
    // Under the module's Edit permission, NOT roleOnly: an exemption is an administrative statement
    // and moves no cash. It is BLOCKED (409) when the student has already paid — refund first.
    //
    // ══════════════════════════════════════════════════════════════════════════
    [HttpPut("events/{eventId:long}/students/{teacherStudentId:long}/exempt")]
    [ModulePermission(PaymentConstants.EventModuleName, PaymentConstants.EventPermissionEdit)]
    [ProducesResponseType(typeof(Edvanz.Application.Dtos.Result<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(object), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(object), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> SetEventStudentExempt(
        [FromRoute] long eventId,
        [FromRoute] long teacherStudentId,
        [FromBody] SetEventStudentExemptDto dto)
    {
        long? teacherId = await ResolveTeacherIdAsync();
        if (teacherId is null) return TeacherNotResolved();

        var result = await _eventService.SetEventStudentExemptAsync(
            teacherId.Value, eventId, teacherStudentId, dto.Exempt, GetActingUserId(), dto.Reason);
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
